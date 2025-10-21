// src\Game.Chess.Serialization\SerializationUtility.cs

using Game.Chess.Entity;
using Game.Chess.History;

namespace Game.Chess.Serialization;

public static class ChessSerializationUtility
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

            // Use the serialization layer to convert the action into a string fragment.
            var from = pieceAction.ChessAction.From;
            var to = pieceAction.ChessAction.To;
            actionsTimeline.Add(SerializeAction(from.Row, from.Col, to.Row, to.Col));
            state = nextState;
        }

        return actionsTimeline;
    }

    public static List<string> GenerateInitial()
    {
        ChessState state = new();
        var actions = new List<string>();
        ChessPiece?[,] board = state.Board;
        for (int row = 0; row < 8; row++)
        {
            for (int col = 1; col < 8; col++)
            {
                ChessPiece? piece = board[row, col];
                if (piece == null) continue;
                string toPositionDescription = PositionToText(row, col);
                string pieceTypeDescription = PieceTypeDescription(piece.Attributes);
                actions.Add(SerializeInitialSquare(toPositionDescription, pieceTypeDescription));
            }
        }
        string actionsString = string.Join(";", actions);
        var actionsTimeline = new List<string> { actionsString };
        return actionsTimeline;
    }

    public static char ToFenChar(ChessPieceAttribute pieceAttributes)
    {
        char fenChar = pieceAttributes switch
        {
            ChessPieceAttribute t when (t & ChessPieceAttribute.Pawn) != 0 => 'P',
            ChessPieceAttribute t when (t & ChessPieceAttribute.Rook) != 0 => 'R',
            ChessPieceAttribute t when (t & ChessPieceAttribute.Knight) != 0 => 'N',
            ChessPieceAttribute t when (t & ChessPieceAttribute.Bishop) != 0 => 'B',
            ChessPieceAttribute t when (t & ChessPieceAttribute.Queen) != 0 => 'Q',
            ChessPieceAttribute t when (t & ChessPieceAttribute.King) != 0 => 'K',
            _ => throw new ArgumentOutOfRangeException(nameof(pieceAttributes))
        };

        bool isWhite = (pieceAttributes & ChessPieceAttribute.White) != 0;
        return isWhite ? fenChar : char.ToLower(fenChar);
    }

    // Return a short piece type description (FEN-like single-char string)
    public static string PieceTypeDescription(ChessPieceAttribute typeFlagValue)
        => ToFenChar(typeFlagValue).ToString();

    // Convert a domain Position (row,col) into a short chess coordinate like "e4".
    public static string PositionToText(int row, int col)
    {
        static string RowToRank(int r) => (8 - r).ToString();
        static char ColToFile(int c) => (char)('a' + c);
        return $"{ColToFile(col)}{RowToRank(row)}";
    }

    // Serialize a ChessAction as a simple fragment: "from-to:" where from/to are position texts.
    public static string SerializeAction(int fromRow, int fromCol, int toRow, int toCol)
        => $"{PositionToText(fromRow, fromCol)}:{PositionToText(toRow, toCol)}:";

    // Serialize an initial-setup fragment for a square with position and piece type description.
    // positionText should already be computed by the domain (e.g., Position.ToString()).
    public static string SerializeInitialSquare(string positionText, string pieceTypeDescription)
        => $":{positionText}:{pieceTypeDescription}";
}
