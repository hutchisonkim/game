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
                        board[r, file++] = MapFenCharToSymbol(ch);
                    }
                }

                if (file != 8) throw new ArgumentException("Not enough squares in rank.", nameof(fenPlacement));
            }

            return board;
        }

        /// <summary>
        /// Map a FEN piece letter (r,n,b,q,k,p or uppercase) to a Unicode chess symbol char.
        /// Uppercase letters map to white pieces, lowercase to black pieces.
        /// Unknown characters are returned unchanged.
        /// </summary>
        private static char MapFenCharToSymbol(char ch)
        {
            // White: uppercase, Black: lowercase
            bool isWhite = char.IsUpper(ch);
            char t = char.ToLowerInvariant(ch);
            return t switch
            {
                'k' => isWhite ? '\u2654' : '\u265A', // King
                'q' => isWhite ? '\u2655' : '\u265B', // Queen
                'r' => isWhite ? '\u2656' : '\u265C', // Rook
                'b' => isWhite ? '\u2657' : '\u265D', // Bishop
                'n' => isWhite ? '\u2658' : '\u265E', // Knight
                'p' => isWhite ? '\u2659' : '\u265F', // Pawn
                _ => ch,
            };
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

        private static Bitmap RenderBoardBitmap(char[,] board, int size, bool drawPieces = true)
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
                        if (drawPieces)
                        {
                            char c = board[r, f];
                            if (c != '\0' && c != '.')
                            {
                                string s = c.ToString();
                                float fontSize = cell * 0.75f;
                                // Prefer a font that contains chess glyphs on Windows; fall back to GenericSansSerif.
                                Font fontToUse;
                                try
                                {
                                    fontToUse = new Font("Segoe UI Symbol", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                                }
                                catch
                                {
                                    fontToUse = new Font(FontFamily.GenericSansSerif, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                                }

                                using (fontToUse)
                                {
                                    using var textBrush = new SolidBrush(Color.Black);
                                    using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                                    g.DrawString(s, fontToUse, textBrush, rect, sf);
                                }
                            }
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

            var bFrom = ExtractBoardFromState(stateFrom);
            var bTo = ExtractBoardFromState(stateTo);

            int cell = Math.Max(4, stateSize / 8);
            // Render background board only (no pieces)
            using var boardBg = RenderBoardBitmap(bFrom, stateSize, drawPieces: false);
            using var outBmp = new Bitmap(boardBg.Width, boardBg.Height);
            using var g = Graphics.FromImage(outBmp);
            using var ms = new MemoryStream();

            g.Clear(Color.White);
            g.DrawImage(boardBg, 0, 0);

            // Helper to draw pieces from a board with specified opacity
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

            // Draw 'from' pieces faded, then 'to' pieces fully opaque
            DrawPieces(bFrom, 0.35f);
            DrawPieces(bTo, 1.0f);

            // Draw arrow from source to destination based on action.ToString() if possible
            if (action != null)
            {
                // Try to parse UCI-like notation (e.g., e2e4) or algebraic with hyphen
                var text = action.ToString() ?? string.Empty;
                // Accept patterns like e2e4, e7-e5, e2-e4
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
                    // Our internal board indexing uses ranks 0..7 top-to-bottom where 0 == rank8 in display
                    int boardR = 7 - rank; // convert '1' -> row 7, '8' -> row 0
                    return (boardR, file);
                }

                // Simple parse: find patterns of two squares
                var tokens = text.Split([' ', '-', 'x', ':'], StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i + 1 < tokens.Length; i++)
                {
                    var a = ParseSquare(tokens[i]);
                    var b = ParseSquare(tokens[i + 1]);
                    if (a != null && b != null)
                    {
                        moves.Add((a.Value.r, a.Value.f, b.Value.r, b.Value.f));
                        break; // only first match
                    }
                }

                // If no parse from action string, attempt to diff boards: find moved piece(s)
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

                    // Heuristic: pair by piece type (ignoring color) or if single from/to
                    if (fromSquares.Count == 1 && toSquares.Count == 1)
                    {
                        moves.Add((fromSquares[0].r, fromSquares[0].f, toSquares[0].r, toSquares[0].f));
                    }
                    else if (fromSquares.Count > 0 && toSquares.Count > 0)
                    {
                        // try pair by same char
                        foreach (var fs in fromSquares)
                        {
                            var match = toSquares.FirstOrDefault(ts => ts.c == fs.c);
                            if (match != default)
                            {
                                moves.Add((fs.r, fs.f, match.r, match.f));
                                break;
                            }
                        }
                    }
                }

                // Draw arrows for discovered moves
                using var pen = new Pen(Color.Red, Math.Max(2, cell / 6)) { EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor };
                foreach (var mv in moves)
                {
                    var fromCenter = new PointF(mv.fromF * cell + cell / 2f, mv.fromR * cell + cell / 2f);
                    var toCenter = new PointF(mv.toF * cell + cell / 2f, mv.toR * cell + cell / 2f);
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

            var bitmaps = frames.Select(f => RenderBoardBitmap(ExtractBoardFromState(f.state), stateSize)).ToArray();
            var gifCodec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == "image/gif") ?? throw new InvalidOperationException("GIF codec not available.");
            using var first = bitmaps[0] ?? throw new InvalidOperationException("No frames available to render GIF.");
            using var ms = new MemoryStream();
            // Ensure the generated GIF loops forever (Netscape application extension: loop count 0 = infinite).
            // Create a PropertyItem instance and set the LoopCount (0x5101) to 0 (short). We use FormatterServices
            // to allocate a PropertyItem since it has no public constructor.
            var loopProp = (PropertyItem)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PropertyItem));
            loopProp.Id = 0x5101; // PropertyTagLoopCount
            loopProp.Type = 3; // SHORT
            var loopBytes = BitConverter.GetBytes((short)0); // 0 -> infinite loop
            loopProp.Value = loopBytes;
            loopProp.Len = loopBytes.Length;
            try { first.SetPropertyItem(loopProp); } catch { /* best-effort: ignore if setting property fails */ }
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

        public byte[] RenderMultiTransitionPng(IEnumerable<(TState stateFrom, TState stateTo, TAction action)> transitions, int stateSize = 400)
        {
            ArgumentNullException.ThrowIfNull(transitions);
            var items = transitions.ToArray();
            if (items.Length == 0) return Array.Empty<byte>();

            // Use the first 'from' state as the background template, otherwise fall back to first 'to'
            var baseState = items[0].stateFrom ?? items[0].stateTo;
            if (baseState == null) throw new ArgumentException("At least one state must be non-null.", nameof(transitions));

            var baseBoard = ExtractBoardFromState(baseState);
            int cell = Math.Max(4, stateSize / 8);
            using var boardBg = RenderBoardBitmap(baseBoard, stateSize, drawPieces: false);
            using var outBmp = new Bitmap(boardBg.Width, boardBg.Height);
            using var g = Graphics.FromImage(outBmp);
            using var ms = new MemoryStream();

            g.Clear(Color.White);
            g.DrawImage(boardBg, 0, 0);

            // For each transition, draw faded 'from' pieces, then opaque 'to' pieces, and arrow
            foreach (var (stateFrom, stateTo, action) in items)
            {
                var bFrom = stateFrom != null ? ExtractBoardFromState(stateFrom) : null;
                var bTo = stateTo != null ? ExtractBoardFromState(stateTo) : null;

                void DrawPiecesLocal(char[,]? board, float opacity)
                {
                    if (board == null) return;
                    for (int r = 0; r < 8; r++)
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

                // faded from, then to opaque
                DrawPiecesLocal(bFrom, 0.35f);
                DrawPiecesLocal(bTo, 1.0f);

                // Draw arrow for this transition (use same parsing/diff heuristics as single transition)
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
                        if (bFrom != null && bTo != null)
                        {
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
                        }

                        if (fromSquares.Count == 1 && toSquares.Count == 1)
                        {
                            moves.Add((fromSquares[0].r, fromSquares[0].f, toSquares[0].r, toSquares[0].f));
                        }
                        else if (fromSquares.Count > 0 && toSquares.Count > 0)
                        {
                            foreach (var fs in fromSquares)
                            {
                                var match = toSquares.FirstOrDefault(ts => ts.c == fs.c);
                                if (match != default)
                                {
                                    moves.Add((fs.r, fs.f, match.r, match.f));
                                    break;
                                }
                            }
                        }
                    }

                    using var pen = new Pen(Color.Red, Math.Max(2, cell / 6)) { EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor };
                    foreach (var mv in moves)
                    {
                        var fromCenter = new PointF(mv.fromF * cell + cell / 2f, mv.fromR * cell + cell / 2f);
                        var toCenter = new PointF(mv.toF * cell + cell / 2f, mv.toR * cell + cell / 2f);
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        g.DrawLine(pen, fromCenter, toCenter);
                    }
                }
            }

            outBmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }

#pragma warning restore CA1416 // Validate platform compatibility
    }
}