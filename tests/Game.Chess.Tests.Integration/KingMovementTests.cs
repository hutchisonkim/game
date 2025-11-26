using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for King piece movement rules.
/// King can move one square in any direction (orthogonal or diagonal).
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Movement")]
[Trait("PieceType", "King")]
public class KingMovementTests
{
    private readonly SparkSession _spark;
    private readonly ChessPolicy _policy;

    public KingMovementTests(SparkFixture fixture)
    {
        _spark = fixture.Spark;
        _policy = new ChessPolicy(_spark);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void EmptyBoard_KingInCenter_Has8Moves()
    {
        // Arrange - King in center of empty 8x8 board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King);

        // Act
        var moves = MoveHelpers.GetMovesForPieceTypeNoSequenceFilter(_spark, _policy, board, ChessPolicy.Piece.King);

        // Assert - King can move to all 8 surrounding squares
        Assert.Equal(8, moves.Length);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void EmptyBoard_KingAtCorner_Has3Moves()
    {
        // Arrange - King at corner (0,0)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(0, 0, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King);

        // Act
        var moves = MoveHelpers.GetMovesForPieceTypeNoSequenceFilter(_spark, _policy, board, ChessPolicy.Piece.King);

        // Assert - King can only move to 3 squares from corner
        Assert.Equal(3, moves.Length);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void WhiteKingWithBlackPawnAdjacent_CanCapture_Has8MovesIncludingCapture()
    {
        // Arrange - White King at (4,4), Black pawn at (5,5)
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King),
            (5, 5, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn));

        // Act
        var moves = MoveHelpers.GetMovesForPieceTypeNoSequenceFilter(_spark, _policy, board, ChessPolicy.Piece.King);

        // Assert - 7 empty squares + 1 capture = 8 moves
        Assert.Equal(8, moves.Length);
        
        // Verify capture move exists to (5,5)
        var captureMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 5 && r.GetAs<int>("dst_y") == 5);
        Assert.NotNull(captureMove);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void WhiteKingWithWhitePawnAdjacent_CannotMoveOntoAlly_Has7Moves()
    {
        // Arrange - White King at (4,4), White pawn at (5,5) blocking one square
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Mint | ChessPolicy.Piece.King),
            (5, 5, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn));

        // Act
        var moves = MoveHelpers.GetMovesForPieceTypeNoSequenceFilter(_spark, _policy, board, ChessPolicy.Piece.King);

        // Assert - Only 7 moves (blocked by ally at 5,5)
        Assert.Equal(7, moves.Length);
        
        // Verify no move to allied piece at (5,5)
        var invalidMove = moves.FirstOrDefault(r => r.GetAs<int>("dst_x") == 5 && r.GetAs<int>("dst_y") == 5);
        Assert.Null(invalidMove);
    }
}
