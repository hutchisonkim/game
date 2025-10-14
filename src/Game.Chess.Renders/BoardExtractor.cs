using System;
using System.Linq;

namespace Game.Chess.Renders
{
    internal static class BoardExtractor
    {
        public static char[,] ExtractBoardFromState(object state)
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
                if (!string.IsNullOrWhiteSpace(fen)) return ChessBoardParser.ParseFen(fen);
            }

            var s = state.ToString();
            if (!string.IsNullOrWhiteSpace(s) && s.Contains('/')) return ChessBoardParser.ParseFen(s);

            throw new ArgumentException("Unable to extract board from state.", nameof(state));
        }
    }
}
