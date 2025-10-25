// using Xunit;

// using Game.Chess.Entity;
// using Game.Chess.History;
// namespace Game.Chess.Renders.Tests.Unit;

// [System.Runtime.Versioning.SupportedOSPlatform("windows")]
// [Trait("Category", "Unit")]
// [Trait("Feature", "StateTimelineRenderTests")]
// public class StateTimelineRenderTests
// {
//     [Fact]
//     public void RenderDetailedCellsPng_InitialSetup_ProducesPng()
//     {
//         // Arrange
//         var state = new ChessState(); // Initial chess position

//         // Act
//         var renderAttackedCells = true;
//         var renderThreatenedCells = false;
//         var renderCheckedCells = false;
//         var renderPinnedCells = true;
//         var renderBlockedCells = true;
//         var chessView = new ChessView(renderAttackedCells, renderThreatenedCells, renderCheckedCells, renderPinnedCells, renderBlockedCells);
//         byte[] pngBytes = chessView.RenderStatePng(state, 400);

//         // Assert
//         Assert.NotNull(pngBytes);
//         Assert.True(pngBytes.Length > 0);

//         // Save for manual inspection (optional)
//         string assemblyDir = Path.GetDirectoryName(typeof(RenderStatePngTests).Assembly.Location)!;
//         string rootDir = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\.."));
//         string outputPath = Path.Combine(rootDir, "TestResults", "Renders", "RenderDetailedCellsPng_InitialSetup_ProducesPng.png");
//         string? directory = Path.GetDirectoryName(outputPath);
//         if (directory != null)
//         {
//             Directory.CreateDirectory(directory);
//         }
//         File.WriteAllBytes(outputPath, pngBytes);

//         Assert.True(File.Exists(outputPath));
//     }

//     [Fact]
//     [Trait("Feature", "PngRendering")]
//     public void RenderDetailedCellsPng_InitialSetupB_ProducesPng()
//     {
//         // Arrange
//         var state = new ChessState(); // Initial chess position
//         state.UpsTurns = false;
//         var newState = state.Apply(new ChessAction(new ChessPosition(0, 1), new ChessPosition(0, 3))); // a2 to a4

//         // Act
//         var renderAttackedCells = true;
//         var renderThreatenedCells = false;
//         var renderCheckedCells = false;
//         var renderPinnedCells = true;
//         var renderBlockedCells = true;
//         var chessView = new ChessView(renderAttackedCells, renderThreatenedCells, renderCheckedCells, renderPinnedCells, renderBlockedCells);
//         byte[] pngBytes = chessView.RenderStatePng(newState, 400);

//         // Assert
//         Assert.NotNull(pngBytes);
//         Assert.True(pngBytes.Length > 0);

//         // Save for manual inspection (optional)
//         string assemblyDir = Path.GetDirectoryName(typeof(RenderStatePngTests).Assembly.Location)!;
//         string rootDir = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\.."));
//         string outputPath = Path.Combine(rootDir, "TestResults", "Renders", "RenderDetailedCellsPng_InitialSetupB_ProducesPng.png");
//         string? directory = Path.GetDirectoryName(outputPath);
//         if (directory != null)
//         {
//             Directory.CreateDirectory(directory);
//         }
//         File.WriteAllBytes(outputPath, pngBytes);

//         Assert.True(File.Exists(outputPath));
//     }

//     [Fact]
//     [Trait("Feature", "PngRendering")]
//     public void RenderDetailedCellsPng_InitialSetupC_ProducesPng()
//     {
//         // Arrange
//         var state = new ChessState(); // Initial chess position
//         state.UpsTurns = false;
//         var newState = state.Apply(new ChessAction(new ChessPosition(5, 0), new ChessPosition(5, 3))); // f1 to f4

//         // Act
//         var renderAttackedCells = true;
//         var renderThreatenedCells = false;
//         var renderCheckedCells = false;
//         var renderPinnedCells = true;
//         var renderBlockedCells = true;
//         var chessView = new ChessView(renderAttackedCells, renderThreatenedCells, renderCheckedCells, renderPinnedCells, renderBlockedCells);
//         byte[] pngBytes = chessView.RenderStatePng(newState, 400);

