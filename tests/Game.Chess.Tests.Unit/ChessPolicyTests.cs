// tests/Game.Chess.Tests.Unit/ChessPolicySimulationTests.cs

using Xunit;
using System.Text.Json;
using Game.Core.Tests.Unit;
using Game.Chess.Serialization;
using Game.Chess.Entity;
using Game.Chess.History;

namespace Game.Chess.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Feature", "PolicySimulation")]
public class ChessPolicySimulationTests
{
    [Theory]
    [InlineData(64, 1234, ChessPieceAttribute.None)]
    [InlineData(64, 2345, ChessPieceAttribute.None)]
    [InlineData(64, 3456, ChessPieceAttribute.None)]
    [InlineData(64, 1234, ChessPieceAttribute.Pawn)]
    [InlineData(64, 1234, ChessPieceAttribute.Queen)]
    public void ActionsTimeline_TurnsXSeedY_MatchesReference(int turnCount, int seed, ChessPieceAttribute pieceAttributeOverride)
    {
        // Arrange

        //TODO: rename the test renders as RenderActionsTimeline_* rather than RenderAvailableActions_*
        //TODO: add tests than do collect actions as RenderAvailableActions_*


        string fileName = $"ActionsTimeline_Turns{turnCount}Seed{seed}Piece{pieceAttributeOverride}_MatchesReference.json";
        string outputPath = TestFileHelper.GetOutputPath(fileName);
        string referencePath = TestFileHelper.GetOutputPath(fileName, asReference: true);

        List<string> actionsTimeline = ChessSerializationUtility.GenerateRandom(turnCount, seed, pieceAttributeOverride);
        string json = JsonSerializer.Serialize(actionsTimeline, new JsonSerializerOptions { WriteIndented = true });

        // Act
        TestFileHelper.SaveTextToFile(json, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath), $"Output file missing: {outputPath}");
        Assert.True(File.Exists(referencePath), $"Reference file missing: {referencePath}");

        // Act
        string referenceJson = File.ReadAllText(referencePath);

        // Assert
        Assert.Equal(referenceJson, json);
    }

    [Theory]
    [InlineData(64, 1234, ChessPieceAttribute.Pawn)]
    public void CandidateActionsTimeline_TurnsXSeedY_MatchesReference(int turnCount, int seed, ChessPieceAttribute pieceAttributeOverride)
    {
        // Arrange
        string fileName = $"CandidateActionsTimeline_Turns{turnCount}Seed{seed}Piece{pieceAttributeOverride}_MatchesReference.json";
        string outputPath = TestFileHelper.GetOutputPath(fileName);
        string referencePath = TestFileHelper.GetOutputPath(fileName, asReference: true);

        var rng = new Random(seed);
        var state = new ChessState();
        if (pieceAttributeOverride != ChessPieceAttribute.None)
            state.InitializeBoard(pieceAttributeOverride);

        var candidateActionsTimeline = new List<List<string>>();

        for (int turn = 0; turn < turnCount; turn++)
        {
            IEnumerable<ChessHistoryUtility.ChessActionCandidate> actionCandidates = state.GetActionCandidates();
            int count = actionCandidates.Count();
            if (count == 0) break;

            var candidateActionsThisTurn = actionCandidates
                .Select(ac => ChessSerializationUtility.SerializeAction(
                    ac.Action.From.Row, ac.Action.From.Col,
                    ac.Action.To.Row, ac.Action.To.Col))
                .ToList();

            candidateActionsTimeline.Add(candidateActionsThisTurn);

            ChessHistoryUtility.ChessActionCandidate actionCandidate = actionCandidates.ElementAt(rng.Next(count));
            state = state.Apply(actionCandidate.Action);
        }

        string json = JsonSerializer.Serialize(candidateActionsTimeline, new JsonSerializerOptions { WriteIndented = true });

        // Act
        TestFileHelper.SaveTextToFile(json, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath), $"Output file missing: {outputPath}");
        Assert.True(File.Exists(referencePath), $"Reference file missing: {referencePath}");

        // Act
        string referenceJson = File.ReadAllText(referencePath);

        // Assert
        Assert.Equal(referenceJson, json);
    }

    [Fact]
    public void ActionsTimeline_Turns0_MatchesReference()
    {
        // Arrange
        string fileName = $"ActionsTimeline_Turns0_MatchesReference.json";
        string outputPath = TestFileHelper.GetOutputPath(fileName);
        string referencePath = TestFileHelper.GetOutputPath(fileName, asReference: true);

        List<string> actionsTimeline = ChessSerializationUtility.GenerateInitial();
        string json = JsonSerializer.Serialize(actionsTimeline, new JsonSerializerOptions { WriteIndented = true });

        // Act
        TestFileHelper.SaveTextToFile(json, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath), $"Output file missing: {outputPath}");
        Assert.True(File.Exists(referencePath), $"Reference file missing: {referencePath}");

        // Act
        string referenceJson = File.ReadAllText(referencePath);

        // Assert
        Assert.Equal(referenceJson, json);
    }
}
