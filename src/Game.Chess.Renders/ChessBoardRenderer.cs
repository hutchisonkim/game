using System.Drawing;
using Game.Chess.History;
using Game.Chess.Entity;

namespace Game.Chess.Renders;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal static class ChessBoardStamps
{
    // ─────────────────────────────────────────────────────────────
    // MAIN ENTRY POINT — returns a ready board layer with pieces
    // ─────────────────────────────────────────────────────────────
    internal static Bitmap StampBoard(ChessState state, int boardSize, bool includePieces = true)
    {
        if (state is null)
            throw new ArgumentNullException(nameof(state));
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Bitmap rendering is only supported on Windows.");

        int cell = Math.Max(4, boardSize / 8);
        var bmp = new Bitmap(cell * 8, cell * 8);

        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);

        StampSquares(g, cell);

        if (includePieces)
            StampPieces(g, state.Board, cell);

        return bmp;
    }

    // ─────────────────────────────────────────────────────────────
    // CELLS
    // ─────────────────────────────────────────────────────────────
    internal static void StampSquares(Graphics g, int cell, Color? lightColor = null, Color? darkColor = null)
    {
        lightColor ??= Color.Beige;
        darkColor ??= Color.SaddleBrown;

        for (int r = 0; r < 8; r++)
            for (int f = 0; f < 8; f++)
            {
                bool light = (r + f) % 2 == 0;
                var rect = new Rectangle(f * cell, r * cell, cell, cell);
                using var brush = new SolidBrush(light ? lightColor.Value : darkColor.Value);
                g.FillRectangle(brush, rect);
            }
    }

    internal static Bitmap StampSquaresLayer(int cell, Color? lightColor = null, Color? darkColor = null)
    {
        var bmp = new Bitmap(cell * 8, cell * 8);
        using var g = Graphics.FromImage(bmp);
        StampSquares(g, cell, lightColor, darkColor);
        return bmp;
    }

    // ─────────────────────────────────────────────────────────────
    // PIECES
    // ─────────────────────────────────────────────────────────────
    internal static void StampPieces(Graphics g, ChessPiece?[,] board, int cell, float opacity = 1.0f)
    {
        for (int r = 0; r < 8; r++)
            for (int f = 0; f < 8; f++)
            {
                var piece = board[r, f];
                if (piece is not null)
                {
                    var y_flipped = (7 - r) * cell; // flip vertical to match board array mapping
                    var rect = new Rectangle(f * cell, y_flipped, cell, cell);
                    StampPiece(g, rect, piece, cell, opacity);
                }
            }
    }

    internal static Bitmap StampPiecesLayer(ChessPiece?[,] board, int cell, float opacity = 1.0f)
    {
        var bmp = new Bitmap(cell * 8, cell * 8);
        using var g = Graphics.FromImage(bmp);
        StampPieces(g, board, cell, opacity);
        return bmp;
    }

    internal static void StampPiece(Graphics g, Rectangle rect, ChessPiece piece, int cell, float opacity = 1.0f)
    {
        string symbol = GetPieceSymbol(piece);
        float fontSize = cell * 0.75f;

        using var font = TryFont("Segoe UI Symbol", fontSize)
            ?? new Font(FontFamily.GenericSansSerif, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);

        using var brush = new SolidBrush(Color.FromArgb((int)(255 * opacity), Color.Black));
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.DrawString(symbol, font, brush, rect, sf);
    }

    // ─────────────────────────────────────────────────────────────
    // MOVES
    // ─────────────────────────────────────────────────────────────
    internal static void StampMoves(Graphics g, int cell, IEnumerable<(int fromR, int fromF, int toR, int toF)> moves, Color color, bool lengthDrivesWidth = true)
    {
        // calculate the longest and shortest lines. shortest is 1 cell, longest is from one corner to the opposite corner
        var shortestLength = cell;
        var longestLength = (int)Math.Sqrt(2 * (cell * cell));
        var lowestWidth = Math.Max(2, cell / 10);
        var highestWidth = Math.Max(2, cell / 8);
        var lowestColor = color;
        //highest is the same as color but a bit more bright
        var highestColor = Color.FromArgb(255, Math.Min(255, color.R + 20), Math.Min(255, color.G + 20), Math.Min(255, color.B + 20));

        var linesToDraw = new List<(Pen pen, PointF from, PointF to, int width)>();
        foreach (var (fromR, fromF, toR, toF) in moves)
        {
            var length = (int)Math.Sqrt((fromR - toR) * (fromR - toR) + (fromF - toF) * (fromF - toF)) * cell;
            var traverse = (float)(length - shortestLength) / (longestLength - shortestLength);
            var lineWidth = lengthDrivesWidth
                ? lowestWidth + (int)((highestWidth - lowestWidth) * traverse)
                : highestWidth;
            var lineColor = lengthDrivesWidth
                ? Color.FromArgb(
                    255,
                    Math.Min(255, lowestColor.R + (int)((highestColor.R - lowestColor.R) * traverse)),
                    Math.Min(255, lowestColor.G + (int)((highestColor.G - lowestColor.G) * traverse)),
                    Math.Min(255, lowestColor.B + (int)((highestColor.B - lowestColor.B) * traverse)))
                : color;
            
            var pen = new Pen(lineColor, lineWidth)
            {
                EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor
            };
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var fromCenter = new PointF(fromF * cell + cell / 2f, fromR * cell + cell / 2f);
            var toCenter = new PointF(toF * cell + cell / 2f, toR * cell + cell / 2f);
            // g.DrawLine(pen, fromCenter, toCenter);
            linesToDraw.Add((pen, fromCenter, toCenter, (int)lineWidth));
        }
        //sort by width descending
        linesToDraw = linesToDraw.OrderByDescending(l => l.width).ToList();
        foreach (var (pen, from, to, width) in linesToDraw)
        {
            g.DrawLine(pen, from, to);
        }
    }

    internal static Bitmap StampMovesLayer(int cell, IEnumerable<(int fromR, int fromF, int toR, int toF)> moves, Color color)
    {
        var bmp = new Bitmap(cell * 8, cell * 8);
        using var g = Graphics.FromImage(bmp);
        StampMoves(g, cell, moves, color);
        return bmp;
    }

    // ─────────────────────────────────────────────────────────────
    // UTILS
    // ─────────────────────────────────────────────────────────────
    private static Font? TryFont(string name, float size)
    {
        try { return new Font(name, size, FontStyle.Bold, GraphicsUnit.Pixel); }
        catch { return null; }
    }

    private static string GetPieceSymbol(ChessPiece piece)
    {
        var t = piece.TypeFlag;
        if (piece.IsWhite)
        {
            if ((t & ChessPieceAttribute.King) != 0) return "♔";
            if ((t & ChessPieceAttribute.Queen) != 0) return "♕";
            if ((t & ChessPieceAttribute.Rook) != 0) return "♖";
            if ((t & ChessPieceAttribute.Bishop) != 0) return "♗";
            if ((t & ChessPieceAttribute.Knight) != 0) return "♘";
            if ((t & ChessPieceAttribute.Pawn) != 0) return "♙";
        }
        else
        {
            if ((t & ChessPieceAttribute.King) != 0) return "♚";
            if ((t & ChessPieceAttribute.Queen) != 0) return "♛";
            if ((t & ChessPieceAttribute.Rook) != 0) return "♜";
            if ((t & ChessPieceAttribute.Bishop) != 0) return "♝";
            if ((t & ChessPieceAttribute.Knight) != 0) return "♞";
            if ((t & ChessPieceAttribute.Pawn) != 0) return "♟";
        }
        return "?";
    }

    internal static void StampCells_InnerContour(Graphics g, int cell, List<(int, int, int, int)> positions, Color color, int thickness)
    {
        using var pen = new Pen(color, thickness)
        {
            LineJoin = System.Drawing.Drawing2D.LineJoin.MiterClipped,
            StartCap = System.Drawing.Drawing2D.LineCap.Flat,
            EndCap = System.Drawing.Drawing2D.LineCap.Flat
        };
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        foreach (var (fromR, fromF, toR, toF) in positions)
        {
            var rect = new Rectangle(fromF * cell + thickness / 2, fromR * cell + thickness / 2, cell - thickness, cell - thickness);
            g.DrawRectangle(pen, rect);
            //also fill the rectangle with a  semi-transparent version of the color
            using var brush = new SolidBrush(Color.FromArgb(150, color));
            g.FillRectangle(brush, rect);
        }
    }
}
