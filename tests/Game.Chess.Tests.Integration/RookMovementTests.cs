using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for Rook piece movement rules.
/// Rook can move any number of squares horizontally or vertically (orthogonal movement).
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Movement")]
[Trait("PieceType", "Rook")]
public class RookMovementTests
{
    private readonly SparkSession _spark;
    private readonly ChessPolicy _policy;

    public RookMovementTests(SparkFixture fixture)
    {
        _spark = fixture.Spark;
        _policy = new ChessPolicy(_spark);
    }

    [Fact]
    
    public void EmptyBoard_RookInCenter_CanMoveToAdjacentSquares()
    {
        // Arrange - Rook in center of empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Rook);

        // Assert - At minimum, rook can move to 4 adjacent squares (up, down, left, right)
        Assert.True(moves.Length >= 4, $"Expected at least 4 rook moves, got {moves.Length}");
    }

    [Fact]
    
    
    [Trait("Refactored", "True")]
    [Trait("Phase", "2A")]
    public void EmptyBoard_RookInCenter_CanMoveToAdjacentSquares_Refactored()
    {
        // Arrange - Rook in center of empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook);

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Rook);

        // Assert - At minimum, rook can move to 4 adjacent squares (up, down, left, right)
        Assert.True(moves.Length >= 4, $"Expected at least 4 rook moves, got {moves.Length}");
    }

    [Fact]
    
    public void WhiteRookWithBlackPawnAdjacent_CanCapture_CaptureExists()
    {
        // Arrange - White Rook at (0,0), Black pawn at (1,0) adjacent
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 0, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook),
            (1, 0, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn));

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Rook);

        // Assert - Capture move to (1,0) should exist
        var captureMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 1 && r.GetAs<int>("dst_y") == 0);
        Assert.NotNull(captureMove);
    }

    [Fact]
    
    
    [Trait("Refactored", "True")]
    public void WhiteRookWithBlackPawnAdjacent_CanCapture_CaptureExists_Refactored()
    {
        // Arrange - White Rook at (0,0), Black pawn at (1,0) adjacent
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 0, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook),
            (1, 0, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn));

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Rook);

        // Assert - Capture move to (1,0) should exist
        var captureMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 1 && r.GetAs<int>("dst_y") == 0);
        Assert.NotNull(captureMove);
    }

    [Fact]
    
    public void WhiteRookWithWhitePawnAdjacent_CannotMoveOntoAlly_NoMoveToAlly()
    {
        // Arrange - White Rook at (0,0), White pawn at (0,1) adjacent
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 0, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook),
            (0, 1, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn));

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Rook);

        // Assert - No move should exist to allied piece at (0,1)
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 0 && r.GetAs<int>("dst_y") == 1);
        Assert.Null(invalidMove);
    }

    [Fact]
    
    
    [Trait("Refactored", "True")]
    public void WhiteRookWithWhitePawnAdjacent_CannotMoveOntoAlly_NoMoveToAlly_Refactored()
    {
        // Arrange - White Rook at (0,0), White pawn at (0,1) adjacent
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 0, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook),
            (0, 1, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn));

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Rook);

        // Assert - No move should exist to allied piece at (0,1)
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 0 && r.GetAs<int>("dst_y") == 1);
        Assert.Null(invalidMove);
    }

    [Fact]
    
    [Trait("Feature", "Sequence")]
    public void EmptyBoard_RookInCenter_CanReach14Squares()
    {
        // Arrange - Rook in center at (4, 4) on empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook);

        // Act - Use sequenced moves to compute full sliding movement
        var moves = MoveHelpers.GetSequencedMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Rook);

        // Assert - Rook can reach 7 squares horizontally + 7 squares vertically = 14 total
        // (excluding starting square)
        Assert.True(moves.Length >= 14, $"Expected at least 14 rook moves from center, got {moves.Length}");
    }

    [Fact]
    
    [Trait("Feature", "Sequence")]
    
    [Trait("Refactored", "True")]
    public void EmptyBoard_RookInCenter_CanReach14Squares_Refactored()
    {
        // Arrange - Rook in center at (4, 4) on empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook);

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act - Use sequenced moves to compute full sliding movement
        var moves = MoveHelpers.GetSequencedMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Rook);

        // Assert - Rook can reach 7 squares horizontally + 7 squares vertically = 14 total
        // (excluding starting square)
        Assert.True(moves.Length >= 14, $"Expected at least 14 rook moves from center, got {moves.Length}");
    }

    [Fact]
    
    [Trait("Feature", "Sequence")]
    public void WhiteRookWithDistantBlackPawn_CanCaptureDistant_CaptureExists()
    {
        // Arrange - White Rook at (0, 0), Black pawn at (0, 5) with empty squares between
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 0, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook),
            (0, 5, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn));

        // Act
        var moves = MoveHelpers.GetSequencedMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Rook);

        // Assert - Rook should be able to slide and capture at (0, 5)
        var captureMove = moves.FirstOrDefault(r => 
            r.GetAs<int>("dst_x") == 0 && r.GetAs<int>("dst_y") == 5);
        Assert.NotNull(captureMove);
    }

    [Fact]
    
    [Trait("Feature", "Sequence")]
    
    [Trait("Refactored", "True")]
    public void WhiteRookWithDistantBlackPawn_CanCaptureDistant_CaptureExists_Refactored()
    {
        // Arrange - White Rook at (0, 0), Black pawn at (0, 5) with empty squares between
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 0, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook),
            (0, 5, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn));

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetSequencedMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Rook);

        // Assert - Rook should be able to slide and capture at (0, 5)
        var captureMove = moves.FirstOrDefault(r => 
            r.GetAs<int>("dst_x") == 0 && r.GetAs<int>("dst_y") == 5);
        Assert.NotNull(captureMove);
    }

    [Fact]
    
    [Trait("Feature", "Sequence")]
    public void EmptyBoard_RookAtCorner_CanMoveToAdjacentViaSequencing()
    {
        // Arrange - White Rook at corner (0, 0)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(0, 0, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook);

        // Act
        var moves = MoveHelpers.GetSequencedMovesForPieceType(_spark, _policy, board, ChessPolicy.Piece.Rook);

        // Assert - Should include adjacent square (0, 1)
        var adjacentMove = moves.FirstOrDefault(r => 
            r.GetAs<int>("dst_x") == 0 && r.GetAs<int>("dst_y") == 1);
        Assert.NotNull(adjacentMove);
    }

    [Fact]
    
    [Trait("Feature", "Sequence")]
    
    [Trait("Refactored", "True")]
    public void EmptyBoard_RookAtCorner_CanMoveToAdjacentViaSequencing_Refactored()
    {
        // Arrange - White Rook at corner (0, 0)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(0, 0, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.Rook);

        var refactoredPolicy = new ChessPolicyRefactored(_spark);

        // Act
        var moves = MoveHelpers.GetSequencedMovesForPieceType(_spark, refactoredPolicy, board, ChessPolicy.Piece.Rook);

        // Assert - Should include adjacent square (0, 1)
        var adjacentMove = moves.FirstOrDefault(r => 
            r.GetAs<int>("dst_x") == 0 && r.GetAs<int>("dst_y") == 1);
        Assert.NotNull(adjacentMove);
    }
}
