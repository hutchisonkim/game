using System.Drawing;
using Game.Chess.Entity;
using Game.Chess.History;
using Game.Core.Renders;
using static Game.Chess.History.ChessHistoryUtility;
namespace Game.Chess.Renders;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class ChessHistoryRender
{
    public ChessHistoryRender() { }

    public byte[] RenderThreatSequenceGif(IEnumerable<(ChessState fromState, ChessState toState, ChessActionCandidate candidate, bool selected)> attackingTransitions, int stateSize, bool anchorTip)
    {
        var boardSize = 8;

        var turnTransitionsDict = new Dictionary<int, List<(ChessState fromState, ChessState toState, ChessActionCandidate candidate, bool selected)>>();
        foreach (var transition in attackingTransitions)
        {
            int turn = transition.fromState.TurnCount;
            if (!turnTransitionsDict.ContainsKey(turn))
                turnTransitionsDict[turn] = [];
            turnTransitionsDict[turn].Add(transition);
        }
        var frames = new List<Bitmap>();
        var turns = turnTransitionsDict.Keys.OrderBy(t => t);
        foreach (var turn in turns)
        {
            var turnTransitions = turnTransitionsDict[turn];
            var firstTurnTransition = turnTransitions.First();

            var bmpFrom = ComposeBoard(firstTurnTransition.fromState, stateSize);

            StampAttackedCells(bmpFrom, turnTransitions.Select(t => t.candidate), stateSize);
            foreach (var turnTransition in turnTransitions)
            {
                StampMoveHighlight(bmpFrom, turnTransition.candidate.Action, Color.Orange, stateSize, 1.0f, anchorTip: anchorTip);
            }

            StampPieces(bmpFrom, firstTurnTransition.fromState.Board, Math.Max(4, stateSize / boardSize), boardSize, 1.0f);

            frames.Add(bmpFrom);
        }
        return RenderUtility.RenderGif(frames);
    }

    public byte[] RenderTransitionSequenceGif(
        IEnumerable<(ChessState fromState, ChessState toState, ChessActionCandidate candidate, bool selected)> transitions,
        int stateSize,
        bool anchorTip)
    {
        var boardSize = 8;
        var turnTransitionsDict = new Dictionary<int, List<(ChessState fromState, ChessState toState, ChessActionCandidate candidate, bool selected)>>();
        var turnCandidateTransitionsDict = new Dictionary<int, List<(ChessState fromState, ChessState toState, ChessActionCandidate candidate, bool selected)>>();
        foreach (var transition in transitions)
        {
            int turn = transition.fromState.TurnCount;
            if (transition.selected)
            {
                if (!turnTransitionsDict.ContainsKey(turn))
                    turnTransitionsDict[turn] = [];
                turnTransitionsDict[turn].Add(transition);
            }
            else
            {
                if (!turnCandidateTransitionsDict.ContainsKey(turn))
                    turnCandidateTransitionsDict[turn] = [];
                turnCandidateTransitionsDict[turn].Add(transition);
            }
        }

        var bitmaps = new List<Bitmap>();
        var turns = turnTransitionsDict.Keys.Union(turnCandidateTransitionsDict.Keys).Distinct().OrderBy(t => t);

        //print turns
        foreach (var turn in turns)
        {
            {//display all candidate transitions for this turn
                var turnCandidateTransitions = turnCandidateTransitionsDict.ContainsKey(turn) ? turnCandidateTransitionsDict[turn] : [];
                if (turnCandidateTransitions.Count != 0)
                {
                    var bmpFrom = ComposeBoard(turnCandidateTransitions.First().fromState, stateSize);
                    foreach (var turnCandidateTransition in turnCandidateTransitions)
                    {
                        StampMoveHighlight(bmpFrom, turnCandidateTransition.candidate.Action, Color.Orange, stateSize, 1.0f, anchorTip: anchorTip);
                    }
                    StampPieces(bmpFrom, turnCandidateTransitions.First().fromState.Board, Math.Max(4, stateSize / boardSize), boardSize, 1.0f);
                    bitmaps.Add(bmpFrom);
                }
            }
            {//display all candidate transitions for this turn + selected transition
                var turnTransitions = turnTransitionsDict.ContainsKey(turn) ? turnTransitionsDict[turn] : [];

                if (turnTransitions.Count == 0)
                {
                    Console.WriteLine($"*******************************No transitions for turn {turn}");
                    continue;
                }

                var turnCandidateTransitions = turnCandidateTransitionsDict.ContainsKey(turn) ? turnCandidateTransitionsDict[turn] : [];

                var bmpFrom = ComposeBoard(turnTransitions.First().fromState, stateSize);
                var bmpTo = ComposeBoard(turnTransitions.First().toState, stateSize);

                foreach (var turnCandidateTransition in turnCandidateTransitions)
                {
                    StampMoveHighlight(bmpFrom, turnCandidateTransition.candidate.Action, Color.Orange, stateSize, 1.0f, anchorTip: anchorTip);
                }

                foreach (var turnCandidateTransition in turnCandidateTransitions)
                {
                    StampMoveHighlight(bmpTo, turnCandidateTransition.candidate.Action, Color.Orange, stateSize, 1.0f, anchorTip: anchorTip);
                }

                var turnTransition = turnTransitions.First();

                StampMoveHighlight(bmpFrom, turnTransition.candidate.Action, Color.Red, stateSize, 1.0f, anchorTip: anchorTip);
                StampMoveHighlight(bmpTo, turnTransition.candidate.Action, Color.Red, stateSize, 1.0f, anchorTip: anchorTip);

                StampPieces(bmpFrom, turnTransition.fromState.Board, Math.Max(4, stateSize / boardSize), boardSize, 1.0f);
                StampPieces(bmpTo, turnTransition.toState.Board, Math.Max(4, stateSize / boardSize), boardSize, 1.0f);

                bitmaps.Add(bmpFrom);
                bitmaps.Add(bmpTo);
            }
        }


        return RenderUtility.RenderGif(bitmaps);
    }

