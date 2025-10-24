using System.Drawing;
using System.Drawing.Imaging;
using Game.Core.Renders;
using Game.Chess.Entity;
using Game.Chess.History;
using static Game.Chess.History.ChessHistoryUtility;
namespace Game.Chess.Renders;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class ChessView : ViewBase<ChessAction, ChessState>
{
    // add constructor arguments for potentially rendering attacked cells, threatened cells, and checked cells.
    private readonly bool _renderAttackedCells;
    private readonly bool _renderThreatenedCells;
    private readonly bool _renderCheckedCells;
    private readonly bool _renderPinnedCells;
    private readonly bool _renderBlockedCells;

    public ChessView(bool renderAttackedCells = false, bool renderThreatenedCells = false, bool renderCheckedCells = false, bool renderPinnedCells = false, bool renderBlockedCells = false)
    {
        _renderAttackedCells = renderAttackedCells;
        _renderThreatenedCells = renderThreatenedCells;
        _renderCheckedCells = renderCheckedCells;
        _renderPinnedCells = renderPinnedCells;
        _renderBlockedCells = renderBlockedCells;
    }

    public override byte[] RenderStatePng(ChessState state, int stateSize)
    {
        var currentTurnColorFlag = state.TurnColor;
        var otherColor = (currentTurnColorFlag & ChessPieceAttribute.White) != 0 ? ChessPieceAttribute.Black : ChessPieceAttribute.White;
        using var bmp = ComposeBoard(state, stateSize);
        // if (_renderBlockedCells)
        //     StampBlockedCells(bmp, state.GetBlockedPositions(currentTurnColorFlag), stateSize);
        if (_renderPinnedCells)
            StampPinnedCells(bmp, state.GetPinnedCells(currentTurnColorFlag, includeTargetless: false, includeFriendlyfire: false), stateSize);
        if (_renderAttackedCells)
            StampAttackedCells(bmp, state.GetAttackingActionCandidates(currentTurnColorFlag, includeTargetless: true, includeFriendlyfire: true), stateSize);
        if (_renderThreatenedCells)
            StampThreatenedCells(bmp, state.GetThreateningActionCandidates(currentTurnColorFlag, includeTargetless: false, includeFriendlyfire: false), stateSize);
        if (_renderCheckedCells)
            StampCheckedCells(bmp, state.GetCheckingActionCandidates(currentTurnColorFlag, includeTargetless: false, includeFriendlyfire: false), stateSize);

        var boardSize = 8;
        StampPieces(bmp, state.Board, Math.Max(4, stateSize / 8), boardSize, 1.0f); // render pieces with 50% opacity when overlays are active

        return ToPng(bmp);
    }

