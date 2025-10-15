using System.Drawing;
using System.Drawing.Imaging;
using Game.Core.Renders;

namespace Game.Chess.RendersB;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class ChessView : ViewBase<ChessMove, PolicyB.ChessBoard>
{
    public override byte[] RenderStatePng(PolicyB.ChessBoard state, int stateSize = 400)
    {
        using var bmp = ComposeBoard(state, stateSize);
        return ToPng(bmp);
    }

    public override byte[] RenderTransitionSequenceGif(
        IEnumerable<(PolicyB.ChessBoard stateFrom, PolicyB.ChessBoard stateTo, ChessMove action)> transitions,
        int stateSize = 400)
    {
        var bitmaps = transitions
            .Select(f =>
            {
                var bmp = ComposeBoard(f.stateTo, stateSize);
                StampMoveHighlight(bmp, f.action, Color.Red, stateSize);
                return bmp;
            })
            .ToList();

        return Renders.GifComposer.Combine(bitmaps);
    }

    public override byte[] RenderPreTransitionPng(PolicyB.ChessBoard stateFrom, PolicyB.ChessBoard stateTo, ChessMove action, int stateSize = 400)
    {
        using var bmp = ComposeBoard(stateFrom, stateSize);
        StampMoveHighlight(bmp, action, Color.OrangeRed, stateSize);
        return ToPng(bmp);
    }

    public override byte[] RenderPostTransitionPng(PolicyB.ChessBoard stateFrom, PolicyB.ChessBoard stateTo, ChessMove action, int stateSize = 400)
    {
        using var bmp = ComposeBoard(stateTo, stateSize);
        StampMoveHighlight(bmp, action, Color.Red, stateSize);
        return ToPng(bmp);
    }

    public override byte[] RenderTransitionGif(PolicyB.ChessBoard stateFrom, PolicyB.ChessBoard stateTo, ChessMove action, int stateSize = 400)
    {
        var frames = new[]
        {
            ComposeBoard(stateFrom, stateSize),
            ComposeBoard(stateTo, stateSize)
        }.ToList();

        return Renders.GifComposer.Combine(frames);
    }

    // ─────────────────────────────────────────────────────────────
    // COMPOSITION LAYER HELPERS
    // ─────────────────────────────────────────────────────────────

    private static Bitmap ComposeBoard(PolicyB.ChessBoard state, int stateSize)
    {
        int cell = Math.Max(4, stateSize / 8);

        var baseLayer = ChessBoardStamps.StampSquaresLayer(cell);
        var pieceLayer = ChessBoardStamps.StampPiecesLayer(state.Board, cell);

        // Composite them
        using var g = Graphics.FromImage(baseLayer);
        g.DrawImageUnscaled(pieceLayer, 0, 0);

        return baseLayer;
    }

    private static void StampMoveHighlight(Bitmap bmp, ChessMove move, Color color, int stateSize)
    {
        int cell = Math.Max(4, stateSize / 8);
        using var g = Graphics.FromImage(bmp);
        var moveList = new List<(int, int, int, int)>
        {
            (move.From.Row, move.From.Col, move.To.Row, move.To.Col)
        };
        ChessBoardStamps.StampMoves(g, cell, moveList, color);
    }

    private static byte[] ToPng(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
