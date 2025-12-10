using Microsoft.Spark.Sql;
using static Microsoft.Spark.Sql.Functions;
using Game.Chess.HistoryRefactor;
using Game.Chess.Policy.Patterns;
using static Game.Chess.HistoryRefactor.ChessPolicyUtility;

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
    /// This is simply: apply all patterns from opponent pieces to get cells they can attack.
    /// The patterns already encode all sliding distances (e.g., Bishop/Rook/Queen can reach up to 7 squares).
    /// No iteration needed - one pass of pattern matching gives us all threats.
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
        Piece[] specificFactions,
        int turn = 0,
        bool debug = false)
    {
        var opponentTurn = (turn + 1) % specificFactions.Length;

        // Step 1: Create threat patterns by removing dst_conditions (threats ignore empty/occupied)
        var threatPatternsDf = patternsDf
            .WithColumn("dst_conditions", Lit(0)); // Threats can target any square

        // Step 2: Compute threats using simple one-pass pattern matching
        // The patterns already encode sliding distances (Q1-Q4 for sliding pieces)
        // No iteration needed - one pass gets all possible attack destinations
        var allThreatsDf = PatternMatcher.MatchPatternsWithSequence(
            perspectivesDf,
            threatPatternsDf,
            specificFactions,
            turn: opponentTurn,
            activeSequences: ChessPolicyUtility.Sequence.None,
            debug: debug
        );

        // Step 3: Extract and deduplicate threatened cells
        var threatenedCellsDf = allThreatsDf
            .Select(Col("dst_x").Alias("threatened_x"), Col("dst_y").Alias("threatened_y"))
            .Distinct();

        if (debug)
        {
            System.Console.WriteLine($"[ThreatEngine] Computed threatened cells");
        }

        return threatenedCellsDf;
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
                Col("generic_piece").BitwiseOR(Lit((int)Piece.Threatened))
            )
            .Otherwise(Col("generic_piece"))
        );

        // Drop the join columns
        return updatedDf.Drop("threatened_x", "threatened_y");
    }
}
