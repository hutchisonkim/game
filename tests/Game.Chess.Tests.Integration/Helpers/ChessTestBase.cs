using Microsoft.Spark.Sql;
using Game.Chess.HistoryRefactor;
using Game.Chess.Tests.Integration.Infrastructure;
using static Game.Chess.HistoryRefactor.ChessPolicyUtility;

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
    protected ChessPolicyRefactored Policy { get; }

    /// <summary>
    /// Initializes a new instance of the ChessTestBase class.
    /// </summary>
    /// <param name="fixture">The shared Spark fixture from xUnit collection</param>
    protected ChessTestBase(SparkFixture fixture)
    {
        Spark = fixture.Spark;
        Policy = new ChessPolicyRefactored(Spark);
    }

    /// <summary>
    /// Gets the default factions (White and Black) used in most tests.
    /// </summary>
    protected static readonly Piece[] DefaultFactions = [Piece.White, Piece.Black];

    /// <summary>
    /// Creates an empty 8x8 chess board.
    /// </summary>
    protected static Board CreateEmptyBoard() 
        => BoardHelpers.CreateEmptyBoard();

    /// <summary>
    /// Creates an empty board with a single piece at the specified position.
    /// </summary>
    protected static Board CreateEmptyBoardWithPiece(int x, int y, Piece piece)
        => BoardHelpers.CreateEmptyBoardWithPiece(x, y, piece);

    /// <summary>
    /// Creates a board with multiple pieces.
    /// </summary>
    protected static Board CreateBoardWithPieces(params (int X, int Y, Piece Piece)[] pieces)
        => BoardHelpers.CreateBoardWithPieces(pieces);
}
