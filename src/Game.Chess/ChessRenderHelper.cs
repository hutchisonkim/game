using System;
using System.Collections.Generic;
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