using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using Game.Core;
using Game.Core.View;

namespace Game.Chess
{
    /// <summary>
    /// Provides simple textual rendering utilities for a chess board.
    /// </summary>
    public class ChessView<TAction, TState, TView> : IView<TAction, TState, TView>
        where TAction : IAction
        where TState : IState<TAction, TState>
        where TView : IView<TAction, TState, TView>
    {
        /// <summary>
        /// Renders a board represented as an 8x8 char array into a multiline string.
        /// Empty squares should be represented by '.' or '\0'.
        /// The returned string displays ranks 8..1 from top to bottom and files a..h left to right.
        /// </summary>
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

        /// <summary>
        /// Prints a board represented as an 8x8 char array to the console.
        /// </summary>
        public static void Print(char[,] board)
        {
            Console.Write(Render(board));
            Console.WriteLine();
        }

        /// <summary>
        /// Parses the piece placement portion of a FEN string into an 8x8 char array.
        /// Empty squares are set to '.'.
        /// Only the placement field is required (e.g. "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR").
        /// </summary>
        public static char[,] ParseFen(string fenPlacement)
        {
            if (string.IsNullOrWhiteSpace(fenPlacement)) throw new ArgumentNullException(nameof(fenPlacement));

            var board = new char[8, 8];
            var ranks = fenPlacement.Split('/');
            if (ranks.Length != 8) throw new ArgumentException("FEN placement must contain 8 ranks.", nameof(fenPlacement));

            for (int r = 0; r < 8; r++)
            {
                string rankStr = ranks[r];
                int file = 0;
                foreach (char ch in rankStr)
                {
                    if (file >= 8) throw new ArgumentException("Too many squares in rank.", nameof(fenPlacement));
                    if (char.IsDigit(ch))
                    {
                        int emptyCount = ch - '0';
                        for (int i = 0; i < emptyCount; i++)
                        {
                            board[r, file++] = '.';
                        }
                    }
                    else
                    {
                        board[r, file++] = ch;
                    }
                }

                if (file != 8) throw new ArgumentException("Not enough squares in rank.", nameof(fenPlacement));
            }

            return board;
        }

        /// <summary>
        /// Convenience: render a FEN placement string directly to a printable board string.
        /// </summary>
        public static string RenderFromFen(string fenPlacement)
        {
            var board = ParseFen(fenPlacement);
            return Render(board);
        }

        private static char[,] ExtractBoardFromState(object state)
        {
            ArgumentNullException.ThrowIfNull(state);
            if (state is char[,] arr)
            {
                if (arr.GetLength(0) == 8 && arr.GetLength(1) == 8) return arr;
                throw new ArgumentException("Board must be 8x8.", nameof(state));
            }

            var type = state.GetType();
            var boardProp = type.GetProperties().FirstOrDefault(p => p.PropertyType == typeof(char[,]));
            if (boardProp != null)
            {
                var val = boardProp.GetValue(state) as char[,];
                if (val != null) return val;
            }

            var fenProp = type.GetProperties().FirstOrDefault(p => p.PropertyType == typeof(string) && (p.Name.Equals("Fen", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("FenPlacement", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("FEN", StringComparison.OrdinalIgnoreCase)));
            if (fenProp != null)
            {
                var fen = fenProp.GetValue(state) as string;
                if (!string.IsNullOrWhiteSpace(fen)) return ParseFen(fen);
            }

            var s = state.ToString();
            if (!string.IsNullOrWhiteSpace(s) && s.Contains('/')) return ParseFen(s);

            throw new ArgumentException("Unable to extract board from state.", nameof(state));
        }
#pragma warning disable CA1416 // Validate platform compatibility

        private static Bitmap RenderBoardBitmap(char[,] board, int size)
        {
            int cell = Math.Max(4, size / 8);
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("Bitmap rendering is only supported on Windows.");

            var bmp = new Bitmap(cell * 8, cell * 8);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                for (int r = 0; r < 8; r++)
                {
                    for (int f = 0; f < 8; f++)
                    {
                        var rect = new Rectangle(f * cell, r * cell, cell, cell);
                        bool light = ((r + f) % 2 == 0);
                        using (var brush = new SolidBrush(light ? Color.Beige : Color.SaddleBrown))
                        {
                            g.FillRectangle(brush, rect);
                        }

                        char c = board[r, f];
                        if (c != '\0' && c != '.')
                        {
                            string s = c.ToString();
                            float fontSize = cell * 0.75f;
                            using var font = new Font(FontFamily.GenericSansSerif, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                            using var textBrush = new SolidBrush(Color.Black);
                            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                            g.DrawString(s, font, textBrush, rect, sf);
                        }
                    }
                }
            }
            return bmp;
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

            var b1 = ExtractBoardFromState(stateFrom);
            var b2 = ExtractBoardFromState(stateTo);

            using var left = RenderBoardBitmap(b1, stateSize);
            using var right = RenderBoardBitmap(b2, stateSize);
            using var ms = new MemoryStream();
            int gap = Math.Max(20, stateSize / 12);
            var outBmp = new Bitmap(left.Width + right.Width + gap, Math.Max(left.Height, right.Height));
            using (var g = Graphics.FromImage(outBmp))
            {
                g.Clear(Color.White);
                g.DrawImage(left, 0, 0);
                g.DrawImage(right, left.Width + gap, 0);

                if (action != null)
                {
                    string actionText = action?.ToString() ?? string.Empty;
                    float fontSize = Math.Max(10, gap * 0.6f);
                    using var font = new Font(FontFamily.GenericSansSerif, fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
                    using var brush = new SolidBrush(Color.Black);
                    using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    var rect = new Rectangle(left.Width, 0, gap, outBmp.Height);
                    g.DrawString(actionText, font, brush, rect, sf);
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

            var bitmaps = frames.Select(f => RenderBoardBitmap(ExtractBoardFromState(f.state), stateSize)).ToArray();
            var gifCodec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == "image/gif") ?? throw new InvalidOperationException("GIF codec not available.");
            using var first = bitmaps[0] ?? throw new InvalidOperationException("No frames available to render GIF.");
            using var ms = new MemoryStream();
            var ep = new EncoderParameters(1);
            ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
            first.Save(ms, gifCodec, ep);

            for (int i = 1; i < bitmaps.Length; i++)
            {
                var ep2 = new EncoderParameters(1);
                ep2.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);
                first.SaveAdd(bitmaps[i], ep2);
                bitmaps[i].Dispose();
            }

            var epFlush = new EncoderParameters(1);
            epFlush.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.Flush);
            first.SaveAdd(epFlush);
            return ms.ToArray();
        }

#pragma warning restore CA1416 // Validate platform compatibility
    }
}