using System.Drawing;
using System.Drawing.Imaging;
using Game.Core.Renders;
using static Game.Chess.Policy.ChessState;

namespace Game.Chess.Renders;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class ChessView : ViewBase<BaseAction, Policy.ChessState>
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

    public override byte[] RenderStatePng(Policy.ChessState state, int stateSize = 400)
    {
        var currentTurnColor = state.CurrentTurnColor;
        var otherColor = currentTurnColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
        using var bmp = ComposeBoard(state, stateSize);
        if (_renderBlockedCells)
            StampBlockedCells(bmp, state.GetBlockedCells(currentTurnColor), stateSize);
        if (_renderPinnedCells)
            StampPinnedCells(bmp, state.GetPinnedCells(currentTurnColor), stateSize);
        if (_renderAttackedCells)
            StampAttackedCells(bmp, state.GetAttackedCells(currentTurnColor), stateSize);
        if (_renderThreatenedCells)
            StampThreatenedCells(bmp, state.GetThreatenedCells(otherColor), stateSize);
        if (_renderCheckedCells)
            StampCheckedCells(bmp, state.GetCheckedCells(otherColor), stateSize);

        StampPieces(bmp, state.Board, Math.Max(4, stateSize / 8), 1.0f); // render pieces with 50% opacity when overlays are active

        return ToPng(bmp);
    }

    public override byte[] RenderTransitionSequenceGif(
        IEnumerable<(Policy.ChessState fromState, Policy.ChessState toState, BaseAction action)> transitions,
        int stateSize = 400)
    {
        var bitmaps = transitions
            .SelectMany(f =>
            {
                var bmpFrom = ComposeBoard(f.fromState, stateSize);
                StampMoveHighlight(bmpFrom, f.action, Color.OrangeRed, stateSize);
                var bmpTo = ComposeBoard(f.toState, stateSize);
                StampMoveHighlight(bmpTo, f.action, Color.Red, stateSize);
                return new[] { bmpFrom, bmpTo };
            })
            .ToList();

        return Renders.GifComposer.Combine(bitmaps);
    }

    public override byte[] RenderPreTransitionPng(Policy.ChessState fromState, Policy.ChessState toState, BaseAction action, int stateSize = 400)
    {
        using var bmp = ComposeBoard(fromState, stateSize);
        StampMoveHighlight(bmp, action, Color.OrangeRed, stateSize);
        return ToPng(bmp);
    }

    public override byte[] RenderPostTransitionPng(Policy.ChessState fromState, Policy.ChessState toState, BaseAction action, int stateSize = 400)
    {
        using var bmp = ComposeBoard(toState, stateSize);
        StampMoveHighlight(bmp, action, Color.Red, stateSize);
        return ToPng(bmp);
    }

    public override byte[] RenderTransitionGif(Policy.ChessState fromState, Policy.ChessState toState, BaseAction action, int stateSize = 400)
    {
        using var bmpFrom = ComposeBoard(fromState, stateSize);
        StampMoveHighlight(bmpFrom, action, Color.OrangeRed, stateSize);
        using var bmpTo = ComposeBoard(toState, stateSize);
        StampMoveHighlight(bmpTo, action, Color.Red, stateSize);
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

    private static Bitmap ComposeBoard(Policy.ChessState state, int stateSize)
    {
        int cell = Math.Max(4, stateSize / 8);

        var baseLayer = ChessBoardStamps.StampSquaresLayer(cell);
        var pieceLayer = ChessBoardStamps.StampPiecesLayer(state.Board, cell);

        // Composite them
        using var g = Graphics.FromImage(baseLayer);
        g.DrawImageUnscaled(pieceLayer, 0, 0);

        return baseLayer;
    }

    private static void StampPieces(Bitmap bmp, Policy.Piece?[,] board, int cell, float opacity = 1.0f)
    {
        using var g = Graphics.FromImage(bmp);
        ChessBoardStamps.StampPieces(g, board, cell, opacity);
    }

    private static void StampMoveHighlight(Bitmap bmp, BaseAction move, Color color, int stateSize)
    {
        int cell = Math.Max(4, stateSize / 8);
        using var g = Graphics.FromImage(bmp);
        var moveList = new List<(int, int, int, int)>
        {
            (move.From.Row, move.From.Col, move.To.Row, move.To.Col)
        };
        //invert positions on the y axis
        moveList = moveList.Select(p => (7 - p.Item1, p.Item2, 7 - p.Item3, p.Item4)).ToList();
        ChessBoardStamps.StampMoves(g, cell, moveList, color);
    }

    private static void StampBlockedCells(Bitmap bmp, IEnumerable<BoardCell> cells, int stateSize)
    {
        int cell = Math.Max(4, stateSize / 8);
        using var g = Graphics.FromImage(bmp);
        var positions = cells.Select(c => (c.Row, c.Col, c.Row, c.Col)).ToList();
        //invert positions on the y axis
        positions = positions.Select(p => (7 - p.Item1, p.Item2, 7 - p.Item3, p.Item4)).ToList();
        ChessBoardStamps.StampCells_InnerContour(g, cell, positions, color: Color.Gray, thickness: 5);
    }
    private static void StampPinnedCells(Bitmap bmp, IEnumerable<BoardCell> cells, int stateSize)
    {
        int cell = Math.Max(4, stateSize / 8);
        using var g = Graphics.FromImage(bmp);
        var positions = cells.Select(c => (c.Row, c.Col, c.Row, c.Col)).ToList();
        ChessBoardStamps.StampCells_InnerContour(g, cell, positions, color: Color.Black, thickness: 5);
    }
    private static void StampAttackedCells(Bitmap bmp, IEnumerable<AttackedCell> cells, int stateSize)
    {
        int cell = Math.Max(4, stateSize / 8);
        using var g = Graphics.FromImage(bmp);
        var positions = cells.Select(c => (c.Row, c.Col, c.Row, c.Col)).ToList();
        //invert positions on the y axis
        positions = positions.Select(p => (7 - p.Item1, p.Item2, 7 - p.Item3, p.Item4)).ToList();
        ChessBoardStamps.StampCells_InnerContour(g, cell, positions, color: Color.Orange, thickness: 5);
        // for each attacked cell, draw an arrow from the attacker to the cell (c.AttackingMoves)
        var attackingMovesList = cells.SelectMany(c => c.AttackingMoves);
        var rawAttackingMovesList = attackingMovesList.Select(m => (m.BaseMove.From.Row, m.BaseMove.From.Col, m.BaseMove.To.Row, m.BaseMove.To.Col));
        //invert positions on the y axis
        rawAttackingMovesList = rawAttackingMovesList.Select(p => (7 - p.Item1, p.Item2, 7 - p.Item3, p.Item4));
        ChessBoardStamps.StampMoves(g, cell, rawAttackingMovesList, Color.Orange);
    }

    private static void StampThreatenedCells(Bitmap bmp, IEnumerable<BoardCell> cells, int stateSize)
    {
        int cell = Math.Max(4, stateSize / 8);
        using var g = Graphics.FromImage(bmp);
        var positions = cells.Select(c => (c.Row, c.Col, c.Row, c.Col)).ToList();
        ChessBoardStamps.StampCells_InnerContour(g, cell, positions, color: Color.OrangeRed, thickness: 2);
    }

    private static void StampCheckedCells(Bitmap bmp, IEnumerable<BoardCell> cells, int stateSize)
    {
        int cell = Math.Max(4, stateSize / 8);
        using var g = Graphics.FromImage(bmp);
        var positions = cells.Select(c => (c.Row, c.Col, c.Row, c.Col)).ToList();
        ChessBoardStamps.StampCells_InnerContour(g, cell, positions, color: Color.LightBlue, thickness: 1);
    }


    private static byte[] ToPng(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
