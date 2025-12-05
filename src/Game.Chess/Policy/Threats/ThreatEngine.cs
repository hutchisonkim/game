using Microsoft.Spark.Sql;
using static Microsoft.Spark.Sql.Functions;
using Game.Chess.HistoryB;
using Game.Chess.Policy.Patterns;

namespace Game.Chess.Policy.Threats;

/// <summary>
/// Threat computation engine. Computes which cells are under attack by opponent pieces.
/// 
/// This layer encapsulates threat detection logic:
/// 1. Direct threats from non-sliding pieces (Knight, King, Pawn)
/// 2. Sliding threats from Rook, Bishop, Queen along lines/diagonals
/// 3. Iterative expansion of sliding threats to find all threatened squares
/// 
/// Public methods:
/// - ComputeThreatenedCells: Returns DataFrame of (x, y) cells under threat
/// - AddThreatenedBitToPerspectives: Marks threatened cells in perspective dataframe
/// 
/// Uses PatternMatcher internally for both direct and sliding threat computation.
/// Design:
/// - Stateless, pure functions
/// - Reuses PatternMatcher for consistency
/// - Memory-efficient iteration for sliding threats
/// </summary>
public static class ThreatEngine
{
    /// <summary>
    /// Computes all threatened cells on the board from the opponent's pieces.
    /// 
    /// This combines:
    /// 1. Direct threats from non-sliding pieces
    /// 2. Sliding threats from Rook/Bishop/Queen along paths
    /// 
    /// Parameters:
    /// - perspectivesDf: Full perspectives dataframe
    /// - patternsDf: Pattern definitions with adjusted dst_conditions=None for threats
    /// - specificFactions: List of factions
    /// - turn: Current turn index (threat computed from opponent)
    /// 
    /// Returns:
    /// DataFrame with columns: threatened_x, threatened_y
    /// </summary>
    public static DataFrame ComputeThreatenedCells(
        DataFrame perspectivesDf,
        DataFrame patternsDf,
        ChessPolicy.Piece[] specificFactions,
        int turn = 0,
        bool debug = false)
    {
        var opponentTurn = (turn + 1) % specificFactions.Length;

        // Step 1: Create threat patterns by removing dst_conditions (threats ignore empty/occupied)
        var threatPatternsDf = patternsDf
            .WithColumn("dst_conditions", Lit(0)); // Threats can target any square

        // Step 2: Compute direct (non-sliding) threats
        var directThreatsDf = ComputeDirectThreats(
            perspectivesDf,
            threatPatternsDf,
            specificFactions,
            opponentTurn,
            debug
        );

        // Step 3: Compute sliding threats (Rook, Bishop, Queen)
        var slidingThreatsDf = ComputeSlidingThreats(
            perspectivesDf,
            threatPatternsDf,
            specificFactions,
            opponentTurn,
            maxDepth: 7,
            debug: debug
        );

        // Step 4: Union and deduplicate all threatened cells
        var allThreatsDf = directThreatsDf.Union(slidingThreatsDf).Distinct();

        if (debug)
        {
            System.Console.WriteLine($"[ThreatEngine] Total threatened cells: {allThreatsDf.Count()}");
        }

        return allThreatsDf;
    }

    /// <summary>
    /// Adds the Threatened bit to cells in perspectivesDf that are under attack.
    /// </summary>
    public static DataFrame AddThreatenedBitToPerspectives(
        DataFrame perspectivesDf,
        DataFrame threatenedCellsDf,
        bool debug = false)
    {
        // Left join perspectives with threatened cells
        var joinedDf = perspectivesDf.Join(
            threatenedCellsDf,
            (Col("x") == Col("threatened_x")).And(Col("y") == Col("threatened_y")),
            "left_outer"
        );

        // Add Threatened bit to generic_piece where cell is threatened
        var updatedDf = joinedDf.WithColumn(
            "generic_piece",
            When(
                Col("threatened_x").IsNotNull(),
                Col("generic_piece").BitwiseOR(Lit((int)ChessPolicy.Piece.Threatened))
            )
            .Otherwise(Col("generic_piece"))
        );

        // Drop the join columns
        return updatedDf.Drop("threatened_x", "threatened_y");
    }

    // =============== INTERNAL IMPLEMENTATION ===============

    /// <summary>
    /// Computes direct (non-sliding) threats from pieces like Knight, King, Pawn.
    /// These are single-step moves that don't slide.
    /// </summary>
    private static DataFrame ComputeDirectThreats(
        DataFrame perspectivesDf,
        DataFrame patternsDf,
        ChessPolicy.Piece[] specificFactions,
        int opponentTurn,
        bool debug)
    {
        // Use PatternMatcher with sequence mask to match only direct patterns
        // For threats, we pass InMask to enable continuation patterns (first step only)
        var directMovesDf = PatternMatcher.MatchPatternsWithSequence(
            perspectivesDf,
            patternsDf,
            specificFactions,
            turn: opponentTurn,
            activeSequences: ChessPolicy.Sequence.InMask,
            debug: debug
        );

        // Extract unique destination cells
        var threatenedCellsDf = directMovesDf
            .Select(Col("dst_x").Alias("threatened_x"), Col("dst_y").Alias("threatened_y"))
            .Distinct();

        if (debug)
        {
            System.Console.WriteLine($"[DirectThreats] Count: {threatenedCellsDf.Count()}");
        }

        return threatenedCellsDf;
    }

