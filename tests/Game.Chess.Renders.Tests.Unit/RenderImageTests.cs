using Xunit;
using System.Drawing;
using Game.Chess.Renders;

namespace Game.Chess.Renders.Tests.Unit;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
[Trait("Category", "Unit")]
public class RenderStatePngTests
{

    [Fact]
    [Trait("Feature", "PngRenderingB")]
    public void RenderStatePng_InitialSetup_ProducesPngB()
    {
        // Arrange
        var state = new Policy.ChessState(); // Initial chess position

        // Act
        var chessView = new ChessView();
        byte[] pngBytes = chessView.RenderStatePng(state, 400);

        // Assert
        Assert.NotNull(pngBytes);
        Assert.True(pngBytes.Length > 0);

        // Save for manual inspection (optional)
        string assemblyDir = Path.GetDirectoryName(typeof(RenderStatePngTests).Assembly.Location)!;
        string rootDir = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\.."));
        string outputPath = Path.Combine(rootDir, "TestResults", "Renders", "RenderStatePng_InitialSetup_ProducesPngB.png");
        string? directory = Path.GetDirectoryName(outputPath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllBytes(outputPath, pngBytes);

        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    [Trait("Feature", "PngRenderingB")]
    public void RenderTransitionPngs_RandomMove_ProducesTwoPngsWithArrowsB()
    {
        // Arrange
        var board = new Policy.ChessState();
        var moves = board.GetAvailableActions().ToList();
        Assert.True(moves.Count > 0);

        // Pick a random move deterministically
        var rng = new Random(12345);
        var move = moves[rng.Next(moves.Count)];
        var newBoard = board.Apply(move);

        // Act
        var chessView = new ChessView();
        byte[] beforePng = chessView.RenderPreTransitionPng(board, newBoard, move, 400);
        byte[] afterPng = chessView.RenderPostTransitionPng(board, newBoard, move, 400);

        // Assert
        Assert.NotNull(beforePng);
        Assert.True(beforePng.Length > 0);
        Assert.NotNull(afterPng);
        Assert.True(afterPng.Length > 0);

        // Save for manual inspection (optional)
        string assemblyDir = Path.GetDirectoryName(typeof(RenderStatePngTests).Assembly.Location)!;
        string rootDir = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\.."));
        string beforeOutputPath = Path.Combine(rootDir, "TestResults", "Renders", "RenderTransitionPngs_BeforeB.png");
        string afterOutputPath = Path.Combine(rootDir, "TestResults", "Renders", "RenderTransitionPngs_AfterB.png");
        string? directory = Path.GetDirectoryName(beforeOutputPath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllBytes(beforeOutputPath, beforePng);
        File.WriteAllBytes(afterOutputPath, afterPng);

        Assert.True(File.Exists(beforeOutputPath));
        Assert.True(File.Exists(afterOutputPath));
    }

    [Fact]
    [Trait("Feature", "GifRendering")]
    public void RenderTimelineGif_64Turns_ProducesGifUsingTransitionPngPairsB()
    {
        // Arrange
        var board = new Policy.ChessState();
        var transitions = new List<(Policy.ChessState fromState, Policy.ChessState toState, ChessAction action)>();
        var rng = new Random(12345);
        var chessView = new ChessView();
        var currentBoard = board;

        for (int turn = 0; turn < 64; turn++)
        {
            var moves = currentBoard.GetAvailableActions().ToList();
            if (moves.Count == 0) break;

            var move = moves[rng.Next(moves.Count)];
            var nextBoard = currentBoard.Apply(move);

            transitions.Add((currentBoard, nextBoard, move));
            currentBoard = nextBoard;
        }

        // Act
        byte[] gifBytes = chessView.RenderTransitionSequenceGif(transitions, 200);

        // Assert
        Assert.NotNull(gifBytes);
        Assert.True(gifBytes.Length > 0);

        // Save for manual inspection (optional)
        string assemblyDir = Path.GetDirectoryName(typeof(RenderStatePngTests).Assembly.Location)!;
        string rootDir = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\.."));
        string outputPath = Path.Combine(rootDir, "TestResults", "Renders", "RenderTimelineGif_64Turns_ProducesGifUsingTransitionPngPairsB.gif");
        string? directory = Path.GetDirectoryName(outputPath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllBytes(outputPath, gifBytes);

        Assert.True(File.Exists(outputPath));
        
    }
}
