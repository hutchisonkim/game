using Game.Chess.HistoryB;
using Microsoft.Spark.Sql;
using Microsoft.Spark.Sql.Types;

namespace Game.Chess.Tests.Integration.Helpers;

/// <summary>
/// Helper methods for computing and validating chess moves.
/// </summary>
public static class MoveHelpers
{
    /// <summary>
    /// Default factions used in most tests (White and Black).
    /// </summary>
    public static readonly ChessPolicy.Piece[] DefaultFactions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };

    /// <summary>
    /// Gets valid moves for a specific piece type by filtering patterns and computing candidates.
    /// Filters patterns to only include Public sequence moves.
    /// </summary>
    public static Row[] GetMovesForPieceType(
        SparkSession spark,
        ChessPolicy policy,
        ChessPolicy.Board board,
        ChessPolicy.Piece pieceType)
    {
        return GetMovesForPieceType(spark, policy, board, pieceType, DefaultFactions);
    }

    /// <summary>
    /// Gets valid moves for a specific piece type by filtering patterns and computing candidates.
    /// Filters patterns to only include Public sequence moves.
    /// </summary>
    public static Row[] GetMovesForPieceType(
        SparkSession spark,
        ChessPolicy policy,
        ChessPolicy.Board board,
        ChessPolicy.Piece pieceType,
        ChessPolicy.Piece[] factions)
    {
        var perspectivesDf = policy.GetPerspectives(board, factions);
        int pieceTypeInt = (int)pieceType;
        int publicSeq = (int)ChessPolicy.Sequence.Public;
        var patternsDf = new ChessPolicy.PatternFactory(spark).GetPatterns()
            .Filter($"(src_conditions & {pieceTypeInt}) != 0 AND (sequence & {publicSeq}) != 0");
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(perspectivesDf, patternsDf, factions);
        return candidates.Collect().ToArray();
    }

    /// <summary>
    /// Gets valid moves for a specific piece type using patterns filtered by piece type only (no sequence filter).
    /// Used for pieces like King that don't have InstantRecursive patterns.
    /// </summary>
    public static Row[] GetMovesForPieceTypeNoSequenceFilter(
        SparkSession spark,
        ChessPolicy policy,
        ChessPolicy.Board board,
        ChessPolicy.Piece pieceType)
    {
        return GetMovesForPieceTypeNoSequenceFilter(spark, policy, board, pieceType, DefaultFactions);
    }

    /// <summary>
    /// Gets valid moves for a specific piece type using patterns filtered by piece type only (no sequence filter).
    /// Used for pieces like King that don't have InstantRecursive patterns.
    /// </summary>
    public static Row[] GetMovesForPieceTypeNoSequenceFilter(
        SparkSession spark,
        ChessPolicy policy,
        ChessPolicy.Board board,
        ChessPolicy.Piece pieceType,
        ChessPolicy.Piece[] factions)
    {
        var perspectivesDf = policy.GetPerspectives(board, factions);
        int pieceTypeInt = (int)pieceType;
        var patternsDf = new ChessPolicy.PatternFactory(spark).GetPatterns()
            .Filter($"(src_conditions & {pieceTypeInt}) != 0");
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(perspectivesDf, patternsDf, factions);
        return candidates.Collect().ToArray();
    }

    /// <summary>
    /// Gets all sequenced (sliding) moves for a specific piece type.
    /// </summary>
    public static Row[] GetSequencedMovesForPieceType(
        SparkSession spark,
        ChessPolicy policy,
        ChessPolicy.Board board,
        ChessPolicy.Piece pieceType,
        int maxDepth = 7)
    {
        return GetSequencedMovesForPieceType(spark, policy, board, pieceType, DefaultFactions, maxDepth);
    }

    /// <summary>
    /// Gets all sequenced (sliding) moves for a specific piece type.
    /// </summary>
    public static Row[] GetSequencedMovesForPieceType(
        SparkSession spark,
        ChessPolicy policy,
        ChessPolicy.Board board,
        ChessPolicy.Piece pieceType,
        ChessPolicy.Piece[] factions,
        int maxDepth = 7)
    {
        var perspectivesDf = policy.GetPerspectives(board, factions);
        var patternsDf = new ChessPolicy.PatternFactory(spark).GetPatterns();
        var moves = ChessPolicy.TimelineService.ComputeSequencedMoves(
            perspectivesDf,
            patternsDf,
            factions,
            turn: 0,
            maxDepth: maxDepth);
        return moves.Collect().ToArray();
    }
}