    /// <summary>
    /// Computes sliding threats (Rook, Bishop, Queen) using iterative expansion.
    /// Expands along sliding paths while pieces can continue through empty squares.
    /// </summary>
    private static DataFrame ComputeSlidingThreats(
        DataFrame perspectivesDf,
        DataFrame patternsDf,
        ChessPolicy.Piece[] specificFactions,
        int opponentTurn,
        int maxDepth = 7,
        bool debug = false)
    {
        var outMask = (int)ChessPolicy.Sequence.OutMask;
        var instantRecursive = (int)ChessPolicy.Sequence.InstantRecursive;
        var publicFlag = (int)ChessPolicy.Sequence.Public;

        // Get entry patterns for sliding pieces
        var entryPatternsDf = patternsDf.Filter(
            Col("sequence").BitwiseAND(Lit(outMask)).NotEqual(Lit(0))
            .And(Col("sequence").BitwiseAND(Lit(instantRecursive)).EqualTo(Lit(instantRecursive)))
            .And(Col("sequence").BitwiseAND(Lit(publicFlag)).EqualTo(Lit(0)))
        );

        if (IsEmpty(entryPatternsDf))
            return CreateEmptyThreatenedCellsDf(perspectivesDf);

        // Start with initial perspectives
        var initialPerspectivesDf = perspectivesDf
            .WithColumn("original_perspective_x", Col("perspective_x"))
            .WithColumn("original_perspective_y", Col("perspective_y"));

        // Compute initial sliding moves
        var currentFrontier = PatternMatcher.MatchPatternsWithSequence(
            initialPerspectivesDf,
            entryPatternsDf,
            specificFactions,
            turn: opponentTurn,
            activeSequences: ChessPolicy.Sequence.None,
            debug: false
        );

        if (IsEmpty(currentFrontier))
            return CreateEmptyThreatenedCellsDf(perspectivesDf);

        // Collect all threatened cells from first step
        var allThreatenedCells = currentFrontier
            .Select(Col("dst_x").Alias("threatened_x"), Col("dst_y").Alias("threatened_y"))
            .Distinct();

        // Track positions that can continue sliding (only through empty)
        var emptyBit = (int)ChessPolicy.Piece.Empty;
        var emptyFrontier = currentFrontier
            .Filter(Col("dst_generic_piece").BitwiseAND(Lit(emptyBit)).NotEqual(Lit(0)));

        // Iterate to find all threatened cells
        for (int depth = 1; depth < maxDepth; depth++)
        {
            if (IsEmpty(emptyFrontier)) break;

            // Create perspectives from frontier positions
            var nextPerspectives = ComputeNextPerspectivesFromMoves(emptyFrontier, perspectivesDf);
            if (IsEmpty(nextPerspectives)) break;

            // Compute next step of sliding
            var nextMoves = PatternMatcher.MatchPatternsWithSequence(
                nextPerspectives,
                entryPatternsDf,
                specificFactions,
                turn: opponentTurn,
                activeSequences: ChessPolicy.Sequence.None,
                debug: false
            );

            if (IsEmpty(nextMoves)) break;

            // Add new threatened cells
            var newThreatened = nextMoves
                .Select(Col("dst_x").Alias("threatened_x"), Col("dst_y").Alias("threatened_y"));
            allThreatenedCells = allThreatenedCells.Union(newThreatened);

            // Continue only through empty squares
            emptyFrontier = nextMoves
                .Filter(Col("dst_generic_piece").BitwiseAND(Lit(emptyBit)).NotEqual(Lit(0)));
        }

        return allThreatenedCells.Distinct();
    }

    // =============== UTILITY METHODS ===============

    /// <summary>
    /// Creates perspectives from move frontier for continuation.
    /// </summary>
    private static DataFrame ComputeNextPerspectivesFromMoves(
        DataFrame movesDf,
        DataFrame perspectivesDf)
    {
        // Build new perspectives from destination positions
        var nextPerspectives = movesDf
            .Select(
                Col("dst_x").Alias("perspective_x"),
                Col("dst_y").Alias("perspective_y"),
                Col("original_perspective_x"),
                Col("original_perspective_y")
            )
            .Join(
                perspectivesDf.Select(
                    Col("x"),
                    Col("y"),
                    Col("piece"),
                    Col("generic_piece"),
                    Col("perspective_x").Alias("lookup_perspective_x"),
                    Col("perspective_y").Alias("lookup_perspective_y")
                ),
                (Col("perspective_x") == Col("x")).And(
                    Col("perspective_y") == Col("y")).And(
                    Col("original_perspective_x") == Col("lookup_perspective_x")).And(
                    Col("original_perspective_y") == Col("lookup_perspective_y")),
                "inner"
            );

        return nextPerspectives.Select(
            Col("perspective_x"),
            Col("perspective_y"),
            Col("piece"),
            Col("generic_piece"),
            Col("original_perspective_x"),
            Col("original_perspective_y")
        );
    }

    /// <summary>
    /// Creates empty threatened cells dataframe with proper schema.
    /// </summary>
    private static DataFrame CreateEmptyThreatenedCellsDf(DataFrame perspectivesDf)
    {
        return perspectivesDf.Limit(0)
            .Select(Lit(0).Alias("threatened_x"), Lit(0).Alias("threatened_y"))
            .Limit(0);
    }

    /// <summary>
    /// Checks if a dataframe is empty without materializing it.
    /// </summary>
    private static bool IsEmpty(DataFrame df)
    {
        try
        {
            return df.Limit(1).Count() == 0;
        }
        catch
        {
            return true;
        }
    }
}
