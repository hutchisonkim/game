using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;
using static Game.Chess.Tests.Integration.Helpers.TestPieces;
using static Game.Chess.HistoryB.ChessPolicy;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for En Passant capture rule.
/// En passant allows a pawn to capture an adjacent enemy pawn that has just moved two squares.
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Integration")]
[Trait("Feature", "EnPassant")]
[Trait("PieceType", "Pawn")]
public class EnPassantTests : ChessTestBase
{
    public EnPassantTests(SparkFixture fixture) : base(fixture)
    {
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void PawnPatterns_EnPassantCapture_ExistsForBothDirections()
    {
        // Act - Get en passant capture patterns (OutE)
        var patterns = GetPatternsFor(Piece.Pawn, Sequence.OutE | Sequence.Public);

        // Assert - Should have 2 patterns (left and right capture)
        MoveAssertions.HasMinimumRowCount(patterns, 2, "en passant OutE patterns");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void PawnPatterns_EnPassantContinuation_Exists()
    {
        // Act - Get en passant continuation patterns (InE)
        var patterns = GetPatternsFor(Piece.Pawn, Sequence.InE);

        // Assert - Should have at least 1 pattern (move forward after sideways capture)
        MoveAssertions.HasMinimumRowCount(patterns, 1, "en passant InE continuation patterns");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void WhitePawn_WithPassingBlackPawnAdjacent_HasEnPassantPattern()
    {
        // Arrange - White pawn at (4, 4), Black passing pawn at (5, 4)
        // The "Passing" flag indicates the enemy pawn just moved two squares
        var board = CreateBoardWithPieces(
            (4, 4, Piece.White | Piece.Pawn),
            (5, 4, Piece.Black | Piece.Pawn | Piece.Passing));

        // Act - Get moves filtered to just OutE (en passant capture) patterns
        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        int outE = (int)Sequence.OutE;
        int publicFlag = (int)Sequence.Public;
        int pawnType = (int)Piece.Pawn;
        var patternsDf = new PatternFactory(Spark).GetPatterns()
            .Filter($"(src_conditions & {pawnType}) != 0 AND (sequence & {outE}) != 0 AND (sequence & {publicFlag}) != 0");
        
        var candidates = TimelineService.ComputeNextCandidates(perspectivesDf, patternsDf, DefaultFactions);
        var moves = candidates.Collect().ToArray();

        // Assert - Should have en passant capture available to (5, 4) or sideways
        Assert.True(moves.Length > 0, "Expected en passant capture pattern to match");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void WhitePawn_WithNonPassingBlackPawnAdjacent_NoEnPassant()
    {
        // Arrange - White pawn at (4, 4), Black pawn at (5, 4) WITHOUT Passing flag
        var board = CreateBoardWithPieces(
            (4, 4, Piece.White | Piece.Pawn),
            (5, 4, Piece.Black | Piece.Pawn));  // No Passing flag

        // Act - Get moves filtered to just OutE (en passant capture) patterns
        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        int outE = (int)Sequence.OutE;
        int publicFlag = (int)Sequence.Public;
        int pawnType = (int)Piece.Pawn;
        var patternsDf = new PatternFactory(Spark).GetPatterns()
            .Filter($"(src_conditions & {pawnType}) != 0 AND (sequence & {outE}) != 0 AND (sequence & {publicFlag}) != 0");
        
        var candidates = TimelineService.ComputeNextCandidates(perspectivesDf, patternsDf, DefaultFactions);
        var moves = candidates.Collect().ToArray();

        // Assert - Should NOT have en passant capture (enemy pawn doesn't have Passing flag)
        Assert.Empty(moves);
    }
}
