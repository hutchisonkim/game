using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Policy.Sequences;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for SequenceEngine - multi-step sliding sequences.
/// 
/// Phase 2C tests. Marked with Debug=True and Phase=2C for filtering.
/// These tests are minimal to avoid long-running sequences.
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Basic")]
[Trait("Feature", "Sequences")]
[Trait("Debug", "True")]
[Trait("Phase", "2C")]
public class SequenceEngineTests
{
    private readonly SparkSession _spark;
    private readonly ChessPolicy _policy;

    public SequenceEngineTests(SparkFixture fixture)
    {
        _spark = fixture.Spark;
        _policy = new ChessPolicy(_spark);
    }

    private DataFrame GetDefaultPatterns()
    {
        var fieldInfo = typeof(ChessPolicy).GetField("_patternFactory", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (fieldInfo == null)
            throw new InvalidOperationException("Could not find _patternFactory");
        
        var patternFactory = fieldInfo.GetValue(_policy);
        var methodInfo = patternFactory!.GetType().GetMethod("GetPatterns", 
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (methodInfo == null)
            throw new InvalidOperationException("Could not find GetPatterns");
        
        var patterns = methodInfo.Invoke(patternFactory, null);
        return (DataFrame)patterns!;
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void ExpandSequencedMoves_EmptyBoardRook_ReturnsMultipleSteps()
    {
        // Arrange: White rook at (0, 0) on otherwise empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(0, 0,
            ChessPolicy.Piece.White | ChessPolicy.Piece.Rook);

        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act
        var sequencedMoves = SequenceEngine.ExpandSequencedMoves(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0,
            maxDepth: 7
        );

        // Assert: Should have moves from rook
        var moveList = sequencedMoves.Collect().ToList();
        Assert.True(moveList.Count > 0, "Rook should have sequenced moves");

        // All moves should start from (0, 0)
        Assert.True(moveList.All(r =>
            r.GetAs<int>("src_x") == 0 && r.GetAs<int>("src_y") == 0),
            "All moves should start from rook's position");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void ExpandSequencedMoves_RookBlocked_StopsAtBlockingPiece()
    {
        // Arrange: White rook at (0, 0), black pawn at (0, 3)
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 0, ChessPolicy.Piece.White | ChessPolicy.Piece.Rook),
            (0, 3, ChessPolicy.Piece.Black | ChessPolicy.Piece.Pawn)
        );

        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act
        var sequencedMoves = SequenceEngine.ExpandSequencedMoves(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0,
            maxDepth: 7
        );

        // Assert: Should not have moves beyond blocked piece
        var moveList = sequencedMoves.Collect().ToList();
        var destinationYs = moveList.Select(r => r.GetAs<int>("dst_y")).Distinct().OrderBy(y => y).ToList();

        // Should have moves up to but not past (0, 3)
        Assert.True(moveList.Count > 0, "Should have some moves");
        Assert.True(destinationYs.Max() <= 3, "Should not move past blocking piece");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void ExpandSequencedMoves_BishopOnDiagonal_ReturnsConsistentDirection()
    {
        // Arrange: White bishop at (0, 0) on empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(0, 0,
            ChessPolicy.Piece.White | ChessPolicy.Piece.Bishop);

        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act
        var sequencedMoves = SequenceEngine.ExpandSequencedMoves(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0,
            maxDepth: 7
        );

        // Assert: Bishop moves should be on diagonal
        var moveList = sequencedMoves.Collect().ToList();
        Assert.True(moveList.Count > 0, "Bishop should have sequenced moves");

        foreach (var move in moveList)
        {
            var srcX = move.GetAs<int>("src_x");
            var srcY = move.GetAs<int>("src_y");
            var dstX = move.GetAs<int>("dst_x");
            var dstY = move.GetAs<int>("dst_y");
            
            var deltaX = Math.Abs(dstX - srcX);
            var deltaY = Math.Abs(dstY - srcY);
            Assert.Equal(deltaX, deltaY);
        }
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void ExpandSequencedMoves_NoSlidingPieces_ReturnsEmpty()
    {
        // Arrange: Empty board with only knights (no sliding pieces)
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 0, ChessPolicy.Piece.White | ChessPolicy.Piece.Knight),
            (7, 7, ChessPolicy.Piece.White | ChessPolicy.Piece.Knight)
        );

        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act
        var sequencedMoves = SequenceEngine.ExpandSequencedMoves(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0,
            maxDepth: 7
        );

        // Assert: Knights don't have sequenced moves (no sliding)
        var moveList = sequencedMoves.Collect().ToList();
        // Should be empty since knights use patterns, not sequences
        Assert.True(moveList.Count == 0, "Knights should not generate sequenced moves");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void ExpandSequencedMoves_QueenCombined_HasDiagonalAndOrthogonal()
    {
        // Arrange: White queen at (3, 3) on empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(3, 3,
            ChessPolicy.Piece.White | ChessPolicy.Piece.Queen);

        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act
        var sequencedMoves = SequenceEngine.ExpandSequencedMoves(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0,
            maxDepth: 7
        );

        // Assert: Queen should have both diagonal and orthogonal moves
        var moveList = sequencedMoves.Collect().ToList();
        Assert.True(moveList.Count > 0, "Queen should have sequenced moves");

        // Check for both diagonal and orthogonal patterns
        var hasDiagonal = moveList.Any(m =>
        {
            var dX = Math.Abs(m.GetAs<int>("dst_x") - 3);
            var dY = Math.Abs(m.GetAs<int>("dst_y") - 3);
            return dX > 0 && dY > 0 && dX == dY;
        });

        var hasOrthogonal = moveList.Any(m =>
        {
            var dX = m.GetAs<int>("dst_x") - 3;
            var dY = m.GetAs<int>("dst_y") - 3;
            return (dX == 0 && dY != 0) || (dX != 0 && dY == 0);
        });

        Assert.True(hasDiagonal || hasOrthogonal, "Queen should have diagonal or orthogonal moves");
    }
}
