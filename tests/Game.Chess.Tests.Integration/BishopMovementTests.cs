using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for Bishop piece movement rules.
/// Bishop can move any number of squares diagonally.
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Movement")]
[Trait("PieceType", "Bishop")]
public class BishopMovementTests
{
    private readonly SparkSession _spark;
    private readonly ChessPolicy _policy;

    public BishopMovementTests(SparkFixture fixture)
    {
        _spark = fixture.Spark;
        _policy = new ChessPolicy(_spark);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void EmptyBoard_BishopInCenter_CanMoveToAdjacentDiagonals()
    {
        // Arrange - Bishop in center of empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Bishop);

        // Assert - At minimum, bishop can move to 4 adjacent diagonal squares
        Assert.True(moves.Length >= 4, $"Expected at least 4 bishop moves, got {moves.Length}");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    [Trait("Essential", "True")]
    public void EmptyBoard_BishopInCenter_CanMoveToAdjacentDiagonals_Refactored()
    {
        // Arrange - Bishop in center of empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop);
        var refactoredPolicy = new ChessPolicyRefactored(_spark);
        var factions = new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black };

        // Act - Get perspectives and compute moves using refactored policy
        var perspectivesDf = refactoredPolicy.GetPerspectives(board, factions);
        int bishopType = (int)ChessPolicy.Piece.Bishop;
        int publicSeq = (int)ChessPolicy.Sequence.Public;
        var patternsDf = new ChessPolicy.PatternFactory(_spark).GetPatterns()
            .Filter($"(src_conditions & {bishopType}) != 0 AND (sequence & {publicSeq}) != 0");
        var candidates = ChessPolicy.TimelineService.ComputeNextCandidates(perspectivesDf, patternsDf, factions);
        var moves = candidates.Collect().ToArray();

        // Assert - At minimum, bishop can move to 4 adjacent diagonal squares
        Assert.True(moves.Length >= 4, $"Expected at least 4 bishop moves, got {moves.Length}");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void WhiteBishopWithBlackPawnAdjacent_CanCapture_CaptureExists()
    {
        // Arrange - White Bishop at (2,2), Black pawn at (3,3) adjacent diagonal
        var board = BoardHelpers.CreateBoardWithPieces(
            (2, 2, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop),
            (3, 3, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn));

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Bishop);

        // Assert - Capture move to (3,3) should exist
        var captureMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 3 && r.GetAs<int>("dst_y") == 3);
        Assert.NotNull(captureMove);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void WhiteBishopWithBlackPawnAdjacent_CanCapture_CaptureExists_Refactored()
    {
        // Arrange - White Bishop at (2,2), Black pawn at (3,3) adjacent diagonal
        var board = BoardHelpers.CreateBoardWithPieces(
            (2, 2, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop),
            (3, 3, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn));
        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Bishop);

        // Assert - Capture move to (3,3) should exist
        var captureMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 3 && r.GetAs<int>("dst_y") == 3);
        Assert.NotNull(captureMove);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void WhiteBishopWithWhitePawnAdjacent_CannotMoveOntoAlly_NoMoveToAlly()
    {
        // Arrange - White Bishop at (2,2), White pawn at (3,3) adjacent diagonal
        var board = BoardHelpers.CreateBoardWithPieces(
            (2, 2, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop),
            (3, 3, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn));

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Bishop);

        // Assert - No move should exist to allied piece at (3,3)
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 3 && r.GetAs<int>("dst_y") == 3);
        Assert.Null(invalidMove);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void WhiteBishopWithWhitePawnAdjacent_CannotMoveOntoAlly_NoMoveToAlly_Refactored()
    {
        // Arrange - White Bishop at (2,2), White pawn at (3,3) adjacent diagonal
        var board = BoardHelpers.CreateBoardWithPieces(
            (2, 2, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Bishop),
            (3, 3, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn));
        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Bishop);

        // Assert - No move should exist to allied piece at (3,3)
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 3 && r.GetAs<int>("dst_y") == 3);
        Assert.Null(invalidMove);
    }
}
