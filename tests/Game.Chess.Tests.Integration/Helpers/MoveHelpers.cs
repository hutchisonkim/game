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
    /// Gets valid moves for a specific piece type by filtering patterns and computing candidates.
    /// Filters patterns to only include Public sequence moves.
    /// Overload for ChessPolicyRefactored.
    /// </summary>
    public static Row[] GetMovesForPieceType(
        SparkSession spark,
        ChessPolicyRefactored policy,
        ChessPolicy.Board board,
        ChessPolicy.Piece pieceType)
    {
        return GetMovesForPieceType(spark, policy, board, pieceType, DefaultFactions);
    }

    /// <summary>
    /// Gets valid moves for a specific piece type by filtering patterns and computing candidates.
    /// Filters patterns to only include Public sequence moves.
    /// Overload for ChessPolicyRefactored.
    /// </summary>
    public static Row[] GetMovesForPieceType(
        SparkSession spark,
        ChessPolicyRefactored policy,
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
    /// Gets valid moves for a specific piece type using patterns filtered by piece type only (no sequence filter).
    /// Used for pieces like King that don't have InstantRecursive patterns.
    /// Overload for ChessPolicyRefactored.
    /// </summary>
    public static Row[] GetMovesForPieceTypeNoSequenceFilter(
        SparkSession spark,
        ChessPolicyRefactored policy,
        ChessPolicy.Board board,
        ChessPolicy.Piece pieceType)
    {
        return GetMovesForPieceTypeNoSequenceFilter(spark, policy, board, pieceType, DefaultFactions);
    }

    /// <summary>
    /// Gets valid moves for a specific piece type using patterns filtered by piece type only (no sequence filter).
    /// Used for pieces like King that don't have InstantRecursive patterns.
    /// Overload for ChessPolicyRefactored.
    /// </summary>
    public static Row[] GetMovesForPieceTypeNoSequenceFilter(
        SparkSession spark,
        ChessPolicyRefactored policy,
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

    /// <summary>
    /// Gets all sequenced (sliding) moves for a specific piece type.
    /// Overload for ChessPolicyRefactored.
    /// </summary>
    public static Row[] GetSequencedMovesForPieceType(
        SparkSession spark,
        ChessPolicyRefactored policy,
        ChessPolicy.Board board,
        ChessPolicy.Piece pieceType,
        int maxDepth = 7)
    {
        return GetSequencedMovesForPieceType(spark, policy, board, pieceType, DefaultFactions, maxDepth);
    }

    /// <summary>
    /// Gets all sequenced (sliding) moves for a specific piece type.
    /// Overload for ChessPolicyRefactored.
    /// </summary>
    public static Row[] GetSequencedMovesForPieceType(
        SparkSession spark,
        ChessPolicyRefactored policy,
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

    /// <summary>
    /// Gets moves for a specific piece type with custom sequence flags.
    /// Provides more control over pattern filtering than GetMovesForPieceType.
    /// </summary>
    public static Row[] GetMovesFor(
        SparkSession spark,
        ChessPolicy policy,
        ChessPolicy.Board board,
        ChessPolicy.Piece pieceType,
        ChessPolicy.Sequence? sequenceFlags = null,
        ChessPolicy.Piece[]? factions = null)
    {
        factions ??= DefaultFactions;
        var perspectivesDf = policy.GetPerspectives(board, factions);
        
        var factory = new TestPatternFactory(spark);
        var patternsDf = sequenceFlags.HasValue 
            ? factory.GetPatternsFor(pieceType, sequenceFlags.Value)
            : factory.GetPatternsFor(pieceType);
        
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(
            perspectivesDf, patternsDf, factions);
        
        return candidates.Collect().ToArray();
    }

    /// <summary>
    /// Gets patterns and computes moves with active sequence support.
    /// Useful for testing sequence activation (e.g., sliding piece continuations).
    /// </summary>
    public static Row[] GetMovesWithActiveSequence(
        SparkSession spark,
        ChessPolicy policy,
        ChessPolicy.Board board,
        ChessPolicy.Piece pieceType,
        ChessPolicy.Sequence? patternSequenceFilter,
        ChessPolicy.Sequence activeSequences,
        ChessPolicy.Piece[]? factions = null)
    {
        factions ??= DefaultFactions;
        var perspectivesDf = policy.GetPerspectives(board, factions);
        
        var factory = new TestPatternFactory(spark);
        var patternsDf = patternSequenceFilter.HasValue
            ? factory.GetPatternsFor(pieceType, patternSequenceFilter.Value)
            : factory.GetPatternsFor(pieceType);
        
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(
            perspectivesDf, patternsDf, factions,
            turn: 0, activeSequences: activeSequences);
        
        return candidates.Collect().ToArray();
    }

    /// <summary>
    /// Computes candidates directly from pre-built perspectives and patterns DataFrames.
    /// Provides maximum flexibility for advanced test scenarios.
    /// </summary>
    public static Row[] ComputeCandidates(
        DataFrame perspectivesDf,
        DataFrame patternsDf,
        ChessPolicy.Piece[]? factions = null,
        int turn = 0,
        ChessPolicy.Sequence activeSequences = ChessPolicy.Sequence.None)
    {
        factions ??= DefaultFactions;
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(
            perspectivesDf, patternsDf, factions, turn, activeSequences);
        
        return candidates.Collect().ToArray();
    }
}
