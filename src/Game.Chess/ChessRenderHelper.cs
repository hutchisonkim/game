using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Game.Core;
using Game.Core.View;

namespace Game.Chess
{
    public static class ChessRenderHelper
    {
        public sealed record HelperAction(string Desc) : IAction
        {
            public string Description => Desc;
            public override string ToString() => Desc;
        }

        private sealed class HelperState : IState<HelperAction, HelperState>
        {
            public char[,] Board { get; }

            public HelperState(char[,] board)
            {
                Board = board ?? throw new ArgumentNullException(nameof(board));
            }

            public HelperState Clone() => new HelperState((char[,])Board.Clone());

            public HelperState Apply(HelperAction action) => Clone();
        }

        private sealed class HelperView : ChessView<HelperAction, HelperState, HelperView> { }

        public static byte[] RenderStatePng(string fen, int stateSize = 400)
        {
            if (string.IsNullOrWhiteSpace(fen)) throw new ArgumentNullException(nameof(fen));

            var board = ChessView<HelperAction, HelperState, HelperView>.ParseFen(fen);
            using var bmp = ChessView<HelperAction, HelperState, HelperView>.RenderBoardBitmap(board, stateSize);
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }

        public static byte[] RenderStatePngWithArrow(string fen, string action, System.Drawing.Color arrowColor, int stateSize = 400)
        {
            if (string.IsNullOrWhiteSpace(fen)) throw new ArgumentNullException(nameof(fen));

            var board = ChessView<HelperAction, HelperState, HelperView>.ParseFen(fen);
            using var bmp = ChessView<HelperAction, HelperState, HelperView>.RenderBoardBitmap(board, stateSize);
            using var outBmp = new Bitmap(bmp.Width, bmp.Height);
            using var g = Graphics.FromImage(outBmp);
            using var ms = new MemoryStream();

            g.DrawImage(bmp, 0, 0);

            // Draw arrow based on action
            if (!string.IsNullOrWhiteSpace(action))
            {
                int cell = Math.Max(4, stateSize / 8);
                var moves = new List<(int fromR, int fromF, int toR, int toF)>();

                (int r, int f)? ParseSquare(string sq)
                {
                    if (string.IsNullOrWhiteSpace(sq) || sq.Length < 2) return null;
                    char fileCh = sq[0];
                    char rankCh = sq[1];
                    int file = fileCh - 'a';
                    if (file < 0 || file > 7) return null;
                    if (!char.IsDigit(rankCh)) return null;
                    int rank = rankCh - '1';
                    if (rank < 0 || rank > 7) return null;
                    int boardR = 7 - rank;
                    return (boardR, file);
                }

                var tokens = action.Split([' ', '-', 'x', ':'], StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i + 1 < tokens.Length; i++)
                {
                    var a = ParseSquare(tokens[i]);
                    var b = ParseSquare(tokens[i + 1]);
                    if (a != null && b != null)
                    {
                        moves.Add((a.Value.r, a.Value.f, b.Value.r, b.Value.f));
                        break;
                    }
                }

                using var pen = new Pen(arrowColor, Math.Max(2, cell / 6)) { EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor };
                foreach (var (fromR, fromF, toR, toF) in moves)
                {
                    var fromCenter = new PointF(fromF * cell + cell / 2f, fromR * cell + cell / 2f);
                    var toCenter = new PointF(toF * cell + cell / 2f, toR * cell + cell / 2f);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.DrawLine(pen, fromCenter, toCenter);
                }
            }

            outBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }

        public static byte[] RenderTimelineGif(IEnumerable<string> fenFrames, IEnumerable<string>? actionTexts = null, int stateSize = 400)
        {
            if (fenFrames == null) throw new ArgumentNullException(nameof(fenFrames));
            var fens = fenFrames.ToArray();
            var actions = (actionTexts ?? Enumerable.Empty<string>()).ToArray();

            var history = new List<(HelperState state, HelperAction action)>();
            for (int i = 0; i < fens.Length; i++)
            {
                var board = ChessView<HelperAction, HelperState, HelperView>.ParseFen(fens[i]);
                var state = new HelperState(board);
                var action = new HelperAction(i < actions.Length ? actions[i] : string.Empty);
                history.Add((state, action));
            }

            var view = new HelperView();
            return view.RenderTimelineGif(history, stateSize);
        }
        static ChessRenderHelper()
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Rendering is only supported on Windows.");
            }
        }
    }
}