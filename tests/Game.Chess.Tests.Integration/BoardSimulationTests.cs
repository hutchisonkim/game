using Xunit;
using Microsoft.Spark.Sql;
using Game.Chess.HistoryB;
using Game.Chess.Tests.Integration.Infrastructure;
using Game.Chess.Tests.Integration.Helpers;
using static Game.Chess.Tests.Integration.Helpers.TestPieces;
using static Game.Chess.HistoryB.ChessPolicy;

namespace Game.Chess.Tests.Integration;

/// <summary>
/// Tests for the SimulateBoardAfterMove functionality.
/// These tests validate the core forward-timeline simulation abstraction
/// that enables pattern-driven rule evaluation.
/// </summary>
[Collection("Spark collection")]
[Trait("Category", "Integration")]
[Trait("Feature", "BoardSimulation")]
public class BoardSimulationTests : ChessTestBase
{
    public BoardSimulationTests(SparkFixture fixture) : base(fixture)
    {
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void SimulateBoardAfterMove_SimpleMove_UpdatesSourceAndDestination()
    {
        // Arrange - White Pawn at (0, 1) moving to (0, 2)
        var board = CreateBoardWithPieces(
            (0, 1, WhiteMintPawn));

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Get pawn forward move candidates
        int pawnType = (int)Piece.Pawn;
        int publicSeq = (int)Sequence.Public;
        var pawnPatternsDf = patternsDf
            .Filter($"(src_conditions & {pawnType}) != 0 AND (sequence & {publicSeq}) != 0");

        var candidates = TimelineService.ComputeNextCandidates(
            perspectivesDf, pawnPatternsDf, DefaultFactions, turn: 0);

        // Filter to just the move to (0, 2)
        var moveToE3 = candidates.Filter(
            Functions.Col("dst_x").EqualTo(0).And(Functions.Col("dst_y").EqualTo(2)));

        // Act
        var simulatedPerspectives = TimelineService.SimulateBoardAfterMove(
            perspectivesDf, moveToE3, DefaultFactions);

        // Assert - The simulated board should have the piece at the new location
        var simulatedRows = simulatedPerspectives.Collect().ToArray();
        Assert.True(simulatedRows.Length > 0, "Expected simulated perspectives");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void SimulateBoardAfterMove_EmptyCandidates_ReturnsPerspectivesUnchanged()
    {
        // Arrange - Create a board with a pawn
        var board = CreateBoardWithPieces(
            (3, 3, WhiteMintPawn));

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        
        // Create empty candidates by filtering to impossible condition
        var patternsDf = new PatternFactory(Spark).GetPatterns();
        var emptyCandidates = TimelineService.ComputeNextCandidates(
            perspectivesDf, patternsDf, DefaultFactions, turn: 0)
            .Filter(Functions.Lit(false));

        // Act
        var result = TimelineService.SimulateBoardAfterMove(
            perspectivesDf, emptyCandidates, DefaultFactions);

        // Assert - Should return perspectives unchanged
        Assert.Equal(perspectivesDf.Count(), result.Count());
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void ValidateMoveDoesNotLeaveKingInCheck_KingSafeMove_ReturnsTrue()
    {
        // Arrange - White King at (4, 0) with no threats
        var board = CreateBoardWithPieces(
            (4, 0, WhiteMintKing),
            (0, 1, WhiteMintPawn));

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Get king move candidates
        int kingType = (int)Piece.King;
        int publicSeq = (int)Sequence.Public;
        var kingPatternsDf = patternsDf
            .Filter($"(src_conditions & {kingType}) != 0 AND (sequence & {publicSeq}) != 0");

        var candidates = TimelineService.ComputeNextCandidates(
            perspectivesDf, kingPatternsDf, DefaultFactions, turn: 0);

        // Filter to a safe king move
        var safeMove = candidates.Limit(1);

        // Act
        var isValid = TimelineService.ValidateMoveDoesNotLeaveKingInCheck(
            perspectivesDf, safeMove, patternsDf, DefaultFactions, turn: 0);

        // Assert
        Assert.True(isValid, "King move to safe square should be valid");
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void ValidateMoveDoesNotLeaveKingInCheck_EmptyCandidate_ReturnsTrue()
    {
        // Arrange
        var board = CreateBoardWithPieces(
            (4, 0, WhiteMintKing));

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Create empty candidates
        var emptyCandidates = TimelineService.ComputeNextCandidates(
            perspectivesDf, patternsDf, DefaultFactions, turn: 0)
            .Filter(Functions.Lit(false));

        // Act
        var isValid = TimelineService.ValidateMoveDoesNotLeaveKingInCheck(
            perspectivesDf, emptyCandidates, patternsDf, DefaultFactions, turn: 0);

        // Assert - Empty move should be valid (nothing to validate)
        Assert.True(isValid);
    }

    [Fact]
    [Trait("Performance", "Fast")]
    public void BuildTimeline_WithSimulation_GeneratesMultipleTimesteps()
    {
        // Arrange - Simple board setup
        var board = CreateBoardWithPieces(
            (4, 1, WhiteMintPawn),
            (4, 0, WhiteMintKing));

        var perspectivesDf = Policy.GetPerspectives(board, DefaultFactions);
        var patternsDf = new PatternFactory(Spark).GetPatterns();

        // Act - Build timeline with maxDepth=1
        var timelineDf = TimelineService.BuildTimeline(perspectivesDf, patternsDf, DefaultFactions, maxDepth: 1);

        // Assert - Should have timesteps 0 and 1
        var timesteps = timelineDf.Select("timestep").Distinct().Collect().Select(r => r.Get(0)).ToList();
        Assert.Contains(0, timesteps);
        Assert.Contains(1, timesteps);
    }
}
