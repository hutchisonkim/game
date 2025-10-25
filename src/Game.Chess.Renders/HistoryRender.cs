using System.Drawing;
using System.Drawing.Imaging;
using Game.Chess.Entity;
using Game.Chess.History;
using static Game.Chess.History.ChessHistoryUtility;
namespace Game.Chess.Renders;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class HistoryRender
{
    public HistoryRender()
    {
    }


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
        return HistoryRenderUtility.RenderGif(frames);
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


        return HistoryRenderUtility.RenderGif(bitmaps);
    }

    public byte[] RenderPreTransitionPng(ChessState fromState, ChessState toState, ChessAction action, int stateSize, bool anchorTip)
    {
        var boardSize = 8;
        using var bmp = ComposeBoard(fromState, stateSize);
        StampMoveHighlight(bmp, action, Color.OrangeRed, stateSize, 1.0f, anchorTip: anchorTip);
        StampPieces(bmp, fromState.Board, Math.Max(4, stateSize / 8), boardSize, 1.0f);

        return ToPng(bmp);
    }

    public byte[] RenderPostTransitionPng(ChessState fromState, ChessState toState, ChessAction action, int stateSize, bool anchorTip)
    {
        var boardSize = 8;
        using var bmp = ComposeBoard(toState, stateSize);
        StampMoveHighlight(bmp, action, Color.Red, stateSize, 1.0f, anchorTip: anchorTip);
        StampPieces(bmp, toState.Board, Math.Max(4, stateSize / 8), boardSize, 1.0f);

        return ToPng(bmp);
    }

    public byte[] RenderTransitionGif(ChessState fromState, ChessState toState, ChessAction action, int stateSize, bool anchorTip)
    {
        var boardSize = 8;
        using var bmpFrom = ComposeBoard(fromState, stateSize);
        StampMoveHighlight(bmpFrom, action, Color.OrangeRed, stateSize, 1.0f, anchorTip: anchorTip);
        StampPieces(bmpFrom, fromState.Board, Math.Max(4, stateSize / 8), boardSize, 1.0f);

        using var bmpTo = ComposeBoard(toState, stateSize);
        StampMoveHighlight(bmpTo, action, Color.Red, stateSize, 1.0f, anchorTip: anchorTip);
        StampPieces(bmpTo, toState.Board, Math.Max(4, stateSize / 8), boardSize, 1.0f);

        var frames = new[]
        {
            bmpFrom,
            bmpTo
        }.ToList();

        return HistoryRenderUtility.RenderGif(frames);
    }

    // ─────────────────────────────────────────────────────────────
    // COMPOSITION LAYER HELPERS
    // ─────────────────────────────────────────────────────────────

    private static Bitmap ComposeBoard(ChessState state, int stateSize)
    {
        int boardSize = 8;
        int cell = Math.Max(4, stateSize / boardSize);

        var baseLayer = EntityRenderUtility.StampSquaresLayer(cell, boardSize);
        var pieceLayer = EntityRenderUtility.StampPiecesLayer(state.Board, cell, boardSize);

        // Composite them
        using var g = Graphics.FromImage(baseLayer);
        g.DrawImageUnscaled(pieceLayer, 0, 0);

        return baseLayer;
    }

    private static void StampPieces(Bitmap bmp, ChessPiece[,] board, int cell, int boardSize, float opacity = 1.0f)
    {
        using var g = Graphics.FromImage(bmp);
        EntityRenderUtility.StampPieces(g, board, cell, boardSize, opacity);
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
        EntityRenderUtility.StampMoves(g, cell, moveList, transparentColor, false, anchorTip: anchorTip);
    }

    private static void StampBlockedCells(Bitmap bmp, IEnumerable<(int X, int Y)> positions, int stateSize)
    {
        int boardSize = 8;
        int cell = Math.Max(4, stateSize / boardSize);
        using var g = Graphics.FromImage(bmp);
        EntityRenderUtility.StampCells_InnerContour(g, cell, boardSize, positions, color: Color.Gray, thickness: 5);
    }
    private static void StampPinnedCells(Bitmap bmp, IEnumerable<(int X, int Y)> positions, int stateSize)
    {
        int boardSize = 8;
        int cell = Math.Max(4, stateSize / boardSize);
        using var g = Graphics.FromImage(bmp);
        EntityRenderUtility.StampCells_InnerContour(g, cell, boardSize, positions, color: Color.Black, thickness: 5);
    }
    public static void StampAttackedCells(Bitmap bmp, IEnumerable<ChessActionCandidate> attackingCandidates, int stateSize)
    {
        int boardSize = 8;
        int cell = Math.Max(4, stateSize / boardSize);
        using var g = Graphics.FromImage(bmp);
        var positions = attackingCandidates.Select(c => (c.Action.To.X, c.Action.To.Y)).Distinct().ToList();
        EntityRenderUtility.StampCells_InnerContour(g, cell, boardSize, positions, color: Color.Orange, thickness: 3);
        // for each attacked cell, draw an arrow from the attacker to the cell (c.AttackingMoves)
        //invert positions on the y axis
        var rawAttackingMovesList = attackingCandidates.Select(p => (p.Action.From.X, p.Action.From.Y, p.Action.To.X, p.Action.To.Y));
        EntityRenderUtility.StampMoves(g, cell, rawAttackingMovesList, Color.Orange, false, anchorTip: false);
    }

    private static void StampThreatenedCells(Bitmap bmp, IEnumerable<ChessActionCandidate> threateningCandidates, int stateSize)
    {
        int boardSize = 8;
        int cell = Math.Max(4, stateSize / boardSize);
        using var g = Graphics.FromImage(bmp);
        var positions = threateningCandidates.Select(c => (c.Action.To.X, c.Action.To.Y)).ToList();
        //color is half way between orange and red
        var t = 0.5f;
        var c1 = Color.Orange;
        var c2 = Color.White;
        var color = Color.FromArgb((int)((c1.R + c2.R) * t), (int)((c1.G + c2.G) * t), (int)((c1.B + c2.B) * t));
        EntityRenderUtility.StampCells_InnerContour(g, cell, boardSize, positions, color: color, thickness: 3, 3);
    }

    private static void StampCheckedCells(Bitmap bmp, IEnumerable<ChessActionCandidate> cells, int stateSize)
    {
        int boardSize = 8;
        int cell = Math.Max(4, stateSize / boardSize);
        using var g = Graphics.FromImage(bmp);
        var positions = cells.Select(c => (c.Action.To.X, c.Action.To.Y)).ToList();
        EntityRenderUtility.StampCells_InnerContour(g, cell, boardSize, positions, color: Color.LightBlue, thickness: 1);
    }


    private static byte[] ToPng(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
