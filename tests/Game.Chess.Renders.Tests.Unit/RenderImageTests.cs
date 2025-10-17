using Xunit;
using System.Drawing;
using Game.Chess.Renders;

namespace Game.Chess.Renders.Tests.Unit;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
[Trait("Category", "Unit")]
public class RenderStatePngTests
{

    private static string SquareFromPosition(Position p)
    {
        char file = (char)('a' + p.Col);
        char rank = (char)('1' + (7 - p.Row));
        return string.Concat(file, rank);
    }

    [Fact]
    [Trait("Feature", "PngRendering")]
    public void RenderStatePng_InitialSetup_ProducesPng()
    {
        // Arrange
        var state = new ChessBoard_Old(); // Initial chess position

        // Act
        var chessView = new ChessView();
        byte[] pngBytes = chessView.RenderStatePng(state, 400);

        // Assert
        Assert.NotNull(pngBytes);
        Assert.True(pngBytes.Length > 0);

        // Save for manual inspection (optional)
        string assemblyDir = Path.GetDirectoryName(typeof(RenderStatePngTests).Assembly.Location)!;
        string rootDir = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\.."));
        string outputPath = Path.Combine(rootDir, "TestResults", "Renders", "RenderStatePng_InitialSetup_ProducesPng.png");
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
    public void RenderStatePng_InitialSetup_ProducesPngB()
    {
        // Arrange
        var state = new PolicyB.ChessBoard(); // Initial chess position

        // Act
        var chessView = new RendersB.ChessView();
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
    [Trait("Feature", "PngRendering")]
    public void RenderTransitionPngs_RandomMove_ProducesTwoPngsWithArrows()
    {
        // Arrange
        var board = new ChessBoard_Old();
        var policy = new ChessRules();
        var moves = policy.GetAvailableActions(board).ToList();
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
        string beforeOutputPath = Path.Combine(rootDir, "TestResults", "Renders", "RenderTransitionPngs_Before.png");
        string afterOutputPath = Path.Combine(rootDir, "TestResults", "Renders", "RenderTransitionPngs_After.png");
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
    [Trait("Feature", "PngRenderingB")]
    public void RenderTransitionPngs_RandomMove_ProducesTwoPngsWithArrowsB()
    {
        // Arrange
        var board = new PolicyB.ChessBoard();
        var moves = board.GetAvailableActions().ToList();
        Assert.True(moves.Count > 0);

        // Pick a random move deterministically
        var rng = new Random(12345);
        var move = moves[rng.Next(moves.Count)];
        var newBoard = board.Apply(move);

        // Act
        var chessView = new RendersB.ChessView();
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
    public void RenderTimelineGif_64Turns_ProducesGifUsingTransitionPngPairs()
    {
        // Arrange
        var board = new ChessBoard_Old();
        var policy = new ChessRules();
        var transitions = new List<(ChessBoard_Old stateFrom, ChessBoard_Old stateTo, BaseMove action)>();
        var rng = new Random(12345);
        var chessView = new ChessView();
        var currentBoard = board;

        for (int turn = 0; turn < 64; turn++)
        {
            var moves = policy.GetAvailableActions(currentBoard).ToList();
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
        string outputPath = Path.Combine(rootDir, "TestResults", "Renders", "RenderTimelineGif_64Turns_ProducesGifUsingTransitionPngPairs.gif");
        string? directory = Path.GetDirectoryName(outputPath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllBytes(outputPath, gifBytes);

        Assert.True(File.Exists(outputPath));
    }
    [Fact]
    [Trait("Feature", "GifRendering")]
    public void RenderTimelineGif_64Turns_ProducesGifUsingTransitionPngPairsB()
    {
        // Arrange
        var board = new PolicyB.ChessBoard();
        var transitions = new List<(PolicyB.ChessBoard stateFrom, PolicyB.ChessBoard stateTo, BaseMove action)>();
        var rng = new Random(12345);
        var chessView = new RendersB.ChessView();
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
