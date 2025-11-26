using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;

namespace Game.Chess.Tests.Integration.Helpers;

/// <summary>
/// High-level wrapper around ChessPolicy.PatternFactory for common test scenarios.
/// Provides simplified methods to retrieve and filter patterns.
/// </summary>
public class TestPatternFactory
{
    private readonly SparkSession _spark;
    private readonly ChessPolicy.PatternFactory _factory;

    public TestPatternFactory(SparkSession spark)
    {
        _spark = spark;
        _factory = new ChessPolicy.PatternFactory(spark);
    }

    /// <summary>
    /// Gets all patterns from the pattern factory.
    /// </summary>
    public DataFrame GetAllPatterns()
    {
        return _factory.GetPatterns();
    }

    /// <summary>
    /// Gets patterns for a specific piece type with optional sequence filtering.
    /// </summary>
    /// <param name="pieceType">The piece type to filter for</param>
    /// <param name="sequenceFlags">Optional sequence flags to filter (null for no sequence filter)</param>
    public DataFrame GetPatternsFor(ChessPolicy.Piece pieceType, ChessPolicy.Sequence? sequenceFlags = null)
    {
        var patterns = _factory.GetPatterns();
        
        if (sequenceFlags.HasValue)
        {
            return FilterHelpers.CreateFilter()
                .ForPieceType(pieceType)
                .WithSequence(sequenceFlags.Value)
                .ApplyTo(patterns);
        }
        
        return FilterHelpers.CreateFilter()
            .ForPieceType(pieceType)
            .ApplyTo(patterns);
    }

    /// <summary>
    /// Gets patterns for a specific piece type with Public sequence flag.
    /// This is the most common pattern used in basic movement tests.
    /// </summary>
    public DataFrame GetPublicPatternsFor(ChessPolicy.Piece pieceType)
    {
        return GetPatternsFor(pieceType, ChessPolicy.Sequence.Public);
    }

    /// <summary>
    /// Gets entry patterns for sliding pieces (Out flag, InstantRecursive, no In flags).
    /// Used for pieces like Rook, Bishop, and Queen that can slide multiple squares.
    /// </summary>
    /// <param name="pieceType">The sliding piece type</param>
    /// <param name="outFlag">The specific Out flag (e.g., OutI for Rook, OutF for Bishop)</param>
    public DataFrame GetEntryPatterns(ChessPolicy.Piece pieceType, ChessPolicy.Sequence outFlag)
    {
        return _factory.GetPatterns().ForEntryPatterns(pieceType, outFlag);
    }

    /// <summary>
    /// Gets continuation patterns for sliding pieces (In flag, Public).
    /// These patterns continue the movement sequence after entry.
    /// </summary>
    /// <param name="pieceType">The sliding piece type</param>
    /// <param name="inFlag">The specific In flag (e.g., InI for Rook, InF for Bishop)</param>
    public DataFrame GetContinuationPatterns(ChessPolicy.Piece pieceType, ChessPolicy.Sequence inFlag)
    {
        return _factory.GetPatterns().ForContinuationPatterns(pieceType, inFlag);
    }

    /// <summary>
    /// Gets patterns with both piece type and multiple sequence flags.
    /// Useful for complex pattern queries.
    /// </summary>
    public DataFrame GetPatternsWithFlags(
        ChessPolicy.Piece pieceType,
        ChessPolicy.Sequence requiredFlags,
        ChessPolicy.Sequence? forbiddenFlags = null)
    {
        var builder = FilterHelpers.CreateFilter()
            .ForPieceType(pieceType)
            .WithSequence(requiredFlags);

        if (forbiddenFlags.HasValue)
        {
            builder.WithoutSequence(forbiddenFlags.Value);
        }

        return builder.ApplyTo(_factory.GetPatterns());
    }

    /// <summary>
    /// Gets patterns for a specific piece type with exact sequence matching.
    /// </summary>
    public DataFrame GetPatternsWithExactSequence(
        ChessPolicy.Piece pieceType,
        ChessPolicy.Sequence mask,
        ChessPolicy.Sequence expectedValue)
    {
        return FilterHelpers.CreateFilter()
            .ForPieceType(pieceType)
            .WithSequenceExact(mask, expectedValue)
            .ApplyTo(_factory.GetPatterns());
    }
}
