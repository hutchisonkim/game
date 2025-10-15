using System.Drawing;
using System.Drawing.Imaging;
using Game.Core.Renders;

namespace Game.Chess.Renders;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class ChessView : IView<ChessMove, ChessBoard, ChessView>
{
    public byte[] RenderStatePng(ChessBoard state, int stateSize = 400)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        var board = BoardExtractor.ExtractBoardFromState(state);
        using var bmp = ChessBitmapRenderer.RenderBoardBitmap(board, stateSize);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public byte[] RenderTransitionSequenceGif(IEnumerable<(ChessBoard stateFrom, ChessBoard stateTo, ChessMove action)> transitions, int stateSize = 400)
    {
        if (transitions == null) throw new ArgumentNullException(nameof(transitions));
        var frames = transitions.ToArray();
        if (frames.Length == 0) return Array.Empty<byte>();

        var bitmaps = frames.Select(f => ChessBitmapRenderer.RenderBoardBitmap(BoardExtractor.ExtractBoardFromState(f.stateTo), stateSize)).ToList();
        return GifComposer.CombineBitmapsToGif(bitmaps, frameDelaysMs: 0);
    }

    public byte[] RenderPreTransitionPng(ChessBoard stateFrom, ChessBoard stateTo, ChessMove action, int stateSize = 400)
    {
        if (stateFrom == null) throw new ArgumentNullException(nameof(stateFrom));
        var board = BoardExtractor.ExtractBoardFromState(stateFrom);
        var cell = Math.Max(4, stateSize / 8);
        using var bmp = ChessBitmapRenderer.RenderBoardBitmap(board, stateSize);

        using var g = Graphics.FromImage(bmp);
        MoveHighlighter.DrawMoves(g, cell, new List<(int, int, int, int)> { (action.From.Row, action.From.Col, action.To.Row, action.To.Col) }, Color.OrangeRed);

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public byte[] RenderPostTransitionPng(ChessBoard stateFrom, ChessBoard stateTo, ChessMove action, int stateSize = 400)
    {
        if (stateTo == null) throw new ArgumentNullException(nameof(stateTo));
        var board = BoardExtractor.ExtractBoardFromState(stateTo);
        var cell = Math.Max(4, stateSize / 8);
        using var bmp = ChessBitmapRenderer.RenderBoardBitmap(board, stateSize);

        using var g = Graphics.FromImage(bmp);
        MoveHighlighter.DrawMoves(g, cell, new List<(int, int, int, int)> { (action.From.Row, action.From.Col, action.To.Row, action.To.Col) }, Color.Red);

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public byte[] RenderTransitionGif(ChessBoard stateFrom, ChessBoard stateTo, ChessMove action, int stateSize = 400)
    {
        throw new NotImplementedException();
    }
}

