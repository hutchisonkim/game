using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;

namespace Game.Chess.Tests.Integration.Helpers;

/// <summary>
/// Base class for chess integration tests.
/// Provides common setup and initialization following xUnit collection fixture pattern.
/// Follows MSDN test guidelines for shared test context.
/// </summary>
public abstract class ChessTestBase
{
    /// <summary>
    /// The shared SparkSession instance for all tests in the collection.
    /// </summary>
    protected SparkSession Spark { get; }

    /// <summary>
    /// The ChessPolicy instance for the current test.
    /// Each test gets its own policy instance to avoid cross-test contamination.
    /// </summary>
    protected ChessPolicy Policy { get; }

    /// <summary>
    /// Helper for building type-safe DataFrame filters.
    /// </summary>
    protected TestPatternFactory PatternFactory { get; }

    /// <summary>
    /// Initializes a new instance of the ChessTestBase class.
    /// </summary>
    /// <param name="fixture">The shared Spark fixture from xUnit collection</param>
    protected ChessTestBase(SparkFixture fixture)
    {
        Spark = fixture.Spark;
        Policy = new ChessPolicy(Spark);
        PatternFactory = new TestPatternFactory(Spark);
    }

    /// <summary>
    /// Gets the default factions (White and Black) used in most tests.
    /// </summary>
    protected static ChessPolicy.Piece[] DefaultFactions => MoveHelpers.DefaultFactions;

    /// <summary>
    /// Creates an empty 8x8 chess board.
    /// </summary>
    protected static ChessPolicy.Board CreateEmptyBoard() 
        => BoardHelpers.CreateEmptyBoard();

    /// <summary>
    /// Creates an empty board with a single piece at the specified position.
    /// </summary>
    protected static ChessPolicy.Board CreateEmptyBoardWithPiece(int x, int y, ChessPolicy.Piece piece)
        => BoardHelpers.CreateEmptyBoardWithPiece(x, y, piece);

    /// <summary>
    /// Creates a board with multiple pieces.
    /// </summary>
    protected static ChessPolicy.Board CreateBoardWithPieces(params (int X, int Y, ChessPolicy.Piece Piece)[] pieces)
        => BoardHelpers.CreateBoardWithPieces(pieces);

    /// <summary>
    /// Gets moves for a piece type with optional sequence filtering.
    /// </summary>
    protected Row[] GetMovesFor(
        ChessPolicy.Board board,
        ChessPolicy.Piece pieceType,
        ChessPolicy.Sequence? sequenceFlags = null,
        ChessPolicy.Piece[]? factions = null)
    {
        return MoveHelpers.GetMovesFor(Spark, Policy, board, pieceType, sequenceFlags, factions);
    }

    /// <summary>
    /// Gets moves with active sequence support for testing sequence activation.
    /// </summary>
    protected Row[] GetMovesWithActiveSequence(
        ChessPolicy.Board board,
        ChessPolicy.Piece pieceType,
        ChessPolicy.Sequence? patternSequenceFilter,
        ChessPolicy.Sequence activeSequences,
        ChessPolicy.Piece[]? factions = null)
    {
        return MoveHelpers.GetMovesWithActiveSequence(
            Spark, Policy, board, pieceType, patternSequenceFilter, activeSequences, factions);
    }

    /// <summary>
    /// Gets patterns for a specific piece type with optional sequence filtering.
    /// </summary>
    protected DataFrame GetPatternsFor(ChessPolicy.Piece pieceType, ChessPolicy.Sequence? sequenceFlags = null)
    {
        return PatternFactory.GetPatternsFor(pieceType, sequenceFlags);
    }

    /// <summary>
    /// Gets public patterns for a specific piece type.
    /// </summary>
    protected DataFrame GetPublicPatternsFor(ChessPolicy.Piece pieceType)
    {
        return PatternFactory.GetPublicPatternsFor(pieceType);
    }

    /// <summary>
    /// Gets entry patterns for sliding pieces.
    /// </summary>
    protected DataFrame GetEntryPatterns(ChessPolicy.Piece pieceType, ChessPolicy.Sequence outFlag)
    {
        return PatternFactory.GetEntryPatterns(pieceType, outFlag);
    }

    /// <summary>
    /// Gets continuation patterns for sliding pieces.
    /// </summary>
    protected DataFrame GetContinuationPatterns(ChessPolicy.Piece pieceType, ChessPolicy.Sequence inFlag)
    {
        return PatternFactory.GetContinuationPatterns(pieceType, inFlag);
    }
}
