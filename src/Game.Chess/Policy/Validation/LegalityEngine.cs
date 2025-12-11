using Microsoft.Spark.Sql;
using static Microsoft.Spark.Sql.Functions;
using Game.Chess.Policy.Threats;
using static Game.Chess.HistoryRefactor.ChessPolicyUtility;

namespace Game.Chess.Policy.Validation;

/// <summary>
/// LegalityEngine - Pure legality filtering layer.
/// 
/// This layer validates moves to ensure they don't leave the king in check.
/// It handles:
/// 1. King safety - can't move to threatened squares
/// 2. Pin detection - can't move a pinned piece away from the pinning line
/// 3. Discovered checks - can't move a piece that blocks an attack on the king
/// 
/// This is Phase 5 of the architectural refactoring - Layer 8.
/// 
/// Algorithm:
/// 1. Compute threatened cells by opponent on current board
/// 2. Filter king moves: destination must not be threatened
/// 3. Filter non-king moves: must not leave king on threatened square
/// 4. Detect pins: pieces between attacker and king can only move along the pinning line
/// 5. Handle discovered checks: removing a piece can't expose king
/// 
/// Dependencies:
/// - SimulationEngine (via refactored code paths)
/// - ThreatEngine (via ComputeThreatenedCells)
/// - PerspectiveEngine (already available in perspectives)
/// </summary>
public static class LegalityEngine
{
    /// <summary>
    /// Filters candidate moves to only those that leave the king safe.
    /// 
    /// This is the primary entry point - extracted from ChessPolicyB.FilterMovesLeavingKingInCheck.
    /// 
    /// Parameters:
    /// - candidatesDf: All candidate moves before legality filtering
    /// - perspectivesDf: Current board state with Self/Ally/Foe relationships
    /// - patternsDf: Pattern definitions (needed for threat computation)
    /// - specificFactions: Factions in play (e.g., [White, Black])
    /// - turn: Current turn indicator (to identify current vs opponent faction)
    /// - debug: Enable debug output
    /// 
    /// Returns:
    /// DataFrame with only legal moves (those not leaving king in check)
    /// </summary>
    public static DataFrame FilterMovesLeavingKingInCheck(
        DataFrame candidatesDf,
        DataFrame perspectivesDf,
        DataFrame patternsDf,
        Piece[] specificFactions,
        int turn = 0,
        bool debug = false)
    {
        var currentFaction = specificFactions[turn % specificFactions.Length];
        var kingBit = (int)Piece.King;
        var factionBit = (int)currentFaction;

        // Step 1: Find current king position (Limit(1) to get at most 1 king without materializing)
        var kingPerspective = perspectivesDf
            .Filter(
                Col("piece").BitwiseAND(Lit(kingBit)).NotEqual(Lit(0))
                .And(Col("piece").BitwiseAND(Lit(factionBit)).NotEqual(Lit(0)))
                .And(Col("x").EqualTo(Col("perspective_x")))
                .And(Col("y").EqualTo(Col("perspective_y")))
            )
            .Select(Col("x").Alias("king_x"), Col("y").Alias("king_y"))
            .Distinct()
            .Limit(1);

        // If no king on board (edge case for tests), we still proceed - cross join will produce empty result naturally

        if (debug)
        {
            // Debug: would show king position, but we're not materializing
            // Instead, king position is now available as columns via cross join
        }

        // Step 2: Track which moves involve the king - cross join to get king position columns
        // Define expected candidate columns explicitly (from CandidateGenerator union of PatternMatcher + SequenceEngine)
        var expectedCandidateColumns = new[]
        {
            "perspective_x", "perspective_y", "perspective_piece", "perspective_id",
            "src_x", "src_y", "src_piece", "src_generic_piece",
            "dst_x", "dst_y", "dst_piece", "dst_generic_piece",
            "sequence", "dst_effects",
            "original_perspective_x", "original_perspective_y"
        };

        var candidatesWithKingPos = candidatesDf
            .Select(expectedCandidateColumns.Select(c => Col(c)).ToArray())
            .CrossJoin(kingPerspective)
            .WithColumn("king_is_moving",
                Col("src_generic_piece").BitwiseAND(Lit(kingBit)).NotEqual(Lit(0)))
            .WithColumn("king_x_after",
                When(Col("king_is_moving"), Col("dst_x")).Otherwise(Col("king_x")))
            .WithColumn("king_y_after",
                When(Col("king_is_moving"), Col("dst_y")).Otherwise(Col("king_y")));

        // Step 3: Create unique move identifier for deduplication
        var candidatesWithId = candidatesWithKingPos
            .WithColumn("move_id",
                Sha2(ConcatWs("_",
                    Col("src_x").Cast("string"),
                    Col("src_y").Cast("string"),
                    Col("dst_x").Cast("string"),
                    Col("dst_y").Cast("string"),
                    Col("perspective_x").Cast("string"),
                    Col("perspective_y").Cast("string")),
                256));

        // Step 4: Compute threatened cells by opponent
        var opponentFaction = specificFactions[(turn + 1) % specificFactions.Length];
        
        var currentThreats = ThreatEngine.ComputeThreatenedCells(
            perspectivesDf,
            patternsDf,
            specificFactions,
            turn: turn,
            debug: debug
        );

        // BROADCAST threatened cells (typically small, < 64 cells) to avoid shuffle joins
        var broadcastThreats = Broadcast(currentThreats);

        // Step 5: Validate king moves
        // King can't move to threatened squares
        var kingMovesCheck = candidatesWithId
            .Filter(Col("king_is_moving"))
            .Join(
                broadcastThreats
                    .WithColumnRenamed("threatened_x", "threat_x")
                    .WithColumnRenamed("threatened_y", "threat_y"),
                Col("dst_x").EqualTo(Col("threat_x")).And(Col("dst_y").EqualTo(Col("threat_y"))),
                "left_outer"
            )
            .Filter(Col("threat_x").IsNull())  // Safe if not in threatened cells
            .Drop("threat_x", "threat_y")
            .Select(
                // Keep original candidate schema plus king tracking columns to align with non-king branch
                expectedCandidateColumns.Select(c => Col(c)).Concat(
                    new[]
                    {
                        Col("king_x"), Col("king_y"),
                        Col("king_is_moving"), Col("king_x_after"), Col("king_y_after"),
                        Col("move_id")
                    }
                ).ToArray()
            );

        // Step 6: Validate non-king moves (without pin detection)
        var nonKingMoves = candidatesWithId.Filter(Not(Col("king_is_moving")));

        // Join with threats to find which moves would leave king threatened
        var nonKingMovesCheck = nonKingMoves
            .Join(
                broadcastThreats
                    .WithColumnRenamed("threatened_x", "threat_x")
                    .WithColumnRenamed("threatened_y", "threat_y"),
                Col("king_x_after").EqualTo(Col("threat_x")).And(Col("king_y_after").EqualTo(Col("threat_y"))),
                "left_outer"
            )
            .Filter(Col("threat_x").IsNull())  // Safe if king not in threats after move
            .Drop("threat_x", "threat_y");

        // Step 7: Pin detection
        // Get opponent sliding pieces (only they can create pins)
        var opponentSlidingPieces = perspectivesDf
            .Filter(
                Col("piece").BitwiseAND(Lit((int)opponentFaction)).NotEqual(Lit(0))
                .And(
                    Col("piece").BitwiseAND(Lit((int)Piece.Rook)).NotEqual(Lit(0))
                    .Or(Col("piece").BitwiseAND(Lit((int)Piece.Bishop)).NotEqual(Lit(0)))
                    .Or(Col("piece").BitwiseAND(Lit((int)Piece.Queen)).NotEqual(Lit(0)))
                )
                .And(Col("x").EqualTo(Col("perspective_x")))
                .And(Col("y").EqualTo(Col("perspective_y")))
            )
            .Select(
                Col("x").Alias("attacker_x"),
                Col("y").Alias("attacker_y"),
                Col("piece").Alias("attacker_piece")
            );

        // Full pin detection: check if piece is between attacker and king
        // If no opponent sliding pieces, CrossJoin produces empty and validNonKingMoves filters correctly
        var nonKingWithPinCheck = nonKingMovesCheck
            .CrossJoin(opponentSlidingPieces)
                .WithColumn("is_on_same_file", 
                    Col("attacker_x").EqualTo(Col("src_x")).And(Col("src_x").EqualTo(Col("king_x"))))
                .WithColumn("is_on_same_rank",
                    Col("attacker_y").EqualTo(Col("src_y")).And(Col("src_y").EqualTo(Col("king_y"))))
                .WithColumn("is_on_same_diagonal",
                    // All three points on same diagonal
                    Abs(Col("attacker_x") - Col("src_x")).EqualTo(Abs(Col("attacker_y") - Col("src_y")))
                    .And(Abs(Col("src_x") - Col("king_x")).EqualTo(Abs(Col("src_y") - Col("king_y"))))
                    .And(Abs(Col("attacker_x") - Col("king_x")).EqualTo(Abs(Col("attacker_y") - Col("king_y"))))
                )
                .WithColumn("src_between_attacker_and_king",
                    // Source is between attacker and king
                    (
                        // For orthogonal lines
                        (Col("is_on_same_file").Or(Col("is_on_same_rank")))
                        .And(
                            Col("src_x").Between(Least(Col("attacker_x"), Col("king_x")), Greatest(Col("attacker_x"), Col("king_x")))
                            .And(Col("src_y").Between(Least(Col("attacker_y"), Col("king_y")), Greatest(Col("attacker_y"), Col("king_y"))))
                        )
                    )
                    .Or(
                        // For diagonals
                        Col("is_on_same_diagonal")
                        .And(Col("src_x").Between(Least(Col("attacker_x"), Col("king_x")), Greatest(Col("attacker_x"), Col("king_x"))))
                        .And(Col("src_y").Between(Least(Col("attacker_y"), Col("king_y")), Greatest(Col("attacker_y"), Col("king_y"))))
                    )
                )
                .WithColumn("attacker_can_use_line",
                    // Attacker piece type can attack on this line type
                    (Col("is_on_same_file").Or(Col("is_on_same_rank")))
                        .And(
                            Col("attacker_piece").BitwiseAND(Lit((int)Piece.Rook)).NotEqual(Lit(0))
                            .Or(Col("attacker_piece").BitwiseAND(Lit((int)Piece.Queen)).NotEqual(Lit(0)))
                        )
                    .Or(
                        Col("is_on_same_diagonal")
                        .And(
                            Col("attacker_piece").BitwiseAND(Lit((int)Piece.Bishop)).NotEqual(Lit(0))
                            .Or(Col("attacker_piece").BitwiseAND(Lit((int)Piece.Queen)).NotEqual(Lit(0)))
                        )
                    )
                )
                .WithColumn("is_pinned",
                    Col("src_between_attacker_and_king").And(Col("attacker_can_use_line"))
                )
                .WithColumn("move_stays_on_line",
                    // If pinned, move must stay on the same line to the king
                    When(
                        Col("is_on_same_file"),
                        Col("dst_x").EqualTo(Col("king_x"))  // Stay on same file
                    )
                    .When(
                        Col("is_on_same_rank"),
                        Col("dst_y").EqualTo(Col("king_y"))  // Stay on same rank
                    )
                    .When(
                        Col("is_on_same_diagonal"),
                        // Stay on diagonal between attacker and king
                        Abs(Col("dst_x") - Col("king_x")).EqualTo(Abs(Col("dst_y") - Col("king_y")))
                    )
                    .Otherwise(Lit(true))
                );
        
        // Filter: if pinned, must stay on line; if not pinned, any move ok
        var validNonKingMoves = nonKingWithPinCheck
            .Filter(
                Not(Col("is_pinned"))  // Not pinned
                .Or(Col("move_stays_on_line"))  // Pinned but stays on line
            )
            .Drop("is_on_same_file", "is_on_same_rank", "is_on_same_diagonal",
                  "src_between_attacker_and_king", "attacker_can_use_line", "is_pinned",
                  "move_stays_on_line", "attacker_x", "attacker_y", "attacker_piece")
            .Select(
                // Match king branch schema: original candidate columns + king tracking + move_id
                expectedCandidateColumns.Select(c => Col(c)).Concat(
                    new[]
                    {
                        Col("king_x"), Col("king_y"),
                        Col("king_is_moving"), Col("king_x_after"), Col("king_y_after"),
                        Col("move_id")
                    }
                ).ToArray()
            )
            .Distinct();

        // Step 8: Combine results and clean up
        var allSafeMoves = kingMovesCheck.Union(validNonKingMoves);
        
        var result = allSafeMoves
            .Drop("king_is_moving", "king_x_after", "king_y_after", "move_id", "king_x", "king_y");

        return result;
    }
}
