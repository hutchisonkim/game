using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Policy.Patterns;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for PatternMatcher - atomic pattern matching without sequencing.
/// 
/// These tests verify that PatternMatcher correctly:
/// 1. Filters pieces by faction (turn-based)
/// 2. Matches patterns based on source/destination conditions
/// 3. Computes correct destination coordinates
/// 4. Handles out-of-bounds filtering
/// 5. Works across different piece types and board states
/// 
/// Phase 2A tests. Marked with Debug=True and Phase=2A for filtering.
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Basic")]
[Trait("Feature", "PatternMatching")]

[Trait("Phase", "2A")]
public class PatternMatcherTests
{
    private readonly SparkSession _spark;
    private readonly ChessPolicy _policy;

    public PatternMatcherTests(SparkFixture fixture)
    {
        _spark = fixture.Spark;
        _policy = new ChessPolicy(_spark);
    }

    /// <summary>
    /// Helper to get patterns dataframe for testing using reflection.
    /// </summary>
    private DataFrame GetDefaultPatterns()
    {
        var fieldInfo = typeof(ChessPolicy).GetField("_patternFactory", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (fieldInfo == null)
            throw new InvalidOperationException("Could not find _patternFactory field on ChessPolicy");
        
        var patternFactory = fieldInfo.GetValue(_policy);
        var methodInfo = patternFactory!.GetType().GetMethod("GetPatterns", 
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        if (methodInfo == null)
            throw new InvalidOperationException("Could not find GetPatterns method on PatternFactory");
        
        var patterns = methodInfo.Invoke(patternFactory, null);
        return (DataFrame)patterns!;
    }

    [Fact]
    
    public void MatchAtomicPatterns_StandardBoard_ReturnsValidMovesForAllPieces()
    {
        // Arrange: Standard starting board
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act: Match atomic patterns for white (turn 0)
        var result = PatternMatcher.MatchAtomicPatterns(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0
        );

        // Assert: Should have valid moves on starting board
        var resultCount = result.Count();
        Assert.True(resultCount > 0, "Should have valid moves on starting board");

        var rows = result.Collect();
        // All source pieces should be non-empty
        Assert.True(rows.All(r => r.GetAs<int>("src_piece") != (int)ChessPolicy.Piece.Empty),
            "All source pieces should be non-empty");
    }

    [Fact]
    
    public void MatchAtomicPatterns_KnightInCenter_ReturnsEightMoves()
    {
        // Arrange: Empty board with white knight at center
        var board = BoardHelpers.CreateEmptyBoardWithPiece(4, 4, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Knight);
        
        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act
        var result = PatternMatcher.MatchAtomicPatterns(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0
        );

        // Assert: Knight has 8 possible moves from center
        var rows = result.Collect();
        var knightMoves = rows.Where(r => 
            (r.GetAs<int>("src_generic_piece") & (int)ChessPolicy.Piece.Knight) != 0).ToList();
        
        Assert.Equal(8, knightMoves.Count);
        
        var destinations = knightMoves.Select(r => 
            (r.GetAs<int>("dst_x"), r.GetAs<int>("dst_y"))).OrderBy(d => d).ToList();

        // Verify all L-shaped knight moves are present
        var expectedMoves = new[]
        {
            (2, 3), (2, 5), (3, 2), (3, 6),
            (5, 2), (5, 6), (6, 3), (6, 5)
        }.OrderBy(d => d).ToList();
        
        Assert.Equal(expectedMoves, destinations);
    }

    [Fact]
    
    public void MatchAtomicPatterns_SourceConditions_FiltersByPieceType()
    {
        // Arrange: Standard board
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act: Match patterns for white
        var result = PatternMatcher.MatchAtomicPatterns(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0
        );

        // Assert: All results should satisfy src_conditions
        var rows = result.Collect();
        Assert.True(
            rows.All(r => 
            {
                var srcGeneric = r.GetAs<int>("src_generic_piece");
                var srcConditions = r.GetAs<int>("src_conditions");
                return (srcGeneric & srcConditions) == srcConditions;
            }),
            "All results should satisfy src_conditions"
        );
    }

    [Fact]
    
    public void MatchAtomicPatterns_DestinationConditions_AllResultsSatisfyDstConditions()
    {
        // Arrange: Standard board
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act: Match patterns
        var result = PatternMatcher.MatchAtomicPatterns(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0
        );

        // Assert: All results should satisfy dst_conditions
        var rows = result.Collect();
        Assert.True(
            rows.All(r => 
            {
                var dstGeneric = r.GetAs<int>("dst_generic_piece");
                var dstConditions = r.GetAs<int>("dst_conditions");
                return (dstGeneric & dstConditions) == dstConditions;
            }),
            "All results should satisfy dst_conditions"
        );
    }

    [Fact]
    
    public void MatchAtomicPatterns_TurnBasedFactionFilter_OnlyWhiteMovesOnWhiteTurn()
    {
        // Arrange: Standard board
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act: Match patterns for white (turn 0)
        var whiteTurnResult = PatternMatcher.MatchAtomicPatterns(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0
        );

        // Act: Match patterns for black (turn 1)
        var blackTurnResult = PatternMatcher.MatchAtomicPatterns(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 1
        );

        // Assert: White turn should only have white pieces
        var whiteRows = whiteTurnResult.Collect();
        Assert.True(
            whiteRows.All(r => (r.GetAs<int>("src_piece") & (int)ChessPolicy.Piece.White) != 0),
            "All source pieces on white's turn should be white"
        );

        // Assert: Black turn should only have black pieces
        var blackRows = blackTurnResult.Collect();
        Assert.True(
            blackRows.All(r => (r.GetAs<int>("src_piece") & (int)ChessPolicy.Piece.Black) != 0),
            "All source pieces on black's turn should be black"
        );
    }

    [Fact]
    
    public void MatchAtomicPatterns_OutOfBoundsFiltering_NoMovesOutsideBoard()
    {
        // Arrange: Board with king at corner
        var board = BoardHelpers.CreateEmptyBoardWithPiece(0, 0, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.King);

        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act
        var result = PatternMatcher.MatchAtomicPatterns(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0
        );

        // Assert: All destinations should be within bounds
        var rows = result.Collect();
        Assert.True(
            rows.All(r => 
            {
                var dstX = r.GetAs<int>("dst_x");
                var dstY = r.GetAs<int>("dst_y");
                return dstX >= 0 && dstX < 8 && dstY >= 0 && dstY < 8;
            }),
            "All destination coordinates should be within board bounds [0,7]×[0,7]"
        );

        // King at corner has 3 valid moves: (1,0), (0,1), (1,1)
        var kingMoves = rows.Count();
        Assert.True(kingMoves > 0 && kingMoves <= 3, $"King at corner should have 1-3 moves, got {kingMoves}");
    }

    [Fact]
    
    public void MatchAtomicPatterns_BishopDiagonals_AllMovesAreOnDiagonals()
    {
        // Arrange: Empty board with white bishop at center
        var board = BoardHelpers.CreateEmptyBoardWithPiece(3, 3, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Bishop);

        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act
        var result = PatternMatcher.MatchAtomicPatterns(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0
        );

        // Assert: Bishop moves should all be on diagonals
        var rows = result.Collect();
        var bishopMoves = rows.Where(r =>
            (r.GetAs<int>("src_generic_piece") & (int)ChessPolicy.Piece.Bishop) != 0).ToList();

        Assert.True(bishopMoves.Count > 0, "Should have bishop moves");

        foreach (var move in bishopMoves)
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
    
    public void MatchAtomicPatterns_RookOrthogonals_AllMovesAreOnRowsOrColumns()
    {
        // Arrange: Empty board with white rook at center
        var board = BoardHelpers.CreateEmptyBoardWithPiece(3, 3, 
            ChessPolicy.Piece.White | ChessPolicy.Piece.Rook);

        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act
        var result = PatternMatcher.MatchAtomicPatterns(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0
        );

        // Assert: Rook moves should all be orthogonal
        var rows = result.Collect();
        var rookMoves = rows.Where(r =>
            (r.GetAs<int>("src_generic_piece") & (int)ChessPolicy.Piece.Rook) != 0).ToList();

        Assert.True(rookMoves.Count > 0, "Should have rook moves");

        foreach (var move in rookMoves)
        {
            var srcX = move.GetAs<int>("src_x");
            var srcY = move.GetAs<int>("src_y");
            var dstX = move.GetAs<int>("dst_x");
            var dstY = move.GetAs<int>("dst_y");
            
            var isSameRow = srcY == dstY;
            var isSameCol = srcX == dstX;
            
            Assert.True(isSameRow || isSameCol, $"Rook move from ({srcX},{srcY}) to ({dstX},{dstY}) should be orthogonal");
        }
    }

    [Fact]
    
    public void MatchAtomicPatterns_SequenceFiltering_OnlyPublicPatternsWithoutSequences()
    {
        // Arrange: Standard board
        var board = ChessPolicy.Board.Default;
        board.Initialize();
        var perspectivesDf = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patternsDf = GetDefaultPatterns();

        // Act: Match with no active sequences
        var result = PatternMatcher.MatchAtomicPatterns(
            perspectivesDf,
            patternsDf,
            new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black },
            turn: 0
        );

        // Assert: All patterns should have Public flag set
        var rows = result.Collect();
        Assert.True(
            rows.All(r => (r.GetAs<int>("sequence") & (int)ChessPolicy.Sequence.Public) != 0),
            "All patterns should have Public flag when activeSequences is None"
        );
    }
}


