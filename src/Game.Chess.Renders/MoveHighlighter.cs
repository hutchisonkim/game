using System.Drawing;

namespace Game.Chess.Renders;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal static class MoveHighlighter
{
    /// <summary>
    /// Detect moves either from action text (via MoveParser) or by diffing two board states.
    /// </summary>
    public static List<(int fromR, int fromF, int toR, int toF)> DetectMoves(char[,]? fromBoard, char[,]? toBoard, string? actionText)
    {
        var moves = new List<(int fromR, int fromF, int toR, int toF)>();

        // Prefer explicit action text parsing
        try
        {
            var parsed = MoveParser.ParseMovesFromAction(actionText);
            if (parsed != null && parsed.Count > 0) return parsed;
        }
        catch { /* best effort */ }

        // If we have both boards, attempt to diff them
        if (fromBoard == null || toBoard == null) return moves;

        try
        {
            var fromSquares = new List<(int r, int f, char c)>();
            var toSquares = new List<(int r, int f, char c)>();
            for (int r = 0; r < 8; r++)
                for (int f = 0; f < 8; f++)
                {
                    char c1 = fromBoard[r, f];
                    char c2 = toBoard[r, f];
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
                    var match = toSquares.Find(ts => ts.c == c);
                    if (match != default)
                    {
                        moves.Add((r, f, match.r, match.f));
                        break;
                    }
                }
            }
        }
        catch { }

        return moves;
    }

    public static void DrawMoves(Graphics g, int cell, IEnumerable<(int fromR, int fromF, int toR, int toF)> moves, Color color)
    {
        using var pen = new Pen(color, Math.Max(2, cell / 6)) { EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor };
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        foreach (var (fromR, fromF, toR, toF) in moves)
        {
            var fromCenter = new PointF(fromF * cell + cell / 2f, fromR * cell + cell / 2f);
            var toCenter = new PointF(toF * cell + cell / 2f, toR * cell + cell / 2f);
            g.DrawLine(pen, fromCenter, toCenter);
        }
    }
}
