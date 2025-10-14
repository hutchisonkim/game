using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Core.View;

namespace Game.Chess
{
    public static class ChessRenderHelper
    {
        private sealed class HelperState : IState<IAction, HelperState>
        {
            public char[,] Board { get; }

            public HelperState(char[,] board)
            {
                Board = board ?? throw new ArgumentNullException(nameof(board));
            }

            public HelperState Clone() => new HelperState((char[,])Board.Clone());

            public HelperState Apply(IAction action) => Clone();
        }

        private sealed class HelperView : ChessView<IAction, HelperState, HelperView> { }

        public static byte[] RenderStatePng(string fen, int stateSize = 400)
        {
            if (string.IsNullOrWhiteSpace(fen)) throw new ArgumentNullException(nameof(fen));

            var board = ChessView<IAction, HelperState, HelperView>.ParseFen(fen);
            using var bmp = ChessView<IAction, HelperState, HelperView>.RenderBoardBitmap(board, stateSize);
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
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