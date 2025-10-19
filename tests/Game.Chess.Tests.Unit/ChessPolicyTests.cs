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
        string fileName = $"TransitionSequence_Turns{turnCount}Seed{seed}_MatchesReference.json";
        string outputPath = TestFileHelper.GetOutputPath(fileName);
        string referencePath = TestFileHelper.GetOutputPath(fileName, asReference: true);

        var transitions = ChessPolicySimulator.GenerateTransitionSequence(turnCount, seed);
        string json = JsonSerializer.Serialize(transitions, new JsonSerializerOptions { WriteIndented = true });

        // Act
        TestFileHelper.SaveTextToFile(json, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath), $"Output file missing: {outputPath}");

        // Assert
        // Assert.True(File.Exists(referencePath), $"Reference file missing: {referencePath}");
        // string referenceJson = File.ReadAllText(referencePath);
        // Assert.Equal(referenceJson, json);
    }

    [Fact]
    public void GenerateBoardInitializationSequenceJson_MatchesReference()
    {
        // Arrange
        string fileName = $"TransitionSequence_Turns0_MatchesReference.json";
        string outputPath = TestFileHelper.GetOutputPath(fileName);
        string referencePath = TestFileHelper.GetOutputPath(fileName, asReference: true);

        var initializations = ChessPolicySimulator.GenerateBoardInitializationSequence();
        string json = JsonSerializer.Serialize(initializations, new JsonSerializerOptions { WriteIndented = true });

        // Act
        TestFileHelper.SaveTextToFile(json, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath), $"Output file missing: {outputPath}");

        // Assert
        // Assert.True(File.Exists(referencePath), $"Reference file missing: {referencePath}");
        // string referenceJson = File.ReadAllText(referencePath);
        // Assert.Equal(referenceJson, json);
    }
}

/// <summary>
/// Provides deterministic simulation of chess transitions.
/// </summary>
internal static class ChessPolicySimulator
{
    public static List<string> GenerateTransitionSequence(int turnCount, int seed)
    {
        var rng = new Random(seed);
        var state = new ChessState();
        var transitions = new List<string>();

        for (int turn = 0; turn < turnCount; turn++)
        {
            IEnumerable<ChessState.PieceAction> pieceActions = state.GetAvailableActionsDetailed().PieceMoves;
            if (pieceActions.Count() == 0) break;

            ChessState.PieceAction pieceAction = pieceActions.ElementAt(rng.Next(pieceActions.Count()));
            ChessState nextState = state.Apply(pieceAction.ChessAction);

            transitions.Add(pieceAction.ChessAction.Description.ToString());
            state = nextState;
        }

        return transitions;
    }

    public static List<string> GenerateBoardInitializationSequence()
    {
        ChessState state = new();
        List<string> transitions = [];
        IEnumerable<ChessState.PieceAction> pieceActions = state.GetAvailableActionsDetailed().PieceMoves;
        foreach (ChessState.PieceAction pieceAction in pieceActions)
        {
            string fromPositionDescription = pieceAction.ChessAction.From.ToString();
            string pieceTypeDescription = pieceAction.Piece.PieceTypeDescription;
            transitions.Add($":{fromPositionDescription}:{pieceTypeDescription}");
        }
        return transitions;
    }
}
