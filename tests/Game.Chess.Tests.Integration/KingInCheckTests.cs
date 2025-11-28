using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;
using static Game.Chess.Tests.Integration.Helpers.TestPieces;
using static Game.Chess.HistoryB.ChessPolicy;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for the King-in-Check validation mechanic.
/// Validates that moves leaving the king in check are correctly filtered out.
/// This is a core chess rule: a player cannot make a move that leaves their own king under attack.
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Integration")]
[Trait("Feature", "KingInCheck")]
public class KingInCheckTests : ChessTestBase
{
    public KingInCheckTests(SparkFixture fixture) : base(fixture)
    {
    }

    [Fact]
    [Trait("Performance", "Fast")]
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
        
        // Act - Filter moves that would leave king in check
        var legalMoves = TimelineService.FilterMovesLeavingKingInCheck(
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
    [Trait("Performance", "Fast")]
    public void FilterMovesLeavingKingInCheck_KingUnderAttackByRook_MustBlockOrMove()
    {
        // Arrange - White King at (4, 0), Black Rook at (4, 7) threatens the king along file 4
        // White Pawn at (3, 1) - its moves don't help escape check
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
        
        // Act - Filter moves that would leave king in check
        var legalMoves = TimelineService.FilterMovesLeavingKingInCheck(
            candidates,
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0);

        var legalMovesArray = legalMoves.Collect().ToArray();

        // Assert - Only king moves that escape the threat should be legal
        // Pawn moves at (3, 1) don't block the rook's attack on the king
        foreach (var move in legalMovesArray)
        {
            int srcGenericPiece = move.GetAs<int>("src_generic_piece");
            int dstX = move.GetAs<int>("dst_x");

            // If the pawn moves, it must be blocking the rook (moving to file 4)
            bool isPawnMove = (srcGenericPiece & (int)Piece.Pawn) != 0;
            if (isPawnMove)
            {
                // Pawn at (3,1) cannot block (would need to be on file 4)
                Assert.True(false, "Pawn at (3,1) should not have any legal moves");
            }
            
            // If the king moves, it must move off file 4 to escape the rook
            bool isKingMove = (srcGenericPiece & (int)Piece.King) != 0;
            if (isKingMove)
            {
                Assert.NotEqual(4, dstX);  // King must not stay on file 4
            }
        }
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void FilterMovesLeavingKingInCheck_KingCanMoveToSafety_SafeMovesAvailable()
    {
        // Arrange - White King at (4, 4) center, Black Knight at (2, 3) threatens (4, 4)
        // King should be able to move to adjacent squares not threatened by knight
        var board = CreateBoardWithPieces(
            (4, 4, WhiteMintKing),
            (2, 3, BlackMintKnight));

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Get king moves only
        int kingType = (int)Piece.King;
        int publicSeq = (int)Sequence.Public;
        var kingPatternsDf = patternsDf
            .Filter($"(src_conditions & {kingType}) != 0 AND (sequence & {publicSeq}) != 0");

        var candidates = TimelineService.ComputeNextCandidates(
            perspectivesDf, kingPatternsDf, DefaultFactions, turn: 0);
        
        // Act - Filter moves that would leave king in check
        var legalMoves = TimelineService.FilterMovesLeavingKingInCheck(
            candidates,
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0);

        var legalMovesArray = legalMoves.Collect().ToArray();

        // Assert - King should have safe squares to move to
        Assert.True(legalMovesArray.Length > 0, "King should have at least one safe escape square");
        
        // Knight at (2, 3) threatens: (0,2), (0,4), (1,1), (1,5), (3,1), (3,5), (4,2), (4,4)
        // King at (4, 4) can move to: (3,3), (3,4), (3,5), (4,3), (4,5), (5,3), (5,4), (5,5)
        // Safe squares: (3,3), (3,4), (4,3), (4,5), (5,3), (5,4), (5,5)
        // Threatened by knight: (3,5) - this should be filtered out
        foreach (var move in legalMovesArray)
        {
            int dstX = move.GetAs<int>("dst_x");
            int dstY = move.GetAs<int>("dst_y");
            
            // King shouldn't move to (3, 5) which is threatened by the knight
            Assert.False(dstX == 3 && dstY == 5, 
                "King should not move to (3, 5) which is threatened by knight at (2, 3)");
        }
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void ComputeLegalMoves_IntegrationTest_FiltersIllegalMoves()
    {
        // Arrange - Simple setup with king and threatening piece
        // King at (4, 0), Rook at (4, 7) threatens column 4 (x=4)
        // This matches the passing test pattern
        var board = CreateBoardWithPieces(
            (4, 0, WhiteMintKing),
            (4, 7, BlackMintRook));  // Rook threatens column 4

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Get king patterns only (Public sequence patterns for king moves)
        int kingType = (int)Piece.King;
        int publicSeq = (int)Sequence.Public;
        var kingPatternsDf = patternsDf
            .Filter($"(src_conditions & {kingType}) != 0 AND (sequence & {publicSeq}) != 0");

        // Get all candidate king moves
        var candidates = TimelineService.ComputeNextCandidates(
            perspectivesDf, kingPatternsDf, DefaultFactions, turn: 0);

        // Act - Filter moves that leave king in check
        var legalMoves = TimelineService.FilterMovesLeavingKingInCheck(
            candidates,
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0);

        var legalMovesArray = legalMoves.Collect().ToArray();

        // Assert - King at (4, 0) under attack from rook at (4, 7)
        // King can move to (3, 0), (3, 1), (5, 0), (5, 1) to escape
        // Moving to (4, 1) stays on column 4 under attack
        Assert.True(legalMovesArray.Length > 0, "King should have escape moves");
        
        foreach (var move in legalMovesArray)
        {
            int srcGenericPiece = move.GetAs<int>("src_generic_piece");
            int dstX = move.GetAs<int>("dst_x");
            
            bool isKingMove = (srcGenericPiece & (int)Piece.King) != 0;
            if (isKingMove)
            {
                // King must escape - cannot stay on column 4 (x=4) which is threatened by the rook
                Assert.NotEqual(4, dstX);
            }
        }
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void FilterMovesLeavingKingInCheck_PinnedPiece_CannotMoveAwayFromPin()
    {
        // Arrange - White King at (0, 0), White Rook at (0, 3), Black Rook at (0, 7)
        // White Rook is pinned - moving it would expose the king to the black rook
        var board = CreateBoardWithPieces(
            (0, 0, WhiteMintKing),
            (0, 3, WhiteMintRook),
            (0, 7, BlackMintRook));

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Get all white piece moves
        int publicSeq = (int)Sequence.Public;
        var patternsFiltered = patternsDf.Filter($"(sequence & {publicSeq}) != 0");

        var candidates = TimelineService.ComputeNextCandidates(
            perspectivesDf, patternsFiltered, DefaultFactions, turn: 0);

        // Act - Filter moves that would leave king in check
        var legalMoves = TimelineService.FilterMovesLeavingKingInCheck(
            candidates,
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0);

        var legalMovesArray = legalMoves.Collect().ToArray();

        // Assert - White Rook at (0, 3) should NOT be able to move horizontally
        // because it's pinned protecting the king
        // It can only move along the file (vertically on x=0)
        foreach (var move in legalMovesArray)
        {
            int srcX = move.GetAs<int>("src_x");
            int srcY = move.GetAs<int>("src_y");
            int dstX = move.GetAs<int>("dst_x");
            int srcGenericPiece = move.GetAs<int>("src_generic_piece");

            bool isRookMove = (srcGenericPiece & (int)Piece.Rook) != 0;
            if (isRookMove && srcX == 0 && srcY == 3)
            {
                // Pinned rook can only move along file 0 (stay on x=0)
                Assert.Equal(0, dstX);
            }
        }
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void FilterMovesLeavingKingInCheck_EmptyCandidates_ReturnsEmpty()
    {
        // Arrange - Create an empty candidates DataFrame
        var board = CreateBoardWithPieces(
            (4, 0, WhiteMintKing));

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Create empty candidates by filtering to impossible condition
        var emptyCandidates = TimelineService.ComputeNextCandidates(
            perspectivesDf, patternsDf, DefaultFactions, turn: 0)
            .Filter(Functions.Lit(false));  // Force empty result

        // Act
        var result = TimelineService.FilterMovesLeavingKingInCheck(
            emptyCandidates,
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0);

        // Assert
        Assert.Equal(0, result.Count());
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void FilterMovesLeavingKingInCheck_NoKingOnBoard_AllMovesRemain()
    {
        // Arrange - No king on board (edge case for partial board tests)
        var board = CreateBoardWithPieces(
            (3, 3, WhiteMintPawn),
            (5, 5, BlackMintPawn));

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        int pawnType = (int)Piece.Pawn;
        int publicSeq = (int)Sequence.Public;
        var pawnPatternsDf = patternsDf
            .Filter($"(src_conditions & {pawnType}) != 0 AND (sequence & {publicSeq}) != 0");

        var candidates = TimelineService.ComputeNextCandidates(
            perspectivesDf, pawnPatternsDf, DefaultFactions, turn: 0);

        // Act
        var result = TimelineService.FilterMovesLeavingKingInCheck(
            candidates,
            perspectivesDf,
            patternsDf,
            DefaultFactions,
            turn: 0);

        // Assert - With no king, all moves should be considered legal
        Assert.Equal(candidates.Count(), result.Count());
    }
}
