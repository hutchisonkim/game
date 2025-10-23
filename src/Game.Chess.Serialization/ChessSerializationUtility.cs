using Game.Chess.Entity;
using Game.Chess.History;

namespace Game.Chess.Serialization;

public static class ChessSerializationUtility
{
    public static List<string> GenerateRandom(int turnCount, int seed, ChessPieceAttribute pieceAttributeOverride)
    {
        var rng = new Random(seed);
        var state = new ChessState();
        var actionsTimeline = new List<string>();

        if (pieceAttributeOverride != ChessPieceAttribute.None)
            state.InitializeBoard(pieceAttributeOverride);
            
        for (int turn = 0; turn < turnCount; turn++)
        {
            IEnumerable<ChessHistoryUtility.ChessActionCandidate> actionCandidates = state.GetActionCandidates();
            int count = actionCandidates.Count();
            if (count == 0) break;

            ChessHistoryUtility.ChessActionCandidate actionCandidate = actionCandidates.ElementAt(rng.Next(count));
            ChessState nextState = state.Apply(actionCandidate.Action);

            // Use the serialization layer to convert the action into a string fragment.
            var from = actionCandidate.Action.From;
            var to = actionCandidate.Action.To;
            actionsTimeline.Add(SerializeAction(from.X, from.Y, to.X, to.Y));
            state = nextState;
        }

        return actionsTimeline;
    }

    public static List<string> GenerateInitial()
    {
        return GenerateInitial(ChessPiece.Empty);
    }
    public static List<string> GenerateInitial(ChessPiece pieceOverride)
    {
        ChessState state = new();
        var actions = new List<string>();
        ChessPiece[,] board = state.Board;
        for (int x = 0; x < 8; x++)
        {
            for (int y = 1; y < 8; y++)
            {
                ChessPiece piece = board[x, y];
                if (piece.IsEmpty) continue;
                piece = pieceOverride.IsEmpty ? piece : pieceOverride;
                string toPositionDescription = PositionToText(x, y);
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
    public static string PositionToText(int x, int y)
    {
        static string RowToRank(int r) => (8 - r).ToString();
        static char ColToFile(int c) => (char)('a' + c);
        return $"{ColToFile(x)}{RowToRank(y)}";
    }

    // Serialize a ChessAction as a simple fragment: "from-to:" where from/to are position texts.
    public static string SerializeAction(int fromX, int fromY, int toX, int toY)
        => $"{PositionToText(fromX, fromY)}:{PositionToText(toX, toY)}:";

    // Serialize an initial-setup fragment for a square with position and piece type description.
    // positionText should already be computed by the domain (e.g., Position.ToString()).
    public static string SerializeInitialSquare(string positionText, string pieceTypeDescription)
        => $":{positionText}:{pieceTypeDescription}";
}
