using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for Pawn piece movement rules.
/// Pawn can move forward one square, or two squares from starting position.
/// Pawns capture diagonally.
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Movement")]
[Trait("PieceType", "Pawn")]
public class PawnMovementTests
{
    private readonly SparkSession _spark;
    private readonly ChessPolicy _policy;

    public PawnMovementTests(SparkFixture fixture)
    {
        _spark = fixture.Spark;
        _policy = new ChessPolicy(_spark);
    }

    [Fact]
    
    public void WhitePawn_AtStartPosition_CanMoveForwardOneSquare()
    {
        // Arrange - White pawn at starting position (0, 1)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(0, 1, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Pawn);

        // Assert - Pawn can move to (0, 2)
        var forwardMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 0 && r.GetAs<int>("dst_y") == 2);
        Assert.NotNull(forwardMove);
    }

    [Fact]
    
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    [Trait("Essential", "True")]
    public void WhitePawn_AtStartPosition_CanMoveForwardOneSquare_Refactored()
    {
        // Arrange - White pawn at starting position (0, 1)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(0, 1, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Pawn);

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Pawn);

        // Assert - Pawn can move to (0, 2)
        var forwardMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 0 && r.GetAs<int>("dst_y") == 2);
        Assert.NotNull(forwardMove);
    }

    [Fact]
    
    public void WhitePawn_WithBlackPawnDiagonally_CanCapture()
    {
        // Arrange - White pawn at (3, 3), Black pawn at (4, 4)
        var board = BoardHelpers.CreateBoardWithPieces(
            (3, 3, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn),
            (4, 4, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn));

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Pawn);

        // Assert - Pawn can capture at (4, 4)
        var captureMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 4 && r.GetAs<int>("dst_y") == 4);
        Assert.NotNull(captureMove);
    }

    [Fact]
    
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void WhitePawn_WithBlackPawnDiagonally_CanCapture_Refactored()
    {
        // Arrange - White pawn at (3, 3), Black pawn at (4, 4)
        var board = BoardHelpers.CreateBoardWithPieces(
            (3, 3, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn),
            (4, 4, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn));

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Pawn);

        // Assert - Pawn can capture at (4, 4)
        var captureMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 4 && r.GetAs<int>("dst_y") == 4);
        Assert.NotNull(captureMove);
    }

    [Fact]
    
    public void WhitePawn_CannotMoveForwardIntoEnemy()
    {
        // Arrange - White pawn at (3, 3), Black pawn blocking at (3, 4)
        var board = BoardHelpers.CreateBoardWithPieces(
            (3, 3, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn),
            (3, 4, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn));

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Pawn);

        // Assert - No move to (3, 4) (pawn cannot capture forward)
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 3 && r.GetAs<int>("dst_y") == 4);
        Assert.Null(invalidMove);
    }

    [Fact]
    
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void WhitePawn_CannotMoveForwardIntoEnemy_Refactored()
    {
        // Arrange - White pawn at (3, 3), Black pawn blocking at (3, 4)
        var board = BoardHelpers.CreateBoardWithPieces(
            (3, 3, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn),
            (3, 4, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn));

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Pawn);

        // Assert - No move to (3, 4) (pawn cannot capture forward)
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 3 && r.GetAs<int>("dst_y") == 4);
        Assert.Null(invalidMove);
    }

    [Fact]
    
    public void WhitePawn_CannotCaptureFriendlyPiece()
    {
        // Arrange - White pawn at (3, 3), White pawn at (4, 4)
        var board = BoardHelpers.CreateBoardWithPieces(
            (3, 3, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn),
            (4, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn));

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Pawn);

        // Assert - No move to (4, 4) (cannot capture ally)
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 4 && r.GetAs<int>("dst_y") == 4);
        Assert.Null(invalidMove);
    }

    [Fact]
    
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void WhitePawn_CannotCaptureFriendlyPiece_Refactored()
    {
        // Arrange - White pawn at (3, 3), White pawn at (4, 4)
        var board = BoardHelpers.CreateBoardWithPieces(
            (3, 3, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn),
            (4, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn));

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Pawn);

        // Assert - No move to (4, 4) (cannot capture ally)
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 4 && r.GetAs<int>("dst_y") == 4);
        Assert.Null(invalidMove);
    }

    [Fact]
    
    public void WhitePawn_CanCaptureBothDiagonals()
    {
        // Arrange - White pawn at (3, 3), Black pawns at (2, 4) and (4, 4)
        var board = BoardHelpers.CreateBoardWithPieces(
            (3, 3, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn),
            (2, 4, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn),
            (4, 4, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn));

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Pawn);

        // Assert - Can capture both diagonal targets
        var captureLeft = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 2 && r.GetAs<int>("dst_y") == 4);
        var captureRight = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 4 && r.GetAs<int>("dst_y") == 4);
        Assert.NotNull(captureLeft);
        Assert.NotNull(captureRight);
    }

    [Fact]
    
    [Trait("Debug", "True")]
    [Trait("Refactored", "True")]
    public void WhitePawn_CanCaptureBothDiagonals_Refactored()
    {
        // Arrange - White pawn at (3, 3), Black pawns at (2, 4) and (4, 4)
        var board = BoardHelpers.CreateBoardWithPieces(
            (3, 3, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn),
            (2, 4, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn),
            (4, 4, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn));

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Pawn);

        // Assert - Can capture both diagonal targets
        var captureLeft = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 2 && r.GetAs<int>("dst_y") == 4);
        var captureRight = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 4 && r.GetAs<int>("dst_y") == 4);
        Assert.NotNull(captureLeft);
        Assert.NotNull(captureRight);
    }
}