    // ─────────────────────────────────────────────────────────────
    // COMPOSITION LAYER HELPERS
    // ─────────────────────────────────────────────────────────────

    private static Bitmap ComposeBoard(ChessState state, int stateSize)
    {
        int boardSize = 8;
        int cell = Math.Max(4, stateSize / boardSize);

        var baseLayer = ChessEntityRenderUtility.StampSquaresLayer(cell, boardSize);
        var pieceLayer = ChessEntityRenderUtility.StampPiecesLayer(state.Board, cell, boardSize);

        // Composite them
        using var g = Graphics.FromImage(baseLayer);
        g.DrawImageUnscaled(pieceLayer, 0, 0);

        return baseLayer;
    }

    private static void StampPieces(Bitmap bmp, ChessPiece[,] board, int cell, int boardSize, float opacity = 1.0f)
    {
        using var g = Graphics.FromImage(bmp);
        ChessEntityRenderUtility.StampPieces(g, board, cell, boardSize, opacity);
    }

    private static void StampMoveHighlight(Bitmap bmp, ChessAction move, Color color, int stateSize, float opacity, bool anchorTip)
    {
        var transparentColor = Color.FromArgb((int)(opacity * 255), color.R, color.G, color.B);
        int cell = Math.Max(4, stateSize / 8);
        using var g = Graphics.FromImage(bmp);
        var moveList = new List<(int, int, int, int)>
        {
            (move.From.X, move.From.Y, move.To.X, move.To.Y)
        };
        //invert positions on the y axis
        moveList = moveList.Select(p => (p.Item1, p.Item2, p.Item3, p.Item4)).ToList();
        ChessEntityRenderUtility.StampMoves(g, cell, moveList, transparentColor, false, anchorTip: anchorTip);
    }

    public static void StampAttackedCells(Bitmap bmp, IEnumerable<ChessActionCandidate> attackingCandidates, int stateSize)
    {
        int boardSize = 8;
        int cell = Math.Max(4, stateSize / boardSize);
        using var g = Graphics.FromImage(bmp);
        var positions = attackingCandidates.Select(c => (c.Action.To.X, c.Action.To.Y)).Distinct().ToList();
        ChessEntityRenderUtility.StampCells_InnerContour(g, cell, boardSize, positions, color: Color.Orange, thickness: 3);
        // for each attacked cell, draw an arrow from the attacker to the cell (c.AttackingMoves)
        //invert positions on the y axis
        var rawAttackingMovesList = attackingCandidates.Select(p => (p.Action.From.X, p.Action.From.Y, p.Action.To.X, p.Action.To.Y));
        ChessEntityRenderUtility.StampMoves(g, cell, rawAttackingMovesList, Color.Orange, false, anchorTip: false);
    }

}
