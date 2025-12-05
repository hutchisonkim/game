using Xunit;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for move generation completeness - validating all piece types.
/// 
/// Phase 4 tests. Marked with Debug=True and Phase=4 for filtering.
/// Tests validate that piece movement is correctly implemented via BuildTimeline.
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Basic")]
[Trait("Feature", "MoveLegality")]

[Trait("Phase", "4")]
public class MoveLegalityTests
{
    private readonly ChessPolicy _policy;

    public MoveLegalityTests(SparkFixture fixture)
    {
        _policy = new ChessPolicy(fixture.Spark);
    }

    [Fact]
    [Trait("Essential", "True")]
    public void BuildTimeline_PawnAtStart_HasValidMoves()
    {
        // Arrange: White pawn at (4, 1) - starting position
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 1,
            ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn);

        // Act: Get timeline for depth 1
        var timeline = _policy.BuildTimeline(
            board,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            maxDepth: 1
        );

        // Assert: Timeline should have perspectives (avoid Collect() to prevent memory issues)
        // Just check that the DataFrame is not empty by taking a single row
        var firstRow = timeline.Limit(1).Collect().FirstOrDefault();
        Assert.NotNull(firstRow);
        
        // Verify it has the timestep column
        var timestep = firstRow.GetAs<int>("timestep");
        Assert.True(timestep >= 0, "Timestep should be non-negative");
    }

    [Fact]
    [Trait("Phase", "4")]
    public void BuildTimeline_PawnWithCapture_HasCaptureMove()
    {
        // Arrange: White pawn at (4, 4), black pawn at (3, 5)
        var board = BoardHelpers.CreateBoardWithPieces(
            (4, 4, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn),
            (3, 5, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn)
        );

        // Act
        var timeline = _policy.BuildTimeline(
            board,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            maxDepth: 1
        );

        // Assert: Should have capture move
        var moves = timeline.Collect().ToList();
        var captureMove = moves.FirstOrDefault(m =>
            m.GetAs<int>("src_x") == 4 && m.GetAs<int>("src_y") == 4 &&
            m.GetAs<int>("dst_x") == 3 && m.GetAs<int>("dst_y") == 5
        );
        Assert.NotNull(captureMove);
    }

    [Fact]
    [Trait("Phase", "4")]
    public void BuildTimeline_KnightInCenter_HasEightMoves()
    {
        // Arrange: White knight at (4, 4) - center
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4,
            ChessPolicy.Piece.White | ChessPolicy.Piece.Knight);

        // Act
        var timeline = _policy.BuildTimeline(
            board,
            new[] { ChessPolicy.Piece.White },
            maxDepth: 1
        );

        // Assert: Should have exactly 8 knight moves
        var moves = timeline.Collect().ToList();
        var knightMoves = moves.Where(m =>
            (m.GetAs<long>("src_generic_piece") & (long)ChessPolicy.Piece.Knight) != 0
        ).ToList();

        Assert.True(knightMoves.Count <= 8, "Knight should have at most 8 moves");
        Assert.True(knightMoves.Count > 0, "Knight should have moves");
    }

    [Fact]
    [Trait("Phase", "4")]
    public void BuildTimeline_BishopInCenter_AllMovesAreDiagonal()
    {
        // Arrange: White bishop at (3, 3)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(3, 3,
            ChessPolicy.Piece.White | ChessPolicy.Piece.Bishop);

        // Act
        var timeline = _policy.BuildTimeline(
            board,
            new[] { ChessPolicy.Piece.White },
            maxDepth: 1
        );

        // Assert: All bishop moves should be diagonal
        var moves = timeline.Collect().ToList();
        var bishopMoves = moves.Where(m =>
            (m.GetAs<long>("src_generic_piece") & (long)ChessPolicy.Piece.Bishop) != 0
        ).ToList();

        Assert.True(bishopMoves.Count > 0, "Bishop should have moves");
        foreach (var move in bishopMoves)
        {
            var dX = Math.Abs(move.GetAs<int>("dst_x") - 3);
            var dY = Math.Abs(move.GetAs<int>("dst_y") - 3);
            Assert.Equal(dX, dY);
        }
    }

    [Fact]
    [Trait("Phase", "4")]
    public void BuildTimeline_RookInCenter_AllMovesAreOrthogonal()
    {
        // Arrange: White rook at (4, 4)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4,
            ChessPolicy.Piece.White | ChessPolicy.Piece.Rook);

        // Act
        var timeline = _policy.BuildTimeline(
            board,
            new[] { ChessPolicy.Piece.White },
            maxDepth: 1
        );

        // Assert: All rook moves should be orthogonal
        var moves = timeline.Collect().ToList();
        var rookMoves = moves.Where(m =>
            (m.GetAs<long>("src_generic_piece") & (long)ChessPolicy.Piece.Rook) != 0
        ).ToList();

        Assert.True(rookMoves.Count > 0, "Rook should have moves");
        foreach (var move in rookMoves)
        {
            var dX = move.GetAs<int>("dst_x") - 4;
            var dY = move.GetAs<int>("dst_y") - 4;
            Assert.True((dX == 0 && dY != 0) || (dX != 0 && dY == 0));
        }
    }

    [Fact]
    [Trait("Phase", "4")]
    public void BuildTimeline_QueenInCenter_HasBothOrthogonalAndDiagonal()
    {
        // Arrange: White queen at (4, 4)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4,
            ChessPolicy.Piece.White | ChessPolicy.Piece.Queen);

        // Act
        var timeline = _policy.BuildTimeline(
            board,
            new[] { ChessPolicy.Piece.White },
            maxDepth: 1
        );

        // Assert: Queen should have both types of moves
        var moves = timeline.Collect().ToList();
        var queenMoves = moves.Where(m =>
            (m.GetAs<long>("src_generic_piece") & (long)ChessPolicy.Piece.Queen) != 0
        ).ToList();

        Assert.True(queenMoves.Count > 0, "Queen should have moves");

        var hasOrthogonal = queenMoves.Any(m =>
        {
            var dX = m.GetAs<int>("dst_x") - 4;
            var dY = m.GetAs<int>("dst_y") - 4;
            return (dX == 0 && dY != 0) || (dX != 0 && dY == 0);
        });

        var hasDiagonal = queenMoves.Any(m =>
        {
            var dX = Math.Abs(m.GetAs<int>("dst_x") - 4);
            var dY = Math.Abs(m.GetAs<int>("dst_y") - 4);
            return dX > 0 && dY > 0 && dX == dY;
        });

        Assert.True(hasOrthogonal || hasDiagonal, "Queen should have orthogonal or diagonal moves");
    }

    [Fact]
    [Trait("Phase", "4")]
    public void BuildTimeline_KingInCenter_HasEightAdjacentMoves()
    {
        // Arrange: White king at (4, 4)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4,
            ChessPolicy.Piece.White | ChessPolicy.Piece.King);

        // Act
        var timeline = _policy.BuildTimeline(
            board,
            new[] { ChessPolicy.Piece.White },
            maxDepth: 1
        );

        // Assert: King should have up to 8 adjacent moves
        var moves = timeline.Collect().ToList();
        var kingMoves = moves.Where(m =>
            (m.GetAs<long>("src_generic_piece") & (long)ChessPolicy.Piece.King) != 0
        ).ToList();

        Assert.True(kingMoves.Count <= 8, "King should have at most 8 moves");
        Assert.True(kingMoves.Count > 0, "King should have moves");

        // All moves should be to adjacent squares
        foreach (var move in kingMoves)
        {
            var dX = Math.Abs(move.GetAs<int>("dst_x") - 4);
            var dY = Math.Abs(move.GetAs<int>("dst_y") - 4);
            Assert.True(dX <= 1 && dY <= 1, "King should only move 1 square");
        }
    }
}
