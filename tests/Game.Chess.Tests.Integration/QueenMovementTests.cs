using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for Queen piece movement rules.
/// Queen can move any number of squares horizontally, vertically, or diagonally (combines rook and bishop movement).
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Movement")]
[Trait("PieceType", "Queen")]
public class QueenMovementTests
{
    private readonly SparkSession _spark;
    private readonly ChessPolicy _policy;

    public QueenMovementTests(SparkFixture fixture)
    {
        _spark = fixture.Spark;
        _policy = new ChessPolicy(_spark);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void EmptyBoard_QueenInCenter_CanMoveToAdjacent8Directions()
    {
        // Arrange - Queen in center of empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Queen);

        // Assert - Queen can move to 8 adjacent squares (4 orthogonal + 4 diagonal)
        Assert.True(moves.Length >= 8, $"Expected at least 8 queen moves, got {moves.Length}");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void EmptyBoard_QueenInCenter_CanMoveToAdjacent8Directions_Refactored()
    {
        // Arrange - Queen in center of empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen);

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Queen);

        // Assert - Queen can move to 8 adjacent squares (4 orthogonal + 4 diagonal)
        Assert.True(moves.Length >= 8, $"Expected at least 8 queen moves, got {moves.Length}");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void WhiteQueenWithBlackPawnAdjacent_CanCapture_CaptureExists()
    {
        // Arrange - White Queen at (0,0), Black pawn at (0,1) adjacent
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 0, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen),
            (0, 1, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn));

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Queen);

        // Assert - Capture move to (0,1) should exist
        var captureMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 0 && r.GetAs<int>("dst_y") == 1);
        Assert.NotNull(captureMove);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void WhiteQueenWithBlackPawnAdjacent_CanCapture_CaptureExists_Refactored()
    {
        // Arrange - White Queen at (0,0), Black pawn at (0,1) adjacent
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 0, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen),
            (0, 1, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn));

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Queen);

        // Assert - Capture move to (0,1) should exist
        var captureMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 0 && r.GetAs<int>("dst_y") == 1);
        Assert.NotNull(captureMove);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void WhiteQueenWithWhitePawnAdjacent_CannotMoveOntoAlly_NoMoveToAlly()
    {
        // Arrange - White Queen at (0,0), White pawn at (1,1) adjacent diagonal
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 0, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen),
            (1, 1, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn));

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Queen);

        // Assert - No move should exist to allied piece at (1,1)
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 1 && r.GetAs<int>("dst_y") == 1);
        Assert.Null(invalidMove);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void WhiteQueenWithWhitePawnAdjacent_CannotMoveOntoAlly_NoMoveToAlly_Refactored()
    {
        // Arrange - White Queen at (0,0), White pawn at (1,1) adjacent diagonal
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 0, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Queen),
            (1, 1, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn));

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Queen);

        // Assert - No move should exist to allied piece at (1,1)
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 1 && r.GetAs<int>("dst_y") == 1);
        Assert.Null(invalidMove);
    }
}
