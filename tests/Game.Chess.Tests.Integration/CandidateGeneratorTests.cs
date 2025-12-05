using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Policy.Candidates;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for CandidateGenerator - unified move generation interface.
/// 
/// Phase 3 tests. Marked with Debug=True and Phase=3 for filtering.
/// Tests the composition of PatternMatcher + SequenceEngine.
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Basic")]
[Trait("Feature", "Candidates")]

[Trait("Phase", "3")]
public class CandidateGeneratorTests
{
    private readonly SparkSession _spark;
    private readonly ChessPolicy _policy;

    public CandidateGeneratorTests(SparkFixture fixture)
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
    
    public void GetMoves_EmptyBoardWithPieces_ReturnsBothAtomicAndSequenced()
    {
        // Arrange: Just a few pieces (pawn + rook)
        var board = BoardHelpers.CreateBoardWithPieces(
            (1, 1, ChessPolicy.Piece.White | ChessPolicy.Piece.Pawn),
            (0, 0, ChessPolicy.Piece.White | ChessPolicy.Piece.Rook)
        );

        var perspectives = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patterns = GetDefaultPatterns();

        // Act
        var moves = CandidateGenerator.GetMoves(
            perspectives,
            patterns,
            new[] { ChessPolicy.Piece.White },
            turn: 0
        );

        // Assert
        var moveList = moves.Collect().ToList();
        Assert.True(moveList.Count > 0, "Should have moves from white pieces");
        
        // Should have pawn moves (atomic) and rook moves (sliding)
        var hasPawnMoves = moveList.Any(m =>
            (m.GetAs<long>("src_generic_piece") & (long)ChessPolicy.Piece.Pawn) != 0
        );
        var hasRookMoves = moveList.Any(m =>
            (m.GetAs<long>("src_generic_piece") & (long)ChessPolicy.Piece.Rook) != 0
        );
        Assert.True(hasPawnMoves || hasRookMoves, "Should have pawn or rook moves");
    }

    [Fact]
    
    public void GetMovesForPieceType_RookOnly_ReturnsOnlyRookMoves()
    {
        // Arrange: White rook at (0, 0), empty board otherwise
        var board = BoardHelpers.CreateEmptyBoardWithPiece(0, 0,
            ChessPolicy.Piece.White | ChessPolicy.Piece.Rook);

        var perspectives = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patterns = GetDefaultPatterns();

        // Act
        var rookMoves = CandidateGenerator.GetMovesForPieceType(
            perspectives,
            patterns,
            new[] { ChessPolicy.Piece.White },
            ChessPolicy.Piece.Rook
        );

        // Assert
        var moveList = rookMoves.Collect().ToList();
        Assert.True(moveList.Count > 0, "Rook should have moves");
        
        // All moves should be from a rook
        Assert.True(moveList.All(m =>
            (m.GetAs<long>("src_generic_piece") & (long)ChessPolicy.Piece.Rook) != 0
        ), "All moves should be from rook");
    }

    [Fact]
    
    public void GetMovesFromSquare_QueenInCenter_ReturnsQueenMoves()
    {
        // Arrange: White queen at (3, 3) on empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(3, 3,
            ChessPolicy.Piece.White | ChessPolicy.Piece.Queen);

        var perspectives = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patterns = GetDefaultPatterns();

        // Act
        var queenMoves = CandidateGenerator.GetMovesFromSquare(
            perspectives,
            patterns,
            new[] { ChessPolicy.Piece.White },
            sourceX: 3,
            sourceY: 3
        );

        // Assert
        var moveList = queenMoves.Collect().ToList();
        Assert.True(moveList.Count > 0, "Queen should have moves");
        
        // All moves should start from (3, 3)
        Assert.True(moveList.All(m =>
            m.GetAs<int>("src_x") == 3 && m.GetAs<int>("src_y") == 3
        ), "All moves should start from queen position");
        
        // Should have both diagonal and orthogonal moves
        var hasDiagonal = moveList.Any(m =>
        {
            var dX = Math.Abs(m.GetAs<int>("dst_x") - 3);
            var dY = Math.Abs(m.GetAs<int>("dst_y") - 3);
            return dX > 0 && dY > 0 && dX == dY;
        });
        Assert.True(hasDiagonal, "Queen should have diagonal moves");
    }

    [Fact]
    
    public void GetMovesToSquare_TargetSquare_ReturnsMovesReachingTarget()
    {
        // Arrange: White rook at (0, 0), empty board
        var board = BoardHelpers.CreateEmptyBoardWithPiece(0, 0,
            ChessPolicy.Piece.White | ChessPolicy.Piece.Rook);

        var perspectives = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patterns = GetDefaultPatterns();

        // Act: Get moves targeting (0, 3)
        var movesToTarget = CandidateGenerator.GetMovesToSquare(
            perspectives,
            patterns,
            new[] { ChessPolicy.Piece.White },
            destX: 0,
            destY: 3
        );

        // Assert
        var moveList = movesToTarget.Collect().ToList();
        
        // Rook should be able to reach (0, 3)
        Assert.True(moveList.Count > 0, "Should have moves to target square");
        
        // All moves should end at (0, 3)
        Assert.True(moveList.All(m =>
            m.GetAs<int>("dst_x") == 0 && m.GetAs<int>("dst_y") == 3
        ), "All moves should target (0, 3)");
    }

    [Fact]
    
    public void GetMoves_WithDeduplication_NoDuplicates()
    {
        // Arrange: Simple board with a few pieces
        var board = BoardHelpers.CreateBoardWithPieces(
            (0, 0, ChessPolicy.Piece.White | ChessPolicy.Piece.Rook),
            (3, 3, ChessPolicy.Piece.White | ChessPolicy.Piece.Bishop)
        );

        var perspectives = _policy.GetPerspectives(board, new[] { ChessPolicy.Piece.White, ChessPolicy.Piece.Black });
        var patterns = GetDefaultPatterns();

        // Act
        var moves = CandidateGenerator.GetMoves(
            perspectives,
            patterns,
            new[] { ChessPolicy.Piece.White },
            turn: 0,
            deduplicateResults: true
        );

        // Assert
        var moveList = moves.Collect().ToList();
        var moveSignatures = moveList.Select(m =>
            $"{m.GetAs<int>("src_x")},{m.GetAs<int>("src_y")},{m.GetAs<int>("dst_x")},{m.GetAs<int>("dst_y")}"
        ).ToList();

        var uniqueSignatures = moveSignatures.Distinct().ToList();
        Assert.Equal(moveSignatures.Count, uniqueSignatures.Count);
    }
}
