using System;
using System.IO;
using Xunit;
using Game.Chess;

namespace Game.Chess.Pages.Tests.Unit
{
    public class RenderStatePngTests
    {
        [Fact]
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
            string outputPath = Path.Combine(rootDir, "TestResults", "Pages", "RenderStatePng_ValidFen_ProducesPng.png");
            string? directory = Path.GetDirectoryName(outputPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllBytes(outputPath, pngBytes);

            Assert.True(File.Exists(outputPath));
        }
    }
}