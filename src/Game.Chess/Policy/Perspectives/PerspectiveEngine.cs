using Microsoft.Spark.Sql;
using static Microsoft.Spark.Sql.Functions;
using static Game.Chess.HistoryRefactor.ChessPolicyUtility;

namespace Game.Chess.Policy.Perspectives;

/// <summary>
/// Pure perspective engine that computes Self/Ally/Foe relationships for all board positions.
/// This layer has no notion of legality, threats, or patterns - it only establishes
/// the relational context between pieces from each actor's viewpoint.
/// </summary>
public class PerspectiveEngine
{
    /// <summary>
    /// Builds perspectives for the given pieces DataFrame and factions.
    /// Each actor (piece with a faction) sees the board from their perspective,
    /// with cells marked as Self, Ally, or Foe based on faction relationships.
    /// </summary>
    public DataFrame BuildPerspectives(DataFrame piecesDf, Piece[] specificFactions)
    {
        // 1. Filter to only those pieces that actually have a faction.
        // These are the "actors" who generate perspectives.
        var actorDf = piecesDf.Filter(
            specificFactions
                .Select(f => Col("piece").BitwiseAND((int)f).NotEqual(Lit(0)))
                .Aggregate((acc, cond) => acc.Or(cond))
        );

        // Rename for perspective origin (actor)
        actorDf = actorDf
            .WithColumnRenamed("x", "perspective_x")
            .WithColumnRenamed("y", "perspective_y")
            .WithColumnRenamed("piece", "perspective_piece");

        // Cross join: actor perspective × full board state
        var perspectivesDf = actorDf.CrossJoin(piecesDf);

        // 2. Build Self / Ally / Foe logic without removing faction bits
        // pieceHasFaction (dest piece has any listed faction)
        var pieceHasFaction = specificFactions
            .Select(f => Col("piece").BitwiseAND((int)f).NotEqual(Lit(0)))
            .Aggregate((acc, cond) => acc.Or(cond));

        // piece and perspective share the same faction
        var pieceAndPerspectiveShareFaction = specificFactions
            .Select(f =>
                Col("piece").BitwiseAND((int)f).NotEqual(Lit(0))
                .And(Col("perspective_piece").BitwiseAND((int)f).NotEqual(Lit(0)))
            )
            .Aggregate((acc, cond) => acc.Or(cond));

        // 3. generic_piece preserves all original bits (faction + type)
        // and simply ORs in Self/Ally/Foe relationship bits.
        Column genericPieceCol =
            When(
                (Col("x") == Col("perspective_x")) &
                (Col("y") == Col("perspective_y")),
                Col("piece").BitwiseOR(Lit((int)Piece.Self))
            )
            .When(
                pieceAndPerspectiveShareFaction,
                Col("piece").BitwiseOR(Lit((int)Piece.Ally))
            )
            .When(
                pieceHasFaction &
                Not(pieceAndPerspectiveShareFaction),
                Col("piece").BitwiseOR(Lit((int)Piece.Foe))
            )
            .Otherwise(Col("piece")); // empty or no-faction stays unchanged

        return perspectivesDf.WithColumn("generic_piece", genericPieceCol);
    }

    /// <summary>
    /// Adds perspective_id to the perspectives DataFrame for identity tracking.
    /// </summary>
    public DataFrame AddPerspectiveId(DataFrame perspectivesDf)
    {
        return perspectivesDf.WithColumn("perspective_id",
            Sha2(ConcatWs("_",
                Col("x").Cast("string"),
                Col("y").Cast("string"),
                Col("generic_piece").Cast("string")),
            256));
    }

    /// <summary>
    /// Applies the threatened bit to cells in the perspectives that are under attack.
    /// </summary>
    public DataFrame ApplyThreatMask(DataFrame perspectivesDf, DataFrame threatenedCellsDf)
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
            ).Otherwise(Col("generic_piece"))
        );

        // Drop the join columns
        return updatedDf.Drop("threatened_x", "threatened_y");
    }

    /// <summary>
    /// Recomputes generic_piece relationships for a new piece arrangement.
    /// Used after simulating moves to update Self/Ally/Foe relationships.
    /// </summary>
    public DataFrame RecomputeGenericPiece(DataFrame perspectivesDf, Piece[] specificFactions)
    {
        var pieceHasFaction = specificFactions
            .Select(f => Col("piece").BitwiseAND((int)f).NotEqual(Lit(0)))
            .Aggregate((acc, cond) => acc.Or(cond));

        var pieceAndPerspectiveShareFaction = specificFactions
            .Select(f =>
                Col("piece").BitwiseAND((int)f).NotEqual(Lit(0))
                .And(Col("perspective_piece").BitwiseAND((int)f).NotEqual(Lit(0)))
            )
            .Aggregate((acc, cond) => acc.Or(cond));

        Column newGenericPieceCol =
            When(
                (Col("x") == Col("perspective_x")) &
                (Col("y") == Col("perspective_y")),
                Col("piece").BitwiseOR(Lit((int)Piece.Self))
            )
            .When(
                pieceAndPerspectiveShareFaction,
                Col("piece").BitwiseOR(Lit((int)Piece.Ally))
            )
            .When(
                pieceHasFaction &
                Not(pieceAndPerspectiveShareFaction),
                Col("piece").BitwiseOR(Lit((int)Piece.Foe))
            )
            .Otherwise(Col("piece"));

        return perspectivesDf.WithColumn("generic_piece", newGenericPieceCol);
    }
}
