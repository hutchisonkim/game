using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Policy.Validation;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;
using static Game.Chess.Tests.Integration.Helpers.TestPieces;
using static Game.Chess.HistoryB.ChessPolicy;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for the LegalityEngine layer - Phase 5 of the refactoring.
/// 
/// Validates that the extracted FilterMovesLeavingKingInCheck works correctly
/// and matches the behavior of the original TimelineService implementation.
/// 
/// This layer is responsible for:
/// 1. King safety - can't move king to threatened squares
/// 2. Pin detection - pinned pieces can only move along the pinning line
/// 3. Discovered checks - can't move a piece that exposes the king
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Legality")]
[Trait("Phase", "5")]
public class LegalityEngineTests : ChessTestBase
{
    public LegalityEngineTests(SparkFixture fixture) : base(fixture)
    {
    }

    [Fact]
    [Trait("Essential", "True")]
    public void FilterMovesLeavingKingInCheck_KingNotInDanger_AllMovesRemain()
    {
        // Arrange - White King at (4, 0) with no threats
        // White Pawn at (4, 1) can move forward
        var board = CreateBoardWithPieces(
            (4, 0, WhiteMintKing),
            (4, 1, WhiteMintPawn));

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Get pawn moves
        int pawnType = (int)Piece.Pawn;
        int publicSeq = (int)Sequence.Public;
        var pawnPatternsDf = patternsDf
            .Filter($"(src_conditions & {pawnType}) != 0 AND (sequence & {publicSeq}) != 0");

        var candidates = TimelineService.ComputeNextCandidates(
            perspectivesDf, pawnPatternsDf, DefaultFactions, turn: 0);
        
        // Act - Filter moves using new LegalityEngine
        var legalMoves = LegalityEngine.FilterMovesLeavingKingInCheck(
            candidates,
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0);

        // Assert - All pawn moves should remain since king is not threatened
        var originalCount = candidates.Count();
        var legalCount = legalMoves.Count();
        
        Assert.True(legalCount > 0, "Expected at least one legal move");
        Assert.Equal(originalCount, legalCount);
    }

    [Fact]
    [Trait("Essential", "True")]
    public void FilterMovesLeavingKingInCheck_KingUnderAttack_RestrictsMovement()
    {
        // Arrange - White King at (4, 0), Black Rook at (4, 7) threatens king on file 4
        // White Pawn at (3, 1) - doesn't help escape check
        var board = CreateBoardWithPieces(
            (4, 0, WhiteMintKing),
            (3, 1, WhiteMintPawn),
            (4, 7, BlackMintRook));

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Get all white piece moves
        int publicSeq = (int)Sequence.Public;
        var whitePatternsDf = patternsDf.Filter($"(sequence & {publicSeq}) != 0");

        var candidates = TimelineService.ComputeNextCandidates(
            perspectivesDf, whitePatternsDf, DefaultFactions, turn: 0);
        
        // Act - Filter moves using LegalityEngine
        var legalMoves = LegalityEngine.FilterMovesLeavingKingInCheck(
            candidates,
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0);

        // Assert - King should only be able to move to safe squares
        // Pawn moves that don't help escape check should be filtered out
        var legalMovesArray = legalMoves.Collect().ToArray();
        Assert.True(legalMovesArray.Length > 0, "Expected at least one legal move (king escape)");
        
        // All remaining moves must be king moves (only valid moves when in check)
        foreach (var move in legalMovesArray)
        {
            int srcGenericPiece = move.GetAs<int>("src_generic_piece");
            int isKing = srcGenericPiece & (int)Piece.King;
            Assert.NotEqual(0, isKing);
        }
    }

