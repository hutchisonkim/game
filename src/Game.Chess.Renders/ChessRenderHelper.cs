using System.Drawing;
using System.Text;

namespace Game.Chess.Renders;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class ChessRenderHelper
{
    public static byte[] RenderStatePng(string fen, int stateSize = 400)
    {
        if (string.IsNullOrWhiteSpace(fen)) throw new ArgumentNullException(nameof(fen));

        var board = ChessView.ParseFen(fen);
        using var bmp = ChessView.RenderBoardBitmap(board, stateSize);
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    public static string ToFen(ChessBoard board)
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
        
    public static byte[] RenderStatePngWithArrow(string fen, string action, System.Drawing.Color arrowColor, int stateSize = 400, bool dotNotSolid = false)
    {
        if (string.IsNullOrWhiteSpace(fen)) throw new ArgumentNullException(nameof(fen));

        var board = ChessView.ParseFen(fen);
        using var bmp = ChessView.RenderBoardBitmap(board, stateSize);
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

            // Parse UCI-like notation (e.g., e2e4)
            if (action.Length == 4 && char.IsLetter(action[0]) && char.IsDigit(action[1]) && char.IsLetter(action[2]) && char.IsDigit(action[3]))
            {
                var a = ParseSquare(action.Substring(0, 2));
                var b = ParseSquare(action.Substring(2, 2));
                if (a != null && b != null)
                {
                    moves.Add((a.Value.r, a.Value.f, b.Value.r, b.Value.f));
                }
            }
            else
            {
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
            }

            using var pen = new Pen(arrowColor, Math.Max(2, cell / 6))
            {
                EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor,
                DashStyle = dotNotSolid ? System.Drawing.Drawing2D.DashStyle.Dot : System.Drawing.Drawing2D.DashStyle.Solid
            };
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

    public static byte[] RenderTimelineGifUsingPngPairs(List<(byte[], byte[])> transitionPngPairs, int v)
    {
        return ChessView.RenderTimelineGifUsingPngPairs(transitionPngPairs, v);
    }
}