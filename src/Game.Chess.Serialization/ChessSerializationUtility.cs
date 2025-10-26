using Game.Chess.Entity;
using Game.Chess.History;

namespace Game.Chess.Serialization;

public static class ChessSerializationUtility
{
    public static List<string> GenerateRandom(int turnCount, int seed, ChessPieceAttribute pieceAttributeOverride)
    {
        var rng = new Random(seed);
        var state = new ChessState(pieceAttributeOverride);
        var actionsTimeline = new List<string>();

        for (int turn = 0; turn < turnCount; turn++)
        {
            IEnumerable<ChessHistoryUtility.ChessActionCandidate> actionCandidates = state.GetActionCandidates();
            int count = actionCandidates.Count();
            if (count == 0) break;

            ChessHistoryUtility.ChessActionCandidate actionCandidate = actionCandidates.ElementAt(rng.Next(count));
            ChessState nextState = state.Apply(actionCandidate.Action);

            actionsTimeline.Add(SerializeAction(
                actionCandidate.Action.From.X,
                actionCandidate.Action.From.Y,
                actionCandidate.Action.To.X,
                actionCandidate.Action.To.Y
            ));

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
