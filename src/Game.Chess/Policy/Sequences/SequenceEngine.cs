using Microsoft.Spark.Sql;
using static Microsoft.Spark.Sql.Functions;
using Game.Chess.HistoryRefactor;
using Game.Chess.Policy.Patterns;
using static Game.Chess.HistoryRefactor.ChessPolicyUtility;

namespace Game.Chess.Policy.Sequences;

/// <summary>
/// Sequence engine for multi-step sliding moves.
/// 
/// This layer encapsulates sliding piece logic:
/// 1. Entry moves: Initial sliding patterns (OutX | InstantRecursive flags)
/// 2. Continuation: Expanding along the same direction through empty squares
/// 3. Multi-depth: Iterative expansion up to maxDepth steps
/// 
/// Public methods:
/// - ExpandSequencedMoves: Expands all atomic moves to full sliding sequences
/// 
/// Design:
/// - Reuses PatternMatcher for each step
/// - Memory-efficient iteration without materializing full trees
/// - Preserves direction consistency for sliding paths
/// </summary>
public static class SequenceEngine
{
    /// <summary>
    /// Expands atomic moves into full sliding sequences.
    /// 
    /// For sliding pieces (Rook, Bishop, Queen), this takes the initial patterns
    /// and expands them along their direction until blocked or at boundary.
    /// 
    /// Parameters:
    /// - perspectivesDf: Full perspectives dataframe
    /// - patternsDf: Pattern definitions
    /// - specificFactions: List of factions
    /// - turn: Current turn index
    /// - maxDepth: Maximum sliding depth (typically 7 for 8×8 board)
    /// 
    /// Returns:
    /// DataFrame with all sequenced moves (includes entry + continuation moves)
    /// </summary>
    public static DataFrame ExpandSequencedMoves(
        DataFrame perspectivesDf,
        DataFrame patternsDf,
        Piece[] specificFactions,
        int turn = 0,
        int maxDepth = 7,
        bool debug = false)
    {
        var outMask = (int)ChessPolicyUtility.Sequence.OutMask;
        var instantRecursive = (int)ChessPolicyUtility.Sequence.InstantRecursive;
        var publicFlag = (int)ChessPolicyUtility.Sequence.Public;
        var variantMask = (int)(ChessPolicyUtility.Sequence.Variant1 | ChessPolicyUtility.Sequence.Variant2 | 
                                ChessPolicyUtility.Sequence.Variant3 | ChessPolicyUtility.Sequence.Variant4);
        var emptyBit = (int)Piece.Empty;

        // Step 1: Filter patterns for entry moves (OutX | InstantRecursive, no Public)
        var entryPatternsDf = patternsDf.Filter(
            Col("sequence").BitwiseAND(Lit(outMask)).NotEqual(Lit(0))
            .And(Col("sequence").BitwiseAND(Lit(instantRecursive)).EqualTo(Lit(instantRecursive)))
            .And(Col("sequence").BitwiseAND(Lit(publicFlag)).EqualTo(Lit(0)))
        );

        // Step 2: Get initial entry moves using PatternMatcher
        var initialPerspectivesDf = perspectivesDf
            .WithColumn("original_perspective_x", Col("perspective_x"))
            .WithColumn("original_perspective_y", Col("perspective_y"));

        var entryMoves = PatternMatcher.MatchPatternsWithSequence(
            initialPerspectivesDf,
            entryPatternsDf,
            specificFactions,
            turn: turn,
            activeSequences: ChessPolicyUtility.Sequence.None,
            debug: false
        );

        // Step 3: Start accumulating all moves (entry + continuations)
        var allSequencedMoves = entryMoves.Select(
            Col("perspective_x"),
            Col("perspective_y"),
            Col("perspective_piece"),
            Col("perspective_id"),
            Col("src_x"),
            Col("src_y"),
            Col("src_piece"),
            Col("src_generic_piece"),
            Col("dst_x"),
            Col("dst_y"),
            Col("dst_piece"),
            Col("dst_generic_piece"),
            Col("sequence"),
            Col("dst_effects"),
            Col("src_x").Alias("frontier_x"),
            Col("src_y").Alias("frontier_y"),
            Col("sequence").BitwiseAND(Lit(variantMask)).Alias("direction_key"),
            When(Col("original_perspective_x").IsNull(), Col("src_x"))
                .Otherwise(Col("original_perspective_x")).Alias("original_perspective_x"),
            When(Col("original_perspective_y").IsNull(), Col("src_y"))
                .Otherwise(Col("original_perspective_y")).Alias("original_perspective_y")
        );

        // Step 4: Filter for empty destinations to continue sliding
        var emptyFrontier = entryMoves
            .Filter(Col("dst_generic_piece").BitwiseAND(Lit(emptyBit)).NotEqual(Lit(0)))
            .Select(
                Col("dst_x").Alias("continuation_x"),
                Col("dst_y").Alias("continuation_y"),
                Col("src_x"),
                Col("src_y"),
                Col("perspective_x"),
                Col("perspective_y"),
                Col("perspective_piece"),
                Col("perspective_id"),
                Col("dst_piece"),
                Col("dst_generic_piece"),
                Col("src_piece"),
                Col("src_generic_piece"),
                Col("dst_effects"),
                Col("sequence").BitwiseAND(Lit(variantMask)).Alias("direction_key"),
                When(Col("original_perspective_x").IsNull(), Col("src_x"))
                    .Otherwise(Col("original_perspective_x")).Alias("original_perspective_x"),
                When(Col("original_perspective_y").IsNull(), Col("src_y"))
                    .Otherwise(Col("original_perspective_y")).Alias("original_perspective_y")
            )
            .Cache();  // Cache frontier to help optimizer with repeated use in iterations

        // Cache patterns to avoid recomputation in each iteration
        var cachedPatterns = entryPatternsDf.Cache();

        // Step 5: Iteratively expand sliding moves
        for (int depth = 1; depth < maxDepth; depth++)
        {
            // Get continuation moves
            var continuationMoves = ExpandContinuationMoves(
                emptyFrontier,
                perspectivesDf,
                cachedPatterns,
                specificFactions,
                turn,
                variantMask,
                emptyBit
            );

            // Add continuation moves to results
            var continuationFormatted = continuationMoves.Select(
                Col("perspective_x"),
                Col("perspective_y"),
                Col("perspective_piece"),
                Col("perspective_id"),
                Col("src_x"),
                Col("src_y"),
                Col("src_piece"),
                Col("src_generic_piece"),
                Col("dst_x"),
                Col("dst_y"),
                Col("dst_piece"),
                Col("dst_generic_piece"),
                Col("sequence"),
                Col("dst_effects"),
                Col("frontier_x"),
                Col("frontier_y"),
                Col("direction_key"),
                Col("original_perspective_x"),
                Col("original_perspective_y")
            );

            allSequencedMoves = allSequencedMoves.Union(continuationFormatted);

            // Update frontier for next iteration (only empty destinations)
            // Note: we explicitly exclude 'sequence' from frontier because we'll get it fresh from patterns
            emptyFrontier = continuationMoves
                .Filter(Col("dst_generic_piece").BitwiseAND(Lit(emptyBit)).NotEqual(Lit(0)))
                .Select(
                    Col("dst_x").Alias("continuation_x"),
                    Col("dst_y").Alias("continuation_y"),
                    Col("src_x"),
                    Col("src_y"),
                    Col("perspective_x"),
                    Col("perspective_y"),
                    Col("perspective_piece"),
                    Col("perspective_id"),
                    Col("dst_piece"),
                    Col("dst_generic_piece"),
                    Col("src_piece"),
                    Col("src_generic_piece"),
                    Col("dst_effects"),
                    Col("direction_key"),
                    Col("original_perspective_x"),
                    Col("original_perspective_y")
                )
                .Cache();  // Cache frontier to break query plan dependencies in next iteration
        }

        // Normalize schema to match PatternMatcher output
        // Must explicitly select columns to ensure schema matches atomic moves
        return allSequencedMoves
            .Select(
                Col("perspective_x"),
                Col("perspective_y"),
                Col("perspective_piece"),
                Col("perspective_id"),
                Col("src_x"),
                Col("src_y"),
                Col("src_piece"),
                Col("src_generic_piece"),
                Col("dst_x"),
                Col("dst_y"),
                Col("dst_piece"),
                Col("dst_generic_piece"),
                Col("sequence"),
                Col("dst_effects"),
                Col("original_perspective_x"),
                Col("original_perspective_y")
            );
    }

