using Xunit;
using Game.Chess.Policy;

namespace Game.Chess.Renders.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Feature", "RenderAvailableActions")]
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class ChessPolicyRenderTests
{
    [Fact]
    public void RenderAvailableActions_Turns64Seed1234_ValidGif()
    {
        // Arrange
        string fileName = $"{nameof(RenderAvailableActions_Turns64Seed1234_ValidGif)}.gif";

        // Act
        byte[] gifBytes = GenerateTimelineGif(seed: 1234, turnCount: 64);

        // Assert
        Assert.NotNull(gifBytes);
        Assert.True(gifBytes.Length > 0);

        // Save for manual inspection (optional)
        string outputPath = SaveGifToFile(gifBytes, fileName);

        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void RenderAvailableActions_Turns64Seed2345_ValidGif()
    {
        // Arrange
        string fileName = $"{nameof(RenderAvailableActions_Turns64Seed2345_ValidGif)}.gif";

        // Act
        byte[] gifBytes = GenerateTimelineGif(seed: 2345, turnCount: 64);

        // Assert
        Assert.NotNull(gifBytes);
        Assert.True(gifBytes.Length > 0);

        // Save for manual inspection (optional)
        string outputPath = SaveGifToFile(gifBytes, fileName);

        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void RenderAvailableActions_Turns64Seed3456_ValidGif()
    {
        // Arrange
        string fileName = $"{nameof(RenderAvailableActions_Turns64Seed3456_ValidGif)}.gif";

        // Act
        byte[] gifBytes = GenerateTimelineGif(seed: 3456, turnCount: 64);

        // Assert
        Assert.NotNull(gifBytes);
        Assert.True(gifBytes.Length > 0);

        // Save for manual inspection (optional)
        string outputPath = SaveGifToFile(gifBytes, fileName);

        Assert.True(File.Exists(outputPath));
    }

    private static string SaveGifToFile(byte[] gifBytes, string fileName)
    {
        string assemblyDir = Path.GetDirectoryName(typeof(RenderStatePngTests).Assembly.Location)!;
        string rootDir = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\.."));
        string outputPath = Path.Combine(rootDir, "TestResults", "Game.Chess.Renders", fileName);
        string? directory = Path.GetDirectoryName(outputPath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllBytes(outputPath, gifBytes);
        return outputPath;
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
