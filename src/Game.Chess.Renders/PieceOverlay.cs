using System.Drawing;

namespace Game.Chess.Renders
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    internal static class PieceOverlay
    {
        public static void DrawPieces(Graphics g, char[,] board, int cell, float opacity = 1.0f)
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
    }
}
