using System;
using System.Collections.Generic;

namespace Game.Chess.Renders
{
    internal static class MoveParser
    {
        public static List<(int fromR, int fromF, int toR, int toF)> ParseMovesFromAction(string? actionText)
        {
            var moves = new List<(int fromR, int fromF, int toR, int toF)>();
            if (string.IsNullOrWhiteSpace(actionText)) return moves;

            static (int r, int f)? ParseSquareLocal(string sq)
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

            var text = actionText ?? string.Empty;
            var tokens = text.Split(new[] { ' ', '-', 'x', ':' }, StringSplitOptions.RemoveEmptyEntries);
            for (int t = 0; t + 1 < tokens.Length; t++)
            {
                var a = ParseSquareLocal(tokens[t]);
                var b = ParseSquareLocal(tokens[t + 1]);
                if (a != null && b != null)
                {
                    moves.Add((a.Value.r, a.Value.f, b.Value.r, b.Value.f));
                    break;
                }
            }

            if (moves.Count == 0 && text.Length >= 4)
            {
                try
                {
                    var aTxt = text.Substring(0, 2);
                    var bTxt = text.Substring(2, 2);
                    var a = ParseSquareLocal(aTxt);
                    var b = ParseSquareLocal(bTxt);
                    if (a != null && b != null) moves.Add((a.Value.r, a.Value.f, b.Value.r, b.Value.f));
                }
                catch { }
            }

            return moves;
        }
    }
}
