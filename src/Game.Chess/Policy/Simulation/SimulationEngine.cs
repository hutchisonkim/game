using Microsoft.Spark.Sql;
using static Microsoft.Spark.Sql.Functions;

namespace Game.Chess.Policy.Simulation;

/// <summary>
/// Stateless simulation engine for computing board states after moves.
/// This is the core abstraction for forward timeline building.
/// </summary>
public class SimulationEngine
{
    /// <summary>
    /// Simulates the board state after applying a move by creating new perspectives.
    /// 
    /// The method:
    /// 1. Takes the current perspectives and a candidate move
    /// 2. Creates new perspectives where:
    ///    - The source cell becomes empty
    ///    - The destination cell contains the moved piece
    ///    - All other cells remain unchanged
    /// 
    /// This elegant pattern-driven approach enables:
    /// - Timeline building (simulating game tree forward)
    /// - Check detection (simulating if king would be threatened after move)
    /// - Sliding piece computation (iteratively stepping through empty squares)
    /// </summary>
    public DataFrame SimulateBoardAfterMove(
        DataFrame perspectivesDf,
        DataFrame candidatesDf,
        Piece[] specificFactions)
    {
        if (!candidatesDf.Limit(1).Collect().Any())
        {
            return perspectivesDf;
        }

        // Get the move details - we need src_x, src_y, dst_x, dst_y, and the piece info
        // Rename columns to avoid ambiguity when joining with perspectivesDf
        var moveDf = candidatesDf.Select(
            Col("src_x"),
            Col("src_y"),
            Col("src_piece"),
            Col("src_generic_piece"),
            Col("dst_x"),
            Col("dst_y"),
            Col("perspective_x").Alias("move_perspective_x"),
            Col("perspective_y").Alias("move_perspective_y"),
            Col("perspective_piece").Alias("move_perspective_piece")
        ).Distinct();

        // Join perspectives with moves to identify which cells change
        var joinedDf = perspectivesDf.Join(
            moveDf,
            Col("perspective_x").EqualTo(Col("move_perspective_x")).And(
            Col("perspective_y").EqualTo(Col("move_perspective_y"))),
            "left_outer"
        );

        // Apply transformations:
        // - If cell is at src position: mark as empty
        // - If cell is at dst position: place the moved piece
        // - Otherwise: keep unchanged
        var emptyBit = (int)Piece.Empty;

        var newPerspectivesDf = joinedDf.WithColumn(
            "new_piece",
            When(
                Col("x").EqualTo(Col("src_x")).And(Col("y").EqualTo(Col("src_y"))),
                Lit(emptyBit)  // Source becomes empty
            )
            .When(
                Col("x").EqualTo(Col("dst_x")).And(Col("y").EqualTo(Col("dst_y"))),
                Col("src_piece")  // Destination gets the moved piece
            )
            .Otherwise(Col("piece"))  // Other cells unchanged
        );

        // Recompute generic_piece based on new piece and perspective
        var pieceHasFaction = specificFactions
            .Select(f => Col("new_piece").BitwiseAND(Lit((int)f)).NotEqual(Lit(0)))
            .Aggregate((acc, cond) => acc.Or(cond));

        var pieceAndPerspectiveShareFaction = specificFactions
            .Select(f =>
                Col("new_piece").BitwiseAND(Lit((int)f)).NotEqual(Lit(0))
                .And(Col("perspective_piece").BitwiseAND(Lit((int)f)).NotEqual(Lit(0)))
            )
            .Aggregate((acc, cond) => acc.Or(cond));

        Column newGenericPieceCol =
            When(
                (Col("x") == Col("perspective_x")) &
                (Col("y") == Col("perspective_y")),
                Col("new_piece").BitwiseOR(Lit((int)Piece.Self))
            )
            .When(
                pieceAndPerspectiveShareFaction,
                Col("new_piece").BitwiseOR(Lit((int)Piece.Ally))
            )
            .When(
                pieceHasFaction &
                Not(pieceAndPerspectiveShareFaction),
                Col("new_piece").BitwiseOR(Lit((int)Piece.Foe))
            )
            .Otherwise(Col("new_piece"));

        var finalDf = newPerspectivesDf
            .WithColumn("piece", Col("new_piece"))
            .WithColumn("generic_piece", newGenericPieceCol)
            .Drop("new_piece", "src_x", "src_y", "src_piece", "src_generic_piece", "dst_x", "dst_y", 
                  "move_perspective_x", "move_perspective_y", "move_perspective_piece");

        // Remove any duplicate columns from the join
        var columns = perspectivesDf.Columns();
        return finalDf.Select(columns.Select(c => Col(c)).ToArray());
    }
}