//         // Assert
//         Assert.NotNull(pngBytes);
//         Assert.True(pngBytes.Length > 0);

//         // Save for manual inspection (optional)
//         string assemblyDir = Path.GetDirectoryName(typeof(RenderStatePngTests).Assembly.Location)!;
//         string rootDir = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\.."));
//         string outputPath = Path.Combine(rootDir, "TestResults", "Renders", "RenderDetailedCellsPng_InitialSetupC_ProducesPng.png");
//         string? directory = Path.GetDirectoryName(outputPath);
//         if (directory != null)
//         {
//             Directory.CreateDirectory(directory);
//         }
//         File.WriteAllBytes(outputPath, pngBytes);

//         Assert.True(File.Exists(outputPath));
//     }

//     [Fact]
//     [Trait("Feature", "PngRendering")]
//     public void RenderDetailedCellsPng_InitialSetupD_ProducesPng()
//     {
//         // Arrange
//         var state = new ChessState(); // Initial chess position
//         state.UpsTurns = false;
//         var newState = state.Apply(new ChessAction(new ChessPosition(3, 0), new ChessPosition(3, 3))); // d1 to d4

//         // Act
//         var renderAttackedCells = true;
//         var renderThreatenedCells = false;
//         var renderCheckedCells = false;
//         var renderPinnedCells = true;
//         var renderBlockedCells = true;
//         var chessView = new ChessView(renderAttackedCells, renderThreatenedCells, renderCheckedCells, renderPinnedCells, renderBlockedCells);
//         byte[] pngBytes = chessView.RenderStatePng(newState, 400);

//         // Assert
//         Assert.NotNull(pngBytes);
//         Assert.True(pngBytes.Length > 0);

//         // Save for manual inspection (optional)
//         string assemblyDir = Path.GetDirectoryName(typeof(RenderStatePngTests).Assembly.Location)!;
//         string rootDir = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\.."));
//         string outputPath = Path.Combine(rootDir, "TestResults", "Renders", "RenderDetailedCellsPng_InitialSetupD_ProducesPng.png");
//         string? directory = Path.GetDirectoryName(outputPath);
//         if (directory != null)
//         {
//             Directory.CreateDirectory(directory);
//         }
//         File.WriteAllBytes(outputPath, pngBytes);

//         Assert.True(File.Exists(outputPath));
//     }
//     [Fact]
//     [Trait("Feature", "PngRendering")]
//     public void RenderDetailedCellsPng_InitialSetupE_ProducesPng()
//     {
//         // Arrange
//         var state = new ChessState(); // Initial chess position
//         state.UpsTurns = false;
//         var newState = state.Apply(new ChessAction(new ChessPosition(4, 0), new ChessPosition(4, 3))); // e1 to e4

//         // Act
//         var renderAttackedCells = true;
//         var renderThreatenedCells = false;
//         var renderCheckedCells = false;
//         var renderPinnedCells = true;
//         var renderBlockedCells = true;
//         var chessView = new ChessView(renderAttackedCells, renderThreatenedCells, renderCheckedCells, renderPinnedCells, renderBlockedCells);
//         byte[] pngBytes = chessView.RenderStatePng(newState, 400);

//         // Assert
//         Assert.NotNull(pngBytes);
//         Assert.True(pngBytes.Length > 0);

//         // Save for manual inspection (optional)
//         string assemblyDir = Path.GetDirectoryName(typeof(RenderStatePngTests).Assembly.Location)!;
//         string rootDir = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\.."));
//         string outputPath = Path.Combine(rootDir, "TestResults", "Renders", "RenderDetailedCellsPng_InitialSetupE_ProducesPng.png");
//         string? directory = Path.GetDirectoryName(outputPath);
//         if (directory != null)
//         {
//             Directory.CreateDirectory(directory);
//         }
//         File.WriteAllBytes(outputPath, pngBytes);

//         Assert.True(File.Exists(outputPath));
//     }
// }
