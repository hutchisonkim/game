namespace Game.Chess
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var state = new ChessBoard_Old();
            var policy = new ChessRules();
            var actions = policy.GetAvailableActions(state);

            var actionCount = 0;
            Console.WriteLine("Initial legal moves:");
            foreach (ChessMove? action in actions)
            {
                var pieceFrom = state.PieceAt(action.From);
                var pieceTo = state.PieceAt(action.To);
                
                Console.WriteLine($"#{actionCount}: {action.From} -> {action.To}");
                Console.WriteLine($"Piece from: {pieceFrom?.Color} {pieceFrom?.Type}");
                Console.WriteLine($"Piece to: {pieceTo?.Color} {pieceTo?.Type}");

                actionCount++;
            }
        }
    }
}
