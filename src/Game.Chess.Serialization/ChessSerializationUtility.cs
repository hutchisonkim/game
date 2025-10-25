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

}
