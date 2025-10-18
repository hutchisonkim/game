// tests/Game.Chess.Renders.Tests.Unit/ChessPolicySimulationTests.cs

using Xunit;
using Game.Chess.Policy;
using System.Text.Json;
using Game.Core.Tests.Unit;

namespace Game.Chess.Renders.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Feature", "PolicySimulation")]
public class ChessPolicySimulationTests
{
    [Theory]
    [InlineData(64, 1234)]
    [InlineData(64, 2345)]
    [InlineData(64, 3456)]
    public void GenerateTransitionSequenceJson_MatchesReference(int turnCount, int seed)
    {
        // Arrange
        string fileName = $"TransitionSequence_Turns{turnCount}_Seed{seed}.json";
        string outputPath = TestFileHelper.GetOutputPath(fileName);
        string referencePath = TestFileHelper.GetOutputPath(fileName, asReference: true);

        var transitions = ChessPolicySimulator.GenerateTransitionSequence(turnCount, seed);
        string json = JsonSerializer.Serialize(transitions, new JsonSerializerOptions { WriteIndented = true });

        // Act
        TestFileHelper.SaveTextToFile(json, outputPath);

        // Assert
        Assert.True(File.Exists(referencePath), $"Reference file missing: {referencePath}");
        string referenceJson = File.ReadAllText(referencePath);
        Assert.Equal(referenceJson, json);
    }
}

/// <summary>
/// Provides deterministic simulation of chess transitions.
/// </summary>
internal static class ChessPolicySimulator
{
    public static List<TransitionRecord> GenerateTransitionSequence(int turnCount, int seed)
    {
        var rng = new Random(seed);
        var state = new ChessState();
        var transitions = new List<TransitionRecord>();

        for (int turn = 0; turn < turnCount; turn++)
        {
            var actions = state.GetAvailableActions().ToList();
            if (actions.Count == 0) break;

            var action = actions[rng.Next(actions.Count)];
            var nextState = state.Apply(action);

            transitions.Add(new TransitionRecord(turn, action.ToString()));
            state = nextState;
        }

        return transitions;
    }
}

internal record TransitionRecord(int Turn, string Action);