    public override byte[] RenderTransitionSequenceGif(
        IEnumerable<(ChessState fromState, ChessState toState, ChessAction action, bool selected)> transitions,
        int stateSize,
        bool anchorTip)
    {
        var boardSize = 8;
        var turnTransitionsDict = new Dictionary<int, List<(ChessState fromState, ChessState toState, ChessAction action, bool selected)>>();
        var turnCandidateTransitionsDict = new Dictionary<int, List<(ChessState fromState, ChessState toState, ChessAction action, bool selected)>>();
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
                        StampMoveHighlight(bmpFrom, turnCandidateTransition.action, Color.Orange, stateSize, 1.0f, anchorTip: anchorTip);
                    }
                    StampPieces(bmpFrom, turnCandidateTransitions.First().fromState.Board, Math.Max(4, stateSize / boardSize), boardSize, 1.0f);
                    bitmaps.Add(bmpFrom);
                }
            }
            {//display all selected transitions for this turn (with all candidates still in the frame)
                var turnTransitions = turnTransitionsDict.ContainsKey(turn) ? turnTransitionsDict[turn] : [];

                if (turnTransitions.Count == 0)
                {
                    Console.WriteLine($"*******************************No transitions for turn {turn}");
                    continue;
                }

                var turnCandidateTransitions = turnCandidateTransitionsDict.ContainsKey(turn) ? turnCandidateTransitionsDict[turn] : [];
                if (turnCandidateTransitions.Count == 0)
                {
                    // Console.WriteLine($"*******************************No candidate transitions for turn {turn}");
                    // continue;
                }

                var bmpFrom = ComposeBoard(turnTransitions.First().fromState, stateSize);
                var bmpTo = ComposeBoard(turnTransitions.First().toState, stateSize);

                foreach (var turnCandidateTransition in turnCandidateTransitions)
                {
                    StampMoveHighlight(bmpFrom, turnCandidateTransition.action, Color.Orange, stateSize, 1.0f, anchorTip: anchorTip);
                }

                foreach (var turnTransition in turnTransitions)
                {
                    StampMoveHighlight(bmpFrom, turnTransition.action, Color.Red, stateSize, 1.0f, anchorTip: anchorTip);
                    StampPieces(bmpFrom, turnTransition.fromState.Board, Math.Max(4, stateSize / boardSize), boardSize, 1.0f);

                    StampMoveHighlight(bmpTo, turnTransition.action, Color.Red, stateSize, 1.0f, anchorTip: anchorTip);
                    StampPieces(bmpTo, turnTransition.toState.Board, Math.Max(4, stateSize / boardSize), boardSize, 1.0f);
                }

                bitmaps.Add(bmpFrom);
                bitmaps.Add(bmpTo);
            }
        }


        return Renders.GifComposer.Combine(bitmaps);
    }

    public override byte[] RenderPreTransitionPng(ChessState fromState, ChessState toState, ChessAction action, int stateSize, bool anchorTip)
    {
        var boardSize = 8;
        using var bmp = ComposeBoard(fromState, stateSize);
        StampMoveHighlight(bmp, action, Color.OrangeRed, stateSize, 1.0f, anchorTip: anchorTip);
        StampPieces(bmp, fromState.Board, Math.Max(4, stateSize / 8), boardSize, 1.0f);

        return ToPng(bmp);
    }

    public override byte[] RenderPostTransitionPng(ChessState fromState, ChessState toState, ChessAction action, int stateSize, bool anchorTip)
    {
        var boardSize = 8;
        using var bmp = ComposeBoard(toState, stateSize);
        StampMoveHighlight(bmp, action, Color.Red, stateSize, 1.0f, anchorTip: anchorTip);
        StampPieces(bmp, toState.Board, Math.Max(4, stateSize / 8), boardSize, 1.0f);

        return ToPng(bmp);
    }

    public override byte[] RenderTransitionGif(ChessState fromState, ChessState toState, ChessAction action, int stateSize, bool anchorTip)
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

        return Renders.GifComposer.Combine(frames);
    }

    // ─────────────────────────────────────────────────────────────
    // COMPOSITION LAYER HELPERS
    // ─────────────────────────────────────────────────────────────

    private static Bitmap ComposeBoard(ChessState state, int stateSize)
    {
        int boardSize = 8;
        int cell = Math.Max(4, stateSize / boardSize);

        var baseLayer = ChessBoardStamps.StampSquaresLayer(cell, boardSize);
        var pieceLayer = ChessBoardStamps.StampPiecesLayer(state.Board, cell, boardSize);

        // Composite them
        using var g = Graphics.FromImage(baseLayer);
        g.DrawImageUnscaled(pieceLayer, 0, 0);

        return baseLayer;
    }

    private static void StampPieces(Bitmap bmp, ChessPiece[,] board, int cell, int boardSize, float opacity = 1.0f)
    {
        using var g = Graphics.FromImage(bmp);
        ChessBoardStamps.StampPieces(g, board, cell, boardSize, opacity);
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
        ChessBoardStamps.StampMoves(g, cell, moveList, transparentColor, false, anchorTip: anchorTip);
    }

    private static void StampBlockedCells(Bitmap bmp, IEnumerable<(int X, int Y)> positions, int stateSize)
    {
        int boardSize = 8;
        int cell = Math.Max(4, stateSize / boardSize);
        using var g = Graphics.FromImage(bmp);
        ChessBoardStamps.StampCells_InnerContour(g, cell, boardSize, positions, color: Color.Gray, thickness: 5);
    }
    private static void StampPinnedCells(Bitmap bmp, IEnumerable<(int X, int Y)> positions, int stateSize)
    {
        int boardSize = 8;
        int cell = Math.Max(4, stateSize / boardSize);
        using var g = Graphics.FromImage(bmp);
        ChessBoardStamps.StampCells_InnerContour(g, cell, boardSize, positions, color: Color.Black, thickness: 5);
    }
    private static void StampAttackedCells(Bitmap bmp, IEnumerable<ChessActionCandidate> attackingCandidates, int stateSize)
    {
        int boardSize = 8;
        int cell = Math.Max(4, stateSize / boardSize);
        using var g = Graphics.FromImage(bmp);
        var positions = attackingCandidates.Select(c => (c.Action.To.X, c.Action.To.Y)).Distinct().ToList();
        ChessBoardStamps.StampCells_InnerContour(g, cell, boardSize, positions, color: Color.Orange, thickness: 3);
        // for each attacked cell, draw an arrow from the attacker to the cell (c.AttackingMoves)
        //invert positions on the y axis
        var rawAttackingMovesList = attackingCandidates.Select(p => (p.Action.From.X, p.Action.From.Y, p.Action.To.X, p.Action.To.Y));
        ChessBoardStamps.StampMoves(g, cell, rawAttackingMovesList, Color.Orange, false, anchorTip: false);
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
        ChessBoardStamps.StampCells_InnerContour(g, cell, boardSize, positions, color: color, thickness: 3, 3);
    }

    private static void StampCheckedCells(Bitmap bmp, IEnumerable<ChessActionCandidate> cells, int stateSize)
    {
        int boardSize = 8;
        int cell = Math.Max(4, stateSize / boardSize);
        using var g = Graphics.FromImage(bmp);
        var positions = cells.Select(c => (c.Action.To.X, c.Action.To.Y)).ToList();
        ChessBoardStamps.StampCells_InnerContour(g, cell, boardSize, positions, color: Color.LightBlue, thickness: 1);
    }


    private static byte[] ToPng(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
