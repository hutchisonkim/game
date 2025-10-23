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

        StampSquares(g, cell, boardSize);

        if (includePieces)
            StampPieces(g, state.Board, cell, boardSize);

        return bmp;
    }

    // ─────────────────────────────────────────────────────────────
    // CELLS
    // ─────────────────────────────────────────────────────────────
    internal static void StampSquares(Graphics g, int cell, int boardSize, Color? lightColor = null, Color? darkColor = null)
    {
        lightColor ??= Color.Beige;
        darkColor ??= Color.SaddleBrown;

        for (int x = 0; x < boardSize; x++)
            for (int y = 0; y < boardSize; y++)
            {
                bool light = (x + y) % 2 == 0;
                var rect = new Rectangle(x * cell, y * cell, cell, cell);
                var inverseRect = InverseRectangleVertically(rect, cell, boardSize);
                using var brush = new SolidBrush(light ? lightColor.Value : darkColor.Value);
                g.FillRectangle(brush, inverseRect);
            }
    }

    internal static Bitmap StampSquaresLayer(int cell, int boardSize, Color? lightColor = null, Color? darkColor = null)
    {
        var bmp = new Bitmap(cell * boardSize, cell * boardSize);
        using var g = Graphics.FromImage(bmp);
        StampSquares(g, cell, boardSize, lightColor, darkColor);
        return bmp;
    }

    // ─────────────────────────────────────────────────────────────
    // PIECES
    // ─────────────────────────────────────────────────────────────
    internal static void StampPieces(Graphics g, ChessPiece[,] board, int cell, int boardSize, float opacity = 1.0f)
    {
        for (int x = 0; x < boardSize; x++)
            for (int y = 0; y < boardSize; y++)
            {
                var piece = board[x, y];
                if (!piece.IsEmpty)
                {
                    var rect = new Rectangle(x * cell, y * cell, cell, cell);
                    var inverseRect = InverseRectangleVertically(rect, cell, boardSize);
                    StampPiece(g, inverseRect, piece, cell, opacity);
                }
            }
    }

    internal static Bitmap StampPiecesLayer(ChessPiece[,] board, int cell, int boardSize, float opacity = 1.0f)
    {
        var bmp = new Bitmap(cell * boardSize, cell * boardSize);
        using var g = Graphics.FromImage(bmp);
        StampPieces(g, board, cell, boardSize, opacity);
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
    internal static void StampMoves(Graphics g, int cell, IEnumerable<(int fromX, int fromY, int toX, int toY)> moves, Color color, bool lengthDrivesWidth = true)
    {
        // calculate the longest and shortest lines. shortest is 1 cell, longest is from one corner to the opposite corner
        var shortestLength = cell;
        var longestLength = (int)Math.Sqrt(2 * (cell * cell));
        var lowestWidth = Math.Max(2, cell / 10);
        var highestWidth = Math.Max(2, cell / 8);
        // var highestWidth = lowestWidth;
        var lowestColor = color;
        //highest is the same as color but a bit more bright
        var highestColor = Color.FromArgb(255, Math.Min(255, color.R + 5), Math.Min(255, color.G + 5), Math.Min(255, color.B + 5));
        // var highestColor = color;

        var linesToDraw = new List<(Pen pen, PointF from, PointF to, int width)>();
        foreach (var (fromX, fromY, toX, toY) in moves)
        {
            var length = (int)Math.Sqrt((fromY - toY) * (fromY - toY) + (fromX - toX) * (fromX - toX)) * cell;
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
                // EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
                StartCap = System.Drawing.Drawing2D.LineCap.Round
            };
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var fromCenter = new PointF(fromX * cell + cell / 2f, fromY * cell + cell / 2f);
            var toCenter = new PointF(toX * cell + cell / 2f, toY * cell + cell / 2f);
            // g.DrawLine(pen, fromCenter, toCenter);
            linesToDraw.Add((pen, fromCenter, toCenter, (int)lineWidth));
        }
        //sort by width descending
        linesToDraw = linesToDraw.OrderByDescending(l => l.width).ToList();
        foreach (var (pen, from, to, width) in linesToDraw)
        {
            var inverseFrom = InversePointVertically(from, 8, cell);
            var inverseTo = InversePointVertically(to, 8, cell);
            g.DrawLine(pen, inverseFrom, inverseTo);
        }
    }

    internal static Bitmap StampMovesLayer(int cell, IEnumerable<(int fromX, int fromY, int toX, int toY)> moves, Color color)
    {
        var bmp = new Bitmap(cell * 8, cell * 8);
        using var g = Graphics.FromImage(bmp);
        StampMoves(g, cell, moves, color, false);
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
    internal static void StampCells_InnerContour(Graphics g, int cell, int boardSize, IEnumerable<(int, int)> positions, Color color, int thickness, int distanceFromEdge = 0)
    {
        using var pen = new Pen(color, thickness)
        {
            LineJoin = System.Drawing.Drawing2D.LineJoin.MiterClipped,
            StartCap = System.Drawing.Drawing2D.LineCap.Flat,
            EndCap = System.Drawing.Drawing2D.LineCap.Flat
        };
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        foreach ((int X, int Y) position in positions)
        {
            (int X, int Y) inversePosition = (position.X, boardSize - 1 - position.Y);
            Rectangle drawRect = new(
                inversePosition.X * cell + thickness / 2 + distanceFromEdge,
                inversePosition.Y * cell + thickness / 2 + distanceFromEdge,
                cell - thickness - 2 * distanceFromEdge,
                cell - thickness - 2 * distanceFromEdge
            );
            g.DrawRectangle(pen, drawRect);

            using var brush = new SolidBrush(Color.FromArgb(150, color));
            g.FillRectangle(brush, drawRect);
        }
    }

    internal static Rectangle InverseRectangleVertically(Rectangle rectangle, int boardSizeInSquares, int cell)
    {
        int boardSizePx = boardSizeInSquares * cell;
        return new Rectangle(
            rectangle.X,
            boardSizePx - rectangle.Y - rectangle.Height,
            rectangle.Width,
            rectangle.Height
        );
    }

    internal static PointF InversePointVertically(PointF point, int boardSizeInSquares, int cell)
    {
        float boardSizePx = boardSizeInSquares * cell;
        return new PointF(point.X, boardSizePx - point.Y);
    }

    internal static (int fromX, int fromY, int toX, int toY) InversePositionVertically(
        (int fromX, int fromY, int toX, int toY) positions, int boardSizeInSquares)
    {
        return (
            positions.fromX,
            boardSizeInSquares - 1 - positions.fromY,
            positions.toX,
            boardSizeInSquares - 1 - positions.toY
        );
    }


}
