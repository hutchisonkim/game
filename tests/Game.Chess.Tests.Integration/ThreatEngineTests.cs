using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Policy.Threats;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for ThreatEngine - threat computation for opponent attacks.
/// 
/// Phase 2B tests. Marked with Debug=True and Phase=2B for filtering.
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Basic")]
[Trait("Feature", "Threats")]
[Trait("Debug", "True")]
[Trait("Phase", "2B")]
public class ThreatEngineTests
{
    private readonly SparkSession _spark;
    private readonly ChessPolicy _policy;

    public ThreatEngineTests(SparkFixture fixture)
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
    
    public void ComputeThreatenedCells_RookInCorner_ThreatensRowAndColumn()
    {
        // Arrange: White rook at (0, 0) on empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(0, 0,
            ChessPolicy.Piece.White | ChessPolicy.Piece.Rook);

        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act: Compute threatened cells (from Black's perspective - what White threatens)
        var threatenedCellsDf = ThreatEngine.ComputeThreatenedCells(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0  // White's turn, compute Black's view of White's threats
        );

        // Assert: Should have threatened cells (at least along row 0 and column 0)
        var threatenedCells = threatenedCellsDf.Collect().ToList();
        Assert.True(threatenedCells.Count > 0, "Rook at corner should threaten at least some cells");

        var threatenedPositions = threatenedCells.Select(r =>
            (r.GetAs<int>("threatened_x"), r.GetAs<int>("threatened_y"))).ToList();

        // Rook at (0,0) threatens row 0 and column 0
        Assert.True(threatenedPositions.Any(p => p.Item1 == 0), "Should threaten cells in column 0");
        Assert.True(threatenedPositions.Any(p => p.Item2 == 0), "Should threaten cells in row 0");
    }

    [Fact]
    
    public void ComputeThreatenedCells_KnightInCenter_ThreatensEightSquares()
    {
        // Arrange: White knight at (4, 4) on empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4,
            ChessPolicy.Piece.White | ChessPolicy.Piece.Knight);

        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act
        var threatenedCellsDf = ThreatEngine.ComputeThreatenedCells(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0
        );

        // Assert: Knight threatens exactly 8 squares (L-shaped moves)
        var threatenedCells = threatenedCellsDf.Collect().ToList();
        Assert.Equal(8, threatenedCells.Count);
    }

    [Fact]
    
    public void ComputeThreatenedCells_KingInCenter_ThreatensEightAdjacent()
    {
        // Arrange: White king at (4, 4) on empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4,
            ChessPolicy.Piece.White | ChessPolicy.Piece.King);

        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act
        var threatenedCellsDf = ThreatEngine.ComputeThreatenedCells(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0
        );

        // Assert: King threatens exactly 8 adjacent squares
        var threatenedCells = threatenedCellsDf.Collect().ToList();
        Assert.Equal(8, threatenedCells.Count);
    }

    [Fact]
    
    public void AddThreatenedBitToPerspectives_WithThreatenedCells_MarksCorrectly()
    {
        // Arrange: Empty board with white rook at (3, 3)
        var board = BoardHelpers.CreateEmptyBoardWithPiece(3, 3,
            ChessPolicy.Piece.White | ChessPolicy.Piece.Rook);

        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Compute actual threatened cells
        var threatenedCellsDf = ThreatEngine.ComputeThreatenedCells(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0
        );

        // Act
        var updatedDf = ThreatEngine.AddThreatenedBitToPerspectives(perspectivesDf, threatenedCellsDf);

        // Assert: Check that threatened bit was added
        var threatMarked = updatedDf.Collect().Where(r =>
            (r.GetAs<int>("generic_piece") & (int)ChessPolicy.Piece.Threatened) != 0).ToList();

        Assert.True(threatMarked.Count > 0, "Should have marked some cells as threatened");
    }

    [Fact]
    
    public void ComputeThreatenedCells_BishopOnDiagonal_ThreatensAlongDiagonals()
    {
        // Arrange: White bishop at (3, 3) on empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(3, 3,
            ChessPolicy.Piece.White | ChessPolicy.Piece.Bishop);

        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act
        var threatenedCellsDf = ThreatEngine.ComputeThreatenedCells(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0
        );

        // Assert: All threatened cells should be on diagonals from (3,3)
        var threatenedCells = threatenedCellsDf.Collect().ToList();
        Assert.True(threatenedCells.Count > 0, "Bishop should threaten at least some cells");

        foreach (var row in threatenedCells)
        {
            var x = row.GetAs<int>("threatened_x");
            var y = row.GetAs<int>("threatened_y");
            var deltaX = Math.Abs(x - 3);
            var deltaY = Math.Abs(y - 3);
            Assert.Equal(deltaX, deltaY);
        }
    }

    [Fact]
    
    public void ComputeThreatenedCells_MultipleAttackers_CombinesThreats()
    {
        // Arrange: White king at (0, 0) and white rook at (7, 7)
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 0, ChessPolicy.Piece.White | ChessPolicy.Piece.King),
            (7, 7, ChessPolicy.Piece.White | ChessPolicy.Piece.Rook)
        );

        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act
        var threatenedCellsDf = ThreatEngine.ComputeThreatenedCells(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0
        );

        // Assert: Should have threatened cells from both pieces
        var threatenedCells = threatenedCellsDf.Collect().ToList();
        Assert.True(threatenedCells.Count > 8, "Two pieces should threaten more than just king's 8");
    }
}
