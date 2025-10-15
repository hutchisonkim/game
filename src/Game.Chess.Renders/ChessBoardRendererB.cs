using System.Drawing;

namespace Game.Chess.RendersB;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal static class ChessBoardStamps
{
    // ─────────────────────────────────────────────────────────────
    // MAIN ENTRY POINT — returns a ready board layer with pieces
    // ─────────────────────────────────────────────────────────────
    internal static Bitmap StampBoard(PolicyB.ChessBoard state, int boardSize, bool includePieces = true)
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
    internal static void StampPieces(Graphics g, PolicyB.Piece?[,] board, int cell)
    {
        for (int r = 0; r < 8; r++)
            for (int f = 0; f < 8; f++)
            {
                var piece = board[r, f];
                if (piece is not null)
                {
                    var rect = new Rectangle(f * cell, r * cell, cell, cell);
                    StampPiece(g, rect, piece, cell);
                }
            }
    }

    internal static Bitmap StampPiecesLayer(PolicyB.Piece?[,] board, int cell)
    {
        var bmp = new Bitmap(cell * 8, cell * 8);
        using var g = Graphics.FromImage(bmp);
        StampPieces(g, board, cell);
        return bmp;
    }

    internal static void StampPiece(Graphics g, Rectangle rect, PolicyB.Piece piece, int cell)
    {
        string symbol = GetPieceSymbol(piece);
        float fontSize = cell * 0.75f;

        using var font = TryFont("Segoe UI Symbol", fontSize)
            ?? new Font(FontFamily.GenericSansSerif, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);

        using var brush = new SolidBrush(Color.Black);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.DrawString(symbol, font, brush, rect, sf);
    }

    // ─────────────────────────────────────────────────────────────
    // MOVES
    // ─────────────────────────────────────────────────────────────
    internal static void StampMoves(Graphics g, int cell, IEnumerable<(int fromR, int fromF, int toR, int toF)> moves, Color color)
    {
        using var pen = new Pen(color, Math.Max(2, cell / 6))
        {
            EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor
        };
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        foreach (var (fromR, fromF, toR, toF) in moves)
        {
            var fromCenter = new PointF(fromF * cell + cell / 2f, fromR * cell + cell / 2f);
            var toCenter = new PointF(toF * cell + cell / 2f, toR * cell + cell / 2f);
            g.DrawLine(pen, fromCenter, toCenter);
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

    private static string GetPieceSymbol(PolicyB.Piece piece)
    {
        return piece switch
        {
            { Type: PieceType.King, Color: PieceColor.White } => "♔",
            { Type: PieceType.Queen, Color: PieceColor.White } => "♕",
            { Type: PieceType.Rook, Color: PieceColor.White } => "♖",
            { Type: PieceType.Bishop, Color: PieceColor.White } => "♗",
            { Type: PieceType.Knight, Color: PieceColor.White } => "♘",
            { Type: PieceType.Pawn, Color: PieceColor.White } => "♙",
            { Type: PieceType.King, Color: PieceColor.Black } => "♚",
            { Type: PieceType.Queen, Color: PieceColor.Black } => "♛",
            { Type: PieceType.Rook, Color: PieceColor.Black } => "♜",
            { Type: PieceType.Bishop, Color: PieceColor.Black } => "♝",
            { Type: PieceType.Knight, Color: PieceColor.Black } => "♞",
            { Type: PieceType.Pawn, Color: PieceColor.Black } => "♟",
            _ => "?"
        };
    }
}
