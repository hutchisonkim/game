using System;
using System.Drawing;
#pragma warning disable CA1416 // Validate platform compatibility

namespace Game.Chess.Renders
{
    internal static class ChessBitmapRenderer
    {
        internal static Bitmap RenderBoardBitmap(char[,] board, int size, bool drawPieces = true)
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
                        bool light = (r + f) % 2 == 0;
                        using (var brush = new SolidBrush(light ? Color.Beige : Color.SaddleBrown))
                        {
                            g.FillRectangle(brush, rect);
                        }
                        if (drawPieces)
                        {
                            char c = board[r, f];
                            if (c != '\0' && c != '.')
                            {
                                DrawPiece(g, rect, c, cell);
                            }
                        }
                    }
                }
            }
            return bmp;
        }

        private static void DrawPiece(Graphics g, Rectangle rect, char c, int cell)
        {
            string s = c.ToString();
            float fontSize = cell * 0.75f;
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
