//tests\Game.Chess.Renders.Tests.Unit\ChessPolicyRenderTests.cs

using Xunit;
using Game.Chess.History;
using static Game.Chess.History.ChessHistoryUtility;
using Game.Chess.Entity;

namespace Game.Chess.Renders.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Feature", "RenderTimeline")]
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class ChessPolicyRenderTests
{
    [Theory]
    [InlineData(64, 1234, ChessPieceAttribute.None)]
    [InlineData(64, 2345, ChessPieceAttribute.None)]
    [InlineData(64, 3456, ChessPieceAttribute.None)]
    [InlineData(64, 1234, ChessPieceAttribute.Pawn)]
    [InlineData(64, 1234, ChessPieceAttribute.Rook)]
    [InlineData(64, 1234, ChessPieceAttribute.Knight)]
    [InlineData(64, 1234, ChessPieceAttribute.Bishop)]
    [InlineData(64, 1234, ChessPieceAttribute.Queen)]
    [InlineData(64, 1234, ChessPieceAttribute.King)]
    public void RenderActionsTimeline_TurnsXSeedY_MatchesReference(int turnCount, int seed, ChessPieceAttribute pieceAttributeOverride)
    {
        // Arrange
        string fileName = $"RenderActionsTimeline_Turns{turnCount}Seed{seed}Piece{pieceAttributeOverride}_MatchesReference.gif";

        // Act
        byte[] gifBytes = GenerateTimelineGif(seed: seed, turnCount: turnCount, pieceAttributeOverride: pieceAttributeOverride);

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

    private static byte[] GenerateTimelineGif(int seed, int turnCount, ChessPieceAttribute pieceAttributeOverride)
    {
        Random rng = new(seed);
        ChessView view = new();
        ChessState state = new();
        if (pieceAttributeOverride != ChessPieceAttribute.None)
            state.InitializeBoard(pieceAttributeOverride);
        List<(ChessState fromState, ChessState toState, ChessAction action)> transitions = [];

        for (int turn = 0; turn < turnCount; turn++)
        {
            List<ChessActionCandidate> actionCandidates = [.. state.GetActionCandidates()];
            if (actionCandidates.Count == 0) break;

            ChessActionCandidate randomActionCandidate = actionCandidates[rng.Next(actionCandidates.Count)];
            ChessState nextState = state.Apply(randomActionCandidate.Action);

            transitions.Add((state, nextState, randomActionCandidate.Action));
            state = nextState;
        }

        // Act
        byte[] gifBytes = view.RenderTransitionSequenceGif(transitions, 200);
        return gifBytes;
    }
    private static byte[] ReadTimelineGif(string path)
    {
        return File.ReadAllBytes(path);
    }
}
