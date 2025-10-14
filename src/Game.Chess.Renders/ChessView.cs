using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using Game.Core;
using Game.Core.Renders;

namespace Game.Chess.Renders
{
    /// <summary>
    /// Provides simple textual rendering utilities for a chess board.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public class ChessView<TAction, TState, TView> : IView<TAction, TState, TView>
        where TAction : IAction
        where TState : IState<TAction, TState>
        where TView : IView<TAction, TState, TView>
    {
        public static string Render(char[,] board)
        {
            ArgumentNullException.ThrowIfNull(board);
            if (board.GetLength(0) != 8 || board.GetLength(1) != 8) throw new ArgumentException("Board must be 8x8.", nameof(board));

            var sb = new StringBuilder();
            for (int rank = 0; rank < 8; rank++)
            {
                int displayRank = 8 - rank;
                sb.Append(displayRank);
                sb.Append(' ');

                for (int file = 0; file < 8; file++)
                {
                    char c = board[rank, file];
                    if (c == '\0' || c == '.') c = '.';
                    sb.Append(c);
                    if (file < 7) sb.Append(' ');
                }

                sb.AppendLine();
            }

            sb.Append("  a b c d e f g h");
            return sb.ToString();
        }

        public static void Print(char[,] board)
        {
            Console.Write(Render(board));
            Console.WriteLine();
        }

        public byte[] RenderPreTransitionPng(TState stateFrom, TState stateTo, TAction action, int stateSize = 400)
        {
            if (stateFrom == null) throw new ArgumentNullException(nameof(stateFrom));
            var board = ExtractBoardFromState(stateFrom);
            var cell = Math.Max(4, stateSize / 8);
            using var bmp = RenderBoardBitmap(board, stateSize);
            try
            {
                // Draw arrow based on action or diff to next state (pre-transition should show arrow from stateFrom to stateTo)
                var moves = MoveParser.ParseMovesFromAction(action?.ToString());

                // Fallback to diff
                if (moves.Count == 0 && stateTo != null)
                {
                    try
                    {
                        var nextBoard = ExtractBoardFromState(stateTo);
                        var fromSquares = new List<(int r, int f, char c)>();
                        var toSquares = new List<(int r, int f, char c)>();
                        for (int r = 0; r < 8; r++)
                            for (int f = 0; f < 8; f++)
                            {
                                char c1 = board[r, f];
                                char c2 = nextBoard[r, f];
                                if (c1 != c2)
                                {
                                    if (c1 != '\0' && c1 != '.') fromSquares.Add((r, f, c1));
                                    if (c2 != '\0' && c2 != '.') toSquares.Add((r, f, c2));
                                }
                            }

                        if (fromSquares.Count == 1 && toSquares.Count == 1)
                        {
                            moves.Add((fromSquares[0].r, fromSquares[0].f, toSquares[0].r, toSquares[0].f));
                        }
                        else if (fromSquares.Count > 0 && toSquares.Count > 0)
                        {
                            foreach (var (r, f, c) in fromSquares)
                            {
                                var match = toSquares.FirstOrDefault(ts => ts.c == c);
                                if (match != default)
                                {
                                    moves.Add((r, f, match.r, match.f));
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (moves.Count > 0)
                {
                    using var g = Graphics.FromImage(bmp);
                    using var pen = new Pen(Color.Red, Math.Max(2, cell / 6)) { EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor };
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    foreach (var (fromR, fromF, toR, toF) in moves)
                    {
                        var fromCenter = new PointF(fromF * cell + cell / 2f, fromR * cell + cell / 2f);
                        var toCenter = new PointF(toF * cell + cell / 2f, toR * cell + cell / 2f);
                        g.DrawLine(pen, fromCenter, toCenter);
                    }
                }
            }
            catch
            {
                // best-effort: ignore drawing failures
            }

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }

        public byte[] RenderPostTransitionPng(TState stateFrom, TState stateTo, TAction action, int stateSize = 400)
        {
            if (stateTo == null) throw new ArgumentNullException(nameof(stateTo));
            var board = ExtractBoardFromState(stateTo);
            var cell = Math.Max(4, stateSize / 8);
            using var bmp = RenderBoardBitmap(board, stateSize);
            try
            {
                // Draw arrow based on action or diff from stateFrom to stateTo
                var moves = MoveParser.ParseMovesFromAction(action?.ToString());

                if (moves.Count == 0 && stateFrom != null)
                {
                    try
                    {
                        var prevBoard = ExtractBoardFromState(stateFrom);
                        var fromSquares = new List<(int r, int f, char c)>();
                        var toSquares = new List<(int r, int f, char c)>();
                        for (int r = 0; r < 8; r++)
                            for (int f = 0; f < 8; f++)
                            {
                                char c1 = prevBoard[r, f];
                                char c2 = board[r, f];
                                if (c1 != c2)
                                {
                                    if (c1 != '\0' && c1 != '.') fromSquares.Add((r, f, c1));
                                    if (c2 != '\0' && c2 != '.') toSquares.Add((r, f, c2));
                                }
                            }

                        if (fromSquares.Count == 1 && toSquares.Count == 1)
                        {
                            moves.Add((fromSquares[0].r, fromSquares[0].f, toSquares[0].r, toSquares[0].f));
                        }
                        else if (fromSquares.Count > 0 && toSquares.Count > 0)
                        {
                            foreach (var (r, f, c) in fromSquares)
                            {
                                var match = toSquares.FirstOrDefault(ts => ts.c == c);
                                if (match != default)
                                {
                                    moves.Add((r, f, match.r, match.f));
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (moves.Count > 0)
                {
                    using var g = Graphics.FromImage(bmp);
                    using var pen = new Pen(Color.Red, Math.Max(2, cell / 6)) { EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor };
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    foreach (var (fromR, fromF, toR, toF) in moves)
                    {
                        var fromCenter = new PointF(fromF * cell + cell / 2f, fromR * cell + cell / 2f);
                        var toCenter = new PointF(toF * cell + cell / 2f, toR * cell + cell / 2f);
                        g.DrawLine(pen, fromCenter, toCenter);
                    }
                }
            }
            catch
            {
                // best-effort: ignore drawing failures
            }

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }

        public byte[] RenderTransitionGif(TState stateFrom, TState stateTo, TAction action, int stateSize = 400)
        {
            throw new NotImplementedException();
        }

#nullable enable
        public static char[,] ParseFen(string fenPlacement)
        {
            return ChessBoardParser.ParseFen(fenPlacement);
        }

        private static char[,] ExtractBoardFromState(object state)
        {
            return BoardExtractor.ExtractBoardFromState(state);
        }
#nullable restore
#pragma warning disable CA1416 // Validate platform compatibility

        public static Bitmap RenderBoardBitmap(char[,] board, int size, bool drawPieces = true)
        {
            return ChessBitmapRenderer.RenderBoardBitmap(board, size, drawPieces);
        }

        public byte[] RenderStatePng(TState state, int stateSize = 400)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var board = ExtractBoardFromState(state);
            using var bmp = RenderBoardBitmap(board, stateSize);
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }

        public byte[] RenderTransitionPng(TState stateFrom, TState stateTo, TAction action, int stateSize = 400)
        {
            if (stateFrom == null) throw new ArgumentNullException(nameof(stateFrom));
            if (stateTo == null) throw new ArgumentNullException(nameof(stateTo));

            var bFrom = ExtractBoardFromState(stateFrom);
            var bTo = ExtractBoardFromState(stateTo);

            int cell = Math.Max(4, stateSize / 8);
            using var boardBg = RenderBoardBitmap(bFrom, stateSize, drawPieces: false);
            using var outBmp = new Bitmap(boardBg.Width, boardBg.Height);
            using var g = Graphics.FromImage(outBmp);
            using var ms = new MemoryStream();

            g.Clear(Color.White);
            g.DrawImage(boardBg, 0, 0);

            void DrawPieces(char[,] board, float opacity)
            {
                for (int r = 0; r < 8; r++)
                {
                    for (int f = 0; f < 8; f++)
                    {
                        char c = board[r, f];
                        if (c == '\0' || c == '.') continue;
                        string s = c.ToString();
                        float fontSize = cell * 0.75f;
                        Font fontToUse;
                        try { fontToUse = new Font("Segoe UI Symbol", fontSize, FontStyle.Bold, GraphicsUnit.Pixel); }
                        catch { fontToUse = new Font(FontFamily.GenericSansSerif, fontSize, FontStyle.Bold, GraphicsUnit.Pixel); }

                        using (fontToUse)
                        {
                            using var textBrush = new SolidBrush(Color.FromArgb((int)(opacity * 255), Color.Black));
                            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                            var rect = new Rectangle(f * cell, r * cell, cell, cell);
                            g.DrawString(s, fontToUse, textBrush, rect, sf);
                        }
                    }
                }
            }

            DrawPieces(bFrom, 0.35f);
            DrawPieces(bTo, 1.0f);

            if (action != null)
            {
                var text = action.ToString() ?? string.Empty;
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

                var tokens = text.Split([' ', '-', 'x', ':'], StringSplitOptions.RemoveEmptyEntries);
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

                if (moves.Count == 0)
                {
                    var fromSquares = new List<(int r, int f, char c)>();
                    var toSquares = new List<(int r, int f, char c)>();
                    for (int r = 0; r < 8; r++)
                        for (int f = 0; f < 8; f++)
                        {
                            char c1 = bFrom[r, f];
                            char c2 = bTo[r, f];
                            if (c1 != c2)
                            {
                                if (c1 != '\0' && c1 != '.') fromSquares.Add((r, f, c1));
                                if (c2 != '\0' && c2 != '.') toSquares.Add((r, f, c2));
                            }
                        }

                    if (fromSquares.Count == 1 && toSquares.Count == 1)
                    {
                        moves.Add((fromSquares[0].r, fromSquares[0].f, toSquares[0].r, toSquares[0].f));
                    }
                    else if (fromSquares.Count > 0 && toSquares.Count > 0)
                    {
                        foreach (var (r, f, c) in fromSquares)
                        {
                            var match = toSquares.FirstOrDefault(ts => ts.c == c);
                            if (match != default)
                            {
                                moves.Add((r, f, match.r, match.f));
                                break;
                            }
                        }
                    }
                }

                using var pen = new Pen(Color.Red, Math.Max(2, cell / 6)) { EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor };
                foreach (var (fromR, fromF, toR, toF) in moves)
                {
                    var fromCenter = new PointF(fromF * cell + cell / 2f, fromR * cell + cell / 2f);
                    var toCenter = new PointF(toF * cell + cell / 2f, toR * cell + cell / 2f);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.DrawLine(pen, fromCenter, toCenter);
                }
            }

            outBmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }

        public byte[] RenderTimelinePng(IEnumerable<(TState state, TAction action)> history, int stateSize = 50)
        {
            ArgumentNullException.ThrowIfNull(history);
            var frames = history.ToArray();
            if (frames.Length == 0) return Array.Empty<byte>();

            var bitmaps = frames.Select(f => RenderBoardBitmap(ExtractBoardFromState(f.state), stateSize)).ToArray();
            int spacing = Math.Max(4, stateSize / 16);
            int width = bitmaps.Sum(b => b.Width) + spacing * (bitmaps.Length - 1);
            int height = bitmaps.Max(b => b.Height);

            using var outBmp = new Bitmap(width, height);
            using var g = Graphics.FromImage(outBmp);
            using var ms = new MemoryStream();
            g.Clear(Color.White);
            int x = 0;
            for (int i = 0; i < bitmaps.Length; i++)
            {
                g.DrawImage(bitmaps[i], x, 0);
                x += bitmaps[i].Width + spacing;
                bitmaps[i].Dispose();
            }

            outBmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }


        public byte[] RenderTimelineGif(IEnumerable<(TState state, TAction action)> history, int stateSize = 400)
        {
            ArgumentNullException.ThrowIfNull(history);
            var frames = history.ToArray();
            if (frames.Length == 0) return Array.Empty<byte>().ToArray();

            // Render each frame, draw arrow for the action (parse move or diff with next state), then compose GIF
            int cell = Math.Max(4, stateSize / 8);
            static (int r, int f)? ParseSquareLocal(string sq)
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

            var bitmaps = new List<Bitmap>();
            for (int i = 0; i < frames.Length; i++)
            {
                var (state, action) = frames[i];
                var board = ExtractBoardFromState(state);
                var bmp = RenderBoardBitmap(board, stateSize);
                try
                {
                    var moves = new List<(int fromR, int fromF, int toR, int toF)>();
                    if (action != null)
                    {
                        var text = action.ToString() ?? string.Empty;
                        var tokens = text.Split([' ', '-', 'x', ':'], StringSplitOptions.RemoveEmptyEntries);
                        for (int t = 0; t + 1 < tokens.Length; t++)
                        {
                            var a = ParseSquareLocal(tokens[t]);
                            var b = ParseSquareLocal(tokens[t + 1]);
                            if (a != null && b != null)
                            {
                                moves.Add((a.Value.r, a.Value.f, b.Value.r, b.Value.f));
                                break;
                            }
                        }

                        // Fallback: compact UCI-like strings like "e2e4" (no separator)
                        if (moves.Count == 0 && text.Length >= 4)
                        {
                            try
                            {
                                var aTxt = text.Substring(0, 2);
                                var bTxt = text.Substring(2, 2);
                                var a = ParseSquareLocal(aTxt);
                                var b = ParseSquareLocal(bTxt);
                                if (a != null && b != null)
                                {
                                    moves.Add((a.Value.r, a.Value.f, b.Value.r, b.Value.f));
                                }
                            }
                            catch { /* ignore */ }
                        }
                    }

                    // If no move parsed from action text, attempt to diff with next state's board
                    if (moves.Count == 0 && i + 1 < frames.Length)
                    {
                        var nextBoard = ExtractBoardFromState(frames[i + 1].state);
                        var fromSquares = new List<(int r, int f, char c)>();
                        var toSquares = new List<(int r, int f, char c)>();
                        for (int r = 0; r < 8; r++)
                            for (int f = 0; f < 8; f++)
                            {
                                char c1 = board[r, f];
                                char c2 = nextBoard[r, f];
                                if (c1 != c2)
                                {
                                    if (c1 != '\0' && c1 != '.') fromSquares.Add((r, f, c1));
                                    if (c2 != '\0' && c2 != '.') toSquares.Add((r, f, c2));
                                }
                            }

                        if (fromSquares.Count == 1 && toSquares.Count == 1)
                        {
                            moves.Add((fromSquares[0].r, fromSquares[0].f, toSquares[0].r, toSquares[0].f));
                        }
                        else if (fromSquares.Count > 0 && toSquares.Count > 0)
                        {
                            foreach (var (r, f, c) in fromSquares)
                            {
                                var match = toSquares.FirstOrDefault(ts => ts.c == c);
                                if (match != default)
                                {
                                    moves.Add((r, f, match.r, match.f));
                                    break;
                                }
                            }
                        }
                    }

                    if (moves.Count > 0)
                    {
                        using var g = Graphics.FromImage(bmp);
                        using var pen = new Pen(Color.Red, Math.Max(2, cell / 6)) { EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor };
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        foreach (var (fromR, fromF, toR, toF) in moves)
                        {
                            var fromCenter = new PointF(fromF * cell + cell / 2f, fromR * cell + cell / 2f);
                            var toCenter = new PointF(toF * cell + cell / 2f, toR * cell + cell / 2f);
                            g.DrawLine(pen, fromCenter, toCenter);
                        }
                    }
                }
                catch
                {
                    // best-effort: ignore drawing failures
                }

                bitmaps.Add(bmp);
            }

            // Delegate GIF composition to GifComposer
            return GifComposer.CombineBitmapsToGif(bitmaps);
        }

        public byte[] RenderTimelineGifUsingPngPairs(List<(byte[], byte[])> transitionPngPairs, int stateSize = 400)
        {
            if (transitionPngPairs == null || transitionPngPairs.Count == 0)
                throw new ArgumentNullException(nameof(transitionPngPairs));

            var bitmaps = new List<Bitmap>();
            foreach (var (fromPng, toPng) in transitionPngPairs)
            {
                try
                {
                    var fromStream = new MemoryStream(fromPng);
                    var toStream = new MemoryStream(toPng);
                    var fromBmp = new Bitmap(fromStream);
                    var toBmp = new Bitmap(toStream);

                    bitmaps.Add(fromBmp);
                    bitmaps.Add(toBmp);


                }
                catch
                {
                    // Ignore any invalid PNGs and continue
                }
            }

            // Delegate composition to GifComposer (works from PNG pair bytes)
            return GifComposer.CombinePngPairsToGif(transitionPngPairs.Select(p => (p.Item1, p.Item2)).ToList());
        }

        public byte[] RenderTransitionSequencePng(IEnumerable<(TState stateFrom, TState stateTo, TAction action)> transitions, int stateSize = 400)
        {
            if (transitions == null) return Array.Empty<byte>();
            foreach (var t in transitions)
            {
                if (t.stateTo != null) return RenderStatePng(t.stateTo, stateSize);
            }
            return Array.Empty<byte>();
        }

        public byte[] RenderTransitionSequenceGif(IEnumerable<(TState stateFrom, TState stateTo, TAction action)> transitions, int stateSize = 400)
        {
            if (transitions == null) throw new ArgumentNullException(nameof(transitions));
            var frames = transitions.ToArray();
            if (frames.Length == 0) return Array.Empty<byte>();

            var bitmaps = frames.Select(f => RenderBoardBitmap(ExtractBoardFromState(f.stateTo), stateSize)).ToList();
            return GifComposer.CombineBitmapsToGif(bitmaps);
        }
    }
}
