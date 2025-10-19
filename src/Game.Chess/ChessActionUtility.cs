// tests/Game.Chess.Tests.Unit/ChessPolicySimulationTests.cs

using Game.Chess.Policy;

namespace Game.Chess;

public static class ActionsTimeline
{
    public static List<string> GenerateRandom(int turnCount, int seed)
    {
        var rng = new Random(seed);
        var state = new ChessState();
        var actionsTimeline = new List<string>();

        for (int turn = 0; turn < turnCount; turn++)
        {
            IEnumerable<ChessState.PieceAction> pieceActions = state.GetAvailableActionsDetailed().PieceMoves;
            if (pieceActions.Count() == 0) break;

            ChessState.PieceAction pieceAction = pieceActions.ElementAt(rng.Next(pieceActions.Count()));
            ChessState nextState = state.Apply(pieceAction.ChessAction);

            actionsTimeline.Add($"{pieceAction.ChessAction.Description}:");
            state = nextState;
        }

        return actionsTimeline;
    }

    public static List<string> GenerateInitial()
    {
        ChessState state = new();
        List<string> actions = [];
        Policy.Piece?[,] board = state.Board;
        for (int row = 0; row < 8; row++)
        {
            for (int col = 1; col < 8; col++)
            {
                Policy.Piece? piece = board[row, col];
                if (piece == null) continue;
                string toPositionDescription = new Position(row, col).ToString();
                string pieceTypeDescription = piece.PieceTypeDescription;
                actions.Add($":{toPositionDescription}:{pieceTypeDescription}");
            }
        }
        string actionsString = string.Join(";", actions);
        List<string> actionsTimeline = [actionsString];
        return actionsTimeline;
    }
}