    [Fact]
    [Trait("Phase", "5")]
    public void FilterMovesLeavingKingInCheck_EmptyBoard_ReturnsEmpty()
    {
        // Arrange - Empty candidates and perspectives
        var emptyBoard = BoardHelpers.CreateEmptyBoard();
        var perspectivesDf = Policy.GetPerspectives(emptyBoard, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        var emptyCandidates = Spark.Sql("SELECT * FROM (VALUES (1, 2, 3, 4, 1, 2, 3, 4, 0, 0)) as t WHERE 1 = 0");

        // Act - Filter empty candidates
        var result = LegalityEngine.FilterMovesLeavingKingInCheck(
            emptyCandidates,
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0);

        // Assert - Should return empty DataFrame
        Assert.Equal(0, result.Count());
    }

    [Fact]
    [Trait("Essential", "True")]
    public void FilterMovesLeavingKingInCheck_PinnedPiece_CannotMoveAwayFromPin()
    {
        // Arrange - White: King at (4, 4), Rook at (4, 5) (pinned)
        //          Black: Rook at (4, 7) (pinning piece)
        // The white rook is pinned and can only move along the file (staying on the x=4 line)
        var board = CreateBoardWithPieces(
            (4, 4, WhiteMintKing),
            (4, 5, WhiteMintRook),  // Pinned rook
            (4, 7, BlackMintRook));  // Pinning piece

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Get all white piece moves
        int publicSeq = (int)Sequence.Public;
        var whitePatternsDf = patternsDf.Filter($"(sequence & {publicSeq}) != 0");

        var candidates = TimelineService.ComputeNextCandidates(
            perspectivesDf, whitePatternsDf, DefaultFactions, turn: 0);
        
        // Act - Filter moves using LegalityEngine
        var legalMoves = LegalityEngine.FilterMovesLeavingKingInCheck(
            candidates,
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0);

        var legalMovesArray = legalMoves.Collect().ToArray();

        // Assert - Rook moves should be constrained to the file (staying on x=4)
        var rookMoves = legalMovesArray.Where(m =>
        {
            int srcGenericPiece = m.GetAs<int>("src_generic_piece");
            return (srcGenericPiece & (int)Piece.Rook) != 0;
        }).ToArray();

        // If there are rook moves, they must stay on the same file (x=4)
        foreach (var move in rookMoves)
        {
            int dstX = move.GetAs<int>("dst_x");
            Assert.Equal(4, dstX); // Pinned rook must stay on file 4
        }
    }

    [Fact]
    [Trait("Phase", "5")]
    public void FilterMovesLeavingKingInCheck_NoKingOnBoard_AllMovesRemain()
    {
        // Arrange - Board with only pieces, no kings (edge case)
        var board = CreateBoardWithPieces(
            (4, 1, WhiteMintPawn),
            (4, 6, BlackMintPawn));

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        var candidates = TimelineService.ComputeNextCandidates(
            perspectivesDf, patternsDf, DefaultFactions, turn: 0);

        // Act - Filter moves when no king exists
        var result = LegalityEngine.FilterMovesLeavingKingInCheck(
            candidates,
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0);

        // Assert - All candidate moves should remain (no king to protect)
        var originalCount = candidates.Count();
        var resultCount = result.Count();
        Assert.Equal(originalCount, resultCount);
    }

    [Fact]
    [Trait("Phase", "5")]
    public void FilterMovesLeavingKingInCheck_KingMovesToSafety_MovesAccepted()
    {
        // Arrange - White King at (3, 0) under attack by Black Rook at (3, 7)
        // King can move to (2, 0), (2, 1), (4, 0), (4, 1) - positions not on file 3
        var board = CreateBoardWithPieces(
            (3, 0, WhiteMintKing),
            (3, 7, BlackMintRook));

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Get all white piece moves (just the king)
        int publicSeq = (int)Sequence.Public;
        var whitePatternsDf = patternsDf.Filter($"(sequence & {publicSeq}) != 0");

        var candidates = TimelineService.ComputeNextCandidates(
            perspectivesDf, whitePatternsDf, DefaultFactions, turn: 0);
        
        // Act - Filter moves using LegalityEngine
        var legalMoves = LegalityEngine.FilterMovesLeavingKingInCheck(
            candidates,
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0);

        var legalMovesArray = legalMoves.Collect().ToArray();

        // Assert - King should have safe moves (not on file 3)
        Assert.True(legalMovesArray.Length > 0, "King should have escape moves");
        
        foreach (var move in legalMovesArray)
        {
            int dstX = move.GetAs<int>("dst_x");
            Assert.NotEqual(3, dstX); // Safe moves must not be on the attacking file
        }
    }

    [Fact]
    [Trait("Phase", "5")]
    public void FilterMovesLeavingKingInCheck_DiscoveredCheck_BlockedByPin()
    {
        // Arrange - White: King at (4, 4), Bishop at (5, 5) blocking diagonal
        //          Black: Bishop at (0, 1) attacking along diagonal
        // Moving the white bishop away would expose the king - discovered check
        var board = CreateBoardWithPieces(
            (4, 4, WhiteMintKing),
            (5, 5, WhiteMintBishop),
            (0, 1, BlackMintBishop));

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Get all white piece moves
        int publicSeq = (int)Sequence.Public;
        var whitePatternsDf = patternsDf.Filter($"(sequence & {publicSeq}) != 0");

        var candidates = TimelineService.ComputeNextCandidates(
            perspectivesDf, whitePatternsDf, DefaultFactions, turn: 0);
        
        // Act - Filter moves using LegalityEngine
        var legalMoves = LegalityEngine.FilterMovesLeavingKingInCheck(
            candidates,
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0);

        var legalMovesArray = legalMoves.Collect().ToArray();

        // Assert - Bishop moves should be constrained to the diagonal
        var bishopMoves = legalMovesArray.Where(m =>
        {
            int srcGenericPiece = m.GetAs<int>("src_generic_piece");
            return (srcGenericPiece & (int)Piece.Bishop) != 0;
        }).ToArray();

        // If there are bishop moves, they must stay on the blocking diagonal
        foreach (var move in bishopMoves)
        {
            int srcX = move.GetAs<int>("src_x");
            int srcY = move.GetAs<int>("src_y");
            int dstX = move.GetAs<int>("dst_x");
            int dstY = move.GetAs<int>("dst_y");
            
            // Pinned piece must remain on the same diagonal (distance from (5,5) to (4,4) along diagonal)
            int srcDelta = Math.Abs(srcX - 4) - Math.Abs(srcY - 4);
            int dstDelta = Math.Abs(dstX - 4) - Math.Abs(dstY - 4);
            Assert.Equal(srcDelta, dstDelta);
        }
    }
}
