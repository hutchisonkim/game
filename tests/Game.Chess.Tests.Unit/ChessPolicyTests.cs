// tests/Game.Chess.Tests.Unit/ChessPolicySimulationTests.cs

using Xunit;
using System.Text.Json;
using Game.Core.Tests.Unit;

namespace Game.Chess.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Feature", "PolicySimulation")]
public class ChessPolicySimulationTests
{
    [Theory]
    [InlineData(64, 1234)]
    [InlineData(64, 2345)]
    [InlineData(64, 3456)]
    public void ActionsTimeline_TurnsXSeedY_MatchesReference(int turnCount, int seed)
    {
        // Arrange
        string fileName = $"ActionsTimeline_Turns{turnCount}Seed{seed}_MatchesReference.json";
        string outputPath = TestFileHelper.GetOutputPath(fileName);
        string referencePath = TestFileHelper.GetOutputPath(fileName, asReference: true);

        List<string> actionsTimeline = ActionsTimeline.GenerateRandom(turnCount, seed);
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

    [Fact]
    public void ActionsTimeline_Turns0_MatchesReference()
    {
        // Arrange
        string fileName = $"ActionsTimeline_Turns0_MatchesReference.json";
        string outputPath = TestFileHelper.GetOutputPath(fileName);
        string referencePath = TestFileHelper.GetOutputPath(fileName, asReference: true);

        List<string> actionsTimeline = ActionsTimeline.GenerateInitial();
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
