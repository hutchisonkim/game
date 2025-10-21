// src\Game.Chess.Serialization\ChessSerializerUtility.cs

using Game.Chess.Policy;

namespace Game.Chess.Serialization;

public static class GameSerializerUtility
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

            actionsTimeline.Add(ChessSerializer.SerializeActionDescription(pieceAction.ChessAction.Description));
            state = nextState;
        }

        return actionsTimeline;
    }

    public static List<string> GenerateInitial()
    {
        ChessState state = new();
        var actions = new List<string>();
        Piece?[,] board = state.Board;
        for (int row = 0; row < 8; row++)
        {
            for (int col = 1; col < 8; col++)
            {
                Piece? piece = board[row, col];
                if (piece == null) continue;
                string toPositionDescription = new Position(row, col).ToString();
                string pieceTypeDescription = ChessSerializer.PieceTypeDescription((int)piece.Attributes, piece.IsWhite);
                actions.Add(ChessSerializer.SerializeInitialSquare(toPositionDescription, pieceTypeDescription));
            }
        }
        string actionsString = string.Join(";", actions);
        var actionsTimeline = new List<string> { actionsString };
        return actionsTimeline;
    }
}