    // =============== INTERNAL IMPLEMENTATION ===============

    /// <summary>
    /// Expands one step of continuation moves from a frontier.
    /// </summary>
    private static DataFrame ExpandContinuationMoves(
        DataFrame emptyFrontier,
        DataFrame perspectivesDf,
        DataFrame entryPatternsDf,
        Piece[] specificFactions,
        int turn,
        int variantMask,
        int emptyBit)
    {
        var turnFaction = specificFactions[turn % specificFactions.Length];

        // Create perspectives from frontier positions
        var nextPerspectives = emptyFrontier
            .WithColumn("x", Col("continuation_x"))
            .WithColumn("y", Col("continuation_y"))
            .WithColumn("piece", Col("dst_piece"))
            .WithColumn("generic_piece", Col("dst_generic_piece"))
            .WithColumn("perspective_x", Col("continuation_x"))
            .WithColumn("perspective_y", Col("continuation_y"))
            .Select(
                Col("x"),
                Col("y"),
                Col("piece"),
                Col("generic_piece"),
                Col("perspective_x"),
                Col("perspective_y"),
                Col("perspective_piece"),
                Col("perspective_id"),
                Col("src_x"),
                Col("src_y"),
                Col("src_piece"),
                Col("src_generic_piece"),
                Col("dst_effects"),
                Col("direction_key"),
                Col("original_perspective_x"),
                Col("original_perspective_y")
            );

        // Match patterns with direction consistency
        // Broadcast patterns to optimize the CrossJoin
        var nextMoves = nextPerspectives
            .CrossJoin(Functions.Broadcast(entryPatternsDf).Alias("pat"))
            .Filter(
                // Actor at perspective position
                Col("x").EqualTo(Col("perspective_x")).And(
                Col("y").EqualTo(Col("perspective_y"))).And(
                Col("piece").BitwiseAND(Lit((int)turnFaction)).NotEqual(Lit(0))) &
                // Source conditions match
                Col("generic_piece").BitwiseAND(Col("pat.src_conditions")).EqualTo(Col("pat.src_conditions")) &
                // Same direction (variant consistency)
                Col("pat.sequence").BitwiseAND(Lit(variantMask))
                .EqualTo(Col("direction_key"))
            )
            .WithColumn("dst_x", Col("x") + Col("pat.delta_x"))
            .WithColumn("dst_y", Col("y") + Col("pat.delta_y"))
            .Select(
                Col("x"),
                Col("y"),
                Col("piece"),
                Col("generic_piece"),
                Col("perspective_x"),
                Col("perspective_y"),
                Col("perspective_piece"),
                Col("perspective_id"),
                Col("src_x"),
                Col("src_y"),
                Col("src_piece"),
                Col("src_generic_piece"),
                Col("pat.sequence").Alias("sequence"),
                Col("pat.dst_effects").Alias("dst_effects"),
                Col("direction_key"),
                Col("original_perspective_x"),
                Col("original_perspective_y"),
                Col("dst_x"),
                Col("dst_y")
            );

        // Lookup destination pieces
        var lookupDf = perspectivesDf
            .Select(
                Col("x").Alias("lookup_x"),
                Col("y").Alias("lookup_y"),
                Col("perspective_x").Alias("lookup_perspective_x"),
                Col("perspective_y").Alias("lookup_perspective_y"),
                Col("generic_piece").Alias("lookup_generic_piece"),
                Col("piece").Alias("lookup_piece")
            );

        var withDestination = nextMoves
            .Join(
                lookupDf,
                (Col("original_perspective_x") == Col("lookup_perspective_x"))
                .And(Col("original_perspective_y") == Col("lookup_perspective_y"))
                .And(Col("dst_x") == Col("lookup_x"))
                .And(Col("dst_y") == Col("lookup_y")),
                "left_outer"
            )
            .Na().Fill((int)Piece.OutOfBounds, new[] { "lookup_generic_piece" })
            .Select(
                Col("x"),
                Col("y"),
                Col("piece"),
                Col("generic_piece"),
                Col("perspective_x"),
                Col("perspective_y"),
                Col("perspective_piece"),
                Col("perspective_id"),
                Col("src_x"),
                Col("src_y"),
                Col("src_piece"),
                Col("src_generic_piece"),
                Col("sequence"),
                Col("dst_effects"),
                Col("direction_key"),
                Col("original_perspective_x"),
                Col("original_perspective_y"),
                Col("dst_x"),
                Col("dst_y"),
                Col("lookup_generic_piece"),
                Col("lookup_piece")
            )
            .Filter(
                Col("lookup_generic_piece") != Lit((int)Piece.OutOfBounds)
            )
            .WithColumnRenamed("lookup_piece", "dst_piece")
            .WithColumnRenamed("lookup_generic_piece", "dst_generic_piece")
            .Select(
                Col("perspective_x"),
                Col("perspective_y"),
                Col("perspective_piece"),
                Col("perspective_id"),
                Col("src_x"),
                Col("src_y"),
                Col("src_piece"),
                Col("src_generic_piece"),
                Col("dst_x"),
                Col("dst_y"),
                Col("dst_piece"),
                Col("dst_generic_piece"),
                Col("sequence"),
                Col("dst_effects"),
                Col("direction_key"),
                Col("original_perspective_x"),
                Col("original_perspective_y")
            );

        return withDestination.Select(
            Col("perspective_x"),
            Col("perspective_y"),
            Col("perspective_piece"),
            Col("perspective_id"),
            Col("src_x"),
            Col("src_y"),
            Col("src_piece"),
            Col("src_generic_piece"),
            Col("dst_x"),
            Col("dst_y"),
            Col("dst_piece"),
            Col("dst_generic_piece"),
            Col("sequence"),
            Col("dst_effects"),
            Col("src_x").Alias("frontier_x"),
            Col("src_y").Alias("frontier_y"),
            Col("direction_key"),
            Col("original_perspective_x"),
            Col("original_perspective_y")
        );
    }

    // =============== UTILITY METHODS ===============

    private static DataFrame CreateEmptyMovesDf(DataFrame perspectivesDf)
    {
        // Must match the schema that ExpandSequencedMoves returns after drops
        return perspectivesDf.Limit(0)
            .Select(
                Lit(0).Alias("perspective_x"),
                Lit(0).Alias("perspective_y"),
                Lit(0).Alias("perspective_piece"),
                Lit("").Alias("perspective_id"),
                Lit(0).Alias("src_x"),
                Lit(0).Alias("src_y"),
                Lit(0).Alias("src_piece"),
                Lit(0).Alias("src_generic_piece"),
                Lit(0).Alias("dst_x"),
                Lit(0).Alias("dst_y"),
                Lit(0).Alias("dst_generic_piece"),
                Lit(0).Alias("sequence"),
                Lit(0).Alias("dst_effects")
            )
            .Limit(0);
    }

    private static bool IsEmpty(DataFrame df) => false; // Deprecated
}
