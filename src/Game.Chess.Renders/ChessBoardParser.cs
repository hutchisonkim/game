using System;

namespace Game.Chess.Renders
{
    internal static class ChessBoardParser
    {
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

        private static char MapFenCharToSymbol(char ch)
        {
            bool isWhite = char.IsUpper(ch);
            char t = char.ToLowerInvariant(ch);
            return t switch
            {
                'k' => isWhite ? '\u2654' : '\u265A',
                'q' => isWhite ? '\u2655' : '\u265B',
                'r' => isWhite ? '\u2656' : '\u265C',
                'b' => isWhite ? '\u2657' : '\u265D',
                'n' => isWhite ? '\u2658' : '\u265E',
                'p' => isWhite ? '\u2659' : '\u265F',
                _ => ch,
            };
        }
    }
}
