using Xunit;
using System.Drawing;

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
    public void RenderStatePng_ValidFen_ProducesPng()
    {
        // Arrange
        string fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR"; // Initial chess position

        // Act
        byte[] pngBytes = ChessRenderHelper.RenderStatePng(fen);

        // Assert
        Assert.NotNull(pngBytes);
        Assert.True(pngBytes.Length > 0);

        // Save for manual inspection (optional)
        string assemblyDir = Path.GetDirectoryName(typeof(RenderStatePngTests).Assembly.Location)!;
        string rootDir = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\.."));
        string outputPath = Path.Combine(rootDir, "TestResults", "Renders", "RenderStatePng_ValidFen_ProducesPng.png");
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
        var board = new ChessBoard();
        var policy = new ChessRules();
        var moves = policy.GetAvailableActions(board).ToList();
        Assert.True(moves.Count > 0);

        // Pick a random move deterministically
        var rng = new Random(12345);
        var move = moves[rng.Next(moves.Count)];
        var actionStr = SquareFromPosition(move.From) + SquareFromPosition(move.To);

        // Act
        byte[] beforePng = ChessRenderHelper.RenderStatePngWithArrow(ChessRenderHelper.ToFen(board), actionStr, Color.Red, dotNotSolid: true);
        var newBoard = board.Apply(move);
        byte[] afterPng = ChessRenderHelper.RenderStatePngWithArrow(ChessRenderHelper.ToFen(newBoard), actionStr, Color.Red);

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
    [Trait("Feature", "GifRendering")]
    public void RenderTimelineGif_64Turns_ProducesGifUsingTransitionPngPairs()
    {
        // Arrange
        var boardState = new ChessBoard();
        var policy = new ChessRules();
        var transitionPngPairs = new List<(byte[], byte[])>();
        var rng = new Random(12345);
        var current = boardState;

        for (int turn = 0; turn < 64; turn++)
        {
            var moves = policy.GetAvailableActions(current).ToList();
            if (moves.Count == 0) break;

            var mv = moves[rng.Next(moves.Count)];
            var actionStr = SquareFromPosition(mv.From) + SquareFromPosition(mv.To);

            byte[] beforePng = ChessRenderHelper.RenderStatePngWithArrow(ChessRenderHelper.ToFen(current), actionStr, Color.OrangeRed);
            var toChess = current.Apply(mv);
            byte[] afterPng = ChessRenderHelper.RenderStatePngWithArrow(ChessRenderHelper.ToFen(toChess), actionStr, Color.Red);

            transitionPngPairs.Add((beforePng, afterPng));

            current = toChess;
        }

        // Act
        byte[] gifBytes = ChessRenderHelper.RenderTimelineGifUsingPngPairs(transitionPngPairs, 200);

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

}
