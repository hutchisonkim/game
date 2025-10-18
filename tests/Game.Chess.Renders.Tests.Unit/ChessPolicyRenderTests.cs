//tests\Game.Chess.Renders.Tests.Unit\ChessPolicyRenderTests.cs

using Xunit;
using Game.Chess.Policy;

namespace Game.Chess.Renders.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Feature", "RenderAvailableActions")]
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class ChessPolicyRenderTests
{

    [Fact]
    public void RenderAvailableActions_Turns64Seed1234_MatchingGif() =>
        RenderAvailableActions_TurnsXSeedY_MatchingGif(64, 1234);
    [Fact]
    public void RenderAvailableActions_Turns64Seed2345_MatchingGif() =>
        RenderAvailableActions_TurnsXSeedY_MatchingGif(64, 2345);
    [Fact]
    public void RenderAvailableActions_Turns64Seed3456_MatchingGif() =>
        RenderAvailableActions_TurnsXSeedY_MatchingGif(64, 3456);

    private static void RenderAvailableActions_TurnsXSeedY_MatchingGif(int turnCount, int seed)
    {
        // Arrange
        string fileName = $"RenderAvailableActions_Turns{turnCount}Seed{seed}_MatchingGif.gif";

        // Act
        byte[] gifBytes = GenerateTimelineGif(seed: seed, turnCount: turnCount);

        // Assert
        Assert.NotNull(gifBytes);
        Assert.True(gifBytes.Length > 0);

        string outputPath = GetOutputPath(fileName);
        string referencePath = GetOutputPath(fileName, asReference: true);

        SaveGifToFile(gifBytes, outputPath);

        Assert.True(File.Exists(outputPath));
        Assert.True(File.Exists(referencePath));

        byte[] referenceGifBytes = ReadGifFromFile(referencePath);

        Assert.True(referenceGifBytes.SequenceEqual(gifBytes));
    }

    private static string GetOutputPath(string fileName, bool asReference = false)
    {
        string assemblyDir = Path.GetDirectoryName(typeof(RenderStatePngTests).Assembly.Location)!;
        string rootDir = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\.."));
        return Path.Combine(rootDir, asReference ? "TestResultsReference" : "TestResults", "Game.Chess.Renders", fileName);
    }

    private static void SaveGifToFile(byte[] gifBytes, string outputPath)
    {
        string? directory = Path.GetDirectoryName(outputPath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllBytes(outputPath, gifBytes);
    }

    private static byte[] ReadGifFromFile(string inputPath)
    {
        return File.ReadAllBytes(inputPath);
    }

    private static byte[] GenerateTimelineGif(int seed, int turnCount)
    {
        Random rng = new(seed);
        ChessView view = new();
        ChessState state = new();
        List<(ChessState fromState, ChessState toState, BaseAction action)> transitions = [];

        for (int turn = 0; turn < turnCount; turn++)
        {
            List<BaseAction> actions = [.. state.GetAvailableActions()];
            if (actions.Count == 0) break;

            BaseAction randomAction = actions[rng.Next(actions.Count)];
            ChessState nextState = state.Apply(randomAction);

            transitions.Add((state, nextState, randomAction));
            state = nextState;
        }

        // Act
        byte[] gifBytes = view.RenderTransitionSequenceGif(transitions, 200);
        return gifBytes;
    }
}
