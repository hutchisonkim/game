using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for Knight piece movement rules.
/// Knight moves in an L-shape: 2 squares in one direction and 1 square perpendicular.
/// Knight can jump over other pieces.
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Movement")]
[Trait("PieceType", "Knight")]
public class KnightMovementTests
{
    private readonly SparkSession _spark;
    private readonly ChessPolicy _policy;

    public KnightMovementTests(SparkFixture fixture)
    {
        _spark = fixture.Spark;
        _policy = new ChessPolicy(_spark);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void EmptyBoard_KnightInCenter_Has8Moves()
    {
        // Arrange - Knight in center of empty 8x8 board at (4, 4)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Knight);

        // Assert - Knight can move to all 8 L-shaped positions
        Assert.Equal(8, moves.Length);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    [Trait("Essential", "True")]
    public void EmptyBoard_KnightInCenter_Has8Moves_Refactored()
    {
        // Arrange - Knight in center of empty 8x8 board at (4, 4)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight);

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Knight);

        // Assert - Knight can move to all 8 L-shaped positions
        Assert.Equal(8, moves.Length);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void EmptyBoard_KnightAtCorner_Has2Moves()
    {
        // Arrange - Knight at corner (0, 0)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(0, 0, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Knight);

        // Assert - Knight can only move to 2 positions from corner
        Assert.Equal(2, moves.Length);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void EmptyBoard_KnightAtCorner_Has2Moves_Refactored()
    {
        // Arrange - Knight at corner (0, 0)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(0, 0, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight);

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Knight);

        // Assert - Knight can only move to 2 positions from corner
        Assert.Equal(2, moves.Length);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void EmptyBoard_KnightAtEdge_Has4Moves()
    {
        // Arrange - Knight at edge (0, 4)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(0, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Knight);

        // Assert - Knight can move to 4 positions from edge
        Assert.Equal(4, moves.Length);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void EmptyBoard_KnightAtEdge_Has4Moves_Refactored()
    {
        // Arrange - Knight at edge (0, 4)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(0, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight);

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Knight);

        // Assert - Knight can move to 4 positions from edge
        Assert.Equal(4, moves.Length);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void WhiteKnightWithBlackPieceAtLShape_CanCapture()
    {
        // Arrange - White Knight at (4, 4), Black pawn at (6, 5) L-shape position
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight),
            (6, 5, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn));

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Knight);

        // Assert - Knight can capture at (6, 5)
        var captureMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 6 && r.GetAs<int>("dst_y") == 5);
        Assert.NotNull(captureMove);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void WhiteKnightWithBlackPieceAtLShape_CanCapture_Refactored()
    {
        // Arrange - White Knight at (4, 4), Black pawn at (6, 5) L-shape position
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight),
            (6, 5, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn));

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Knight);

        // Assert - Knight can capture at (6, 5)
        var captureMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 6 && r.GetAs<int>("dst_y") == 5);
        Assert.NotNull(captureMove);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void WhiteKnightWithWhitePieceAtLShape_CannotCaptureAlly()
    {
        // Arrange - White Knight at (4, 4), White pawn at (6, 5) L-shape position
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight),
            (6, 5, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn));

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Knight);

        // Assert - Knight cannot capture ally at (6, 5), so only 7 moves
        Assert.Equal(7, moves.Length);
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 6 && r.GetAs<int>("dst_y") == 5);
        Assert.Null(invalidMove);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void WhiteKnightWithWhitePieceAtLShape_CannotCaptureAlly_Refactored()
    {
        // Arrange - White Knight at (4, 4), White pawn at (6, 5) L-shape position
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight),
            (6, 5, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn));

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Knight);

        // Assert - Knight cannot capture ally at (6, 5), so only 7 moves
        Assert.Equal(7, moves.Length);
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 6 && r.GetAs<int>("dst_y") == 5);
        Assert.Null(invalidMove);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void Knight_CanJumpOverPieces()
    {
        // Arrange - Knight at (4, 4), surrounded by pieces but can still jump
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight),
            (3, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn),
            (5, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn),
            (4, 3, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn),
            (4, 5, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn));

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Knight);

        // Assert - Knight can still move to all 8 L-positions (can jump over adjacent pieces)
        Assert.Equal(8, moves.Length);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void Knight_CanJumpOverPieces_Refactored()
    {
        // Arrange - Knight at (4, 4), surrounded by pieces but can still jump
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight),
            (3, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn),
            (5, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn),
            (4, 3, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn),
            (4, 5, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn));

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Knight);

        // Assert - Knight can still move to all 8 L-positions (can jump over adjacent pieces)
        Assert.Equal(8, moves.Length);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void Knight_MovesToAllLShapePositions()
    {
        // Arrange - Knight at (4, 4)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Knight);

        // Assert - Knight can move to all 8 L-shaped destinations
        var expectedMoves = new[] { (6, 5), (6, 3), (2, 5), (2, 3), (5, 6), (5, 2), (3, 6), (3, 2) };
        foreach (var (x, y) in expectedMoves)
        {
            var move = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == x && r.GetAs<int>("dst_y") == y);
            Assert.NotNull(move);
        }
    }

    [Fact]
    [Trait("Performance", "Fast")]
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void Knight_MovesToAllLShapePositions_Refactored()
    {
        // Arrange - Knight at (4, 4)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Knight);

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Knight);

        // Assert - Knight can move to all 8 L-shaped destinations
        var expectedMoves = new[] { (6, 5), (6, 3), (2, 5), (2, 3), (5, 6), (5, 2), (3, 6), (3, 2) };
        foreach (var (x, y) in expectedMoves)
        {
            var move = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == x && r.GetAs<int>("dst_y") == y);
            Assert.NotNull(move);
        }
    }
}
