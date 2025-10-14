using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using Game.Chess;

namespace Game.Chess.Pages.Tests.Unit
{
    public class RenderStatePngTests
    {
        private static string ToFen(ChessBoard board)
        {
            var sb = new StringBuilder();
            for (int boardRow = 0; boardRow < 8; boardRow++)
            {
                int empty = 0;
                for (int c = 0; c < 8; c++)
                {
                    var piece = board.Board[boardRow, c];
                    if (piece == null)
                    {
                        empty++;
                    }
                    else
                    {
                        if (empty > 0)
                        {
                            sb.Append(empty);
                            empty = 0;
                        }
                        char ch = piece.Type switch
                        {
                            PieceType.Pawn => 'p',
                            PieceType.Rook => 'r',
                            PieceType.Knight => 'n',
                            PieceType.Bishop => 'b',
                            PieceType.Queen => 'q',
                            PieceType.King => 'k',
                            _ => '?'
                        };
                        if (piece.Color == PieceColor.White) ch = char.ToUpper(ch);
                        sb.Append(ch);
                    }
                }
                if (empty > 0) sb.Append(empty);
                if (boardRow < 7) sb.Append('/');
            }
            return sb.ToString();
        }

        private static string SquareFromPosition(Position p)
        {
            char file = (char)('a' + p.Col);
            char rank = (char)('1' + (7 - p.Row));
            return string.Concat(file, rank);
        }
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

        [Fact]
        public void RenderTimelineGif_64Turns_ProducesGif()
        {
            // Arrange
            var boardState = new ChessBoard();
            var policy = new ChessRules();
            var fenFrames = new List<string>();
            var actionTexts = new List<string>();
            // Deterministic randomness
            var rng = new Random(54321);
            var current = boardState;

            for (int turn = 0; turn < 64; turn++)
            {
                var moves = policy.GetAvailableActions(current).ToList();
                if (moves.Count == 0) break;

                // pick a random move deterministically
                var mv = moves[rng.Next(moves.Count)];

                var toChess = current.Apply(mv);
                var actionStr = SquareFromPosition(mv.From) + SquareFromPosition(mv.To);
                actionTexts.Add(actionStr);
                fenFrames.Add(ToFen(toChess));

                // advance state
                current = toChess;
            }

            // Act
            byte[] gifBytes = ChessRenderHelper.RenderTimelineGif(fenFrames, actionTexts, 200);

            // Assert
            Assert.NotNull(gifBytes);
            Assert.True(gifBytes.Length > 0);

            // Save for manual inspection (optional)
            string assemblyDir = Path.GetDirectoryName(typeof(RenderStatePngTests).Assembly.Location)!;
            string rootDir = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\.."));
            string outputPath = Path.Combine(rootDir, "TestResults", "Pages", "RenderTimelineGif_64Turns_ProducesGif.gif");
            string? directory = Path.GetDirectoryName(outputPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllBytes(outputPath, gifBytes);

            Assert.True(File.Exists(outputPath));
        }
    }
}