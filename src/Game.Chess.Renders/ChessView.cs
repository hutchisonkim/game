using System.Drawing;
using System.Drawing.Imaging;
using Game.Core;
using Game.Core.Renders;

namespace Game.Chess.Renders;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class ChessView<TAction, TState, TView> : IView<TAction, TState, TView>
    where TAction : IAction
    where TState : IState<TAction, TState>
    where TView : IView<TAction, TState, TView>
{
    public byte[] RenderPreTransitionPng(TState stateFrom, TState stateTo, TAction action, int stateSize = 400)
    {
        if (stateFrom == null) throw new ArgumentNullException(nameof(stateFrom));
        var board = ExtractBoardFromState(stateFrom);
        var cell = Math.Max(4, stateSize / 8);
        using var bmp = RenderBoardBitmap(board, stateSize);
        try
        {
            var moves = MoveHighlighter.DetectMoves(board, stateTo == null ? null : ExtractBoardFromState(stateTo), action?.ToString());
            if (moves.Count > 0)
            {
                using var g = Graphics.FromImage(bmp);
                MoveHighlighter.DrawMoves(g, cell, moves, Color.Red);
            }
        }
        catch
        {
            // best-effort: ignore drawing failures
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public byte[] RenderPostTransitionPng(TState stateFrom, TState stateTo, TAction action, int stateSize = 400)
    {
        if (stateTo == null) throw new ArgumentNullException(nameof(stateTo));
        var board = ExtractBoardFromState(stateTo);
        var cell = Math.Max(4, stateSize / 8);
        using var bmp = RenderBoardBitmap(board, stateSize);
        try
        {
            var moves = MoveHighlighter.DetectMoves(stateFrom == null ? null : ExtractBoardFromState(stateFrom), board, action?.ToString());
            if (moves.Count > 0)
            {
                using var g = Graphics.FromImage(bmp);
                MoveHighlighter.DrawMoves(g, cell, moves, Color.Red);
            }
        }
        catch
        {
            // best-effort: ignore drawing failures
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public byte[] RenderTransitionGif(TState stateFrom, TState stateTo, TAction action, int stateSize = 400)
    {
        throw new NotImplementedException();
    }

    public static char[,] ParseFen(string fenPlacement)
    {
        return ChessBoardParser.ParseFen(fenPlacement);
    }

    private static char[,] ExtractBoardFromState(object state)
    {
        return BoardExtractor.ExtractBoardFromState(state);
    }

    public static Bitmap RenderBoardBitmap(char[,] board, int size, bool drawPieces = true)
    {
        return ChessBitmapRenderer.RenderBoardBitmap(board, size, drawPieces);
    }

    public byte[] RenderStatePng(TState state, int stateSize = 400)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        var board = ExtractBoardFromState(state);
        using var bmp = RenderBoardBitmap(board, stateSize);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public byte[] RenderTransitionPng(TState stateFrom, TState stateTo, TAction action, int stateSize = 400)
    {
        if (stateFrom == null) throw new ArgumentNullException(nameof(stateFrom));
        if (stateTo == null) throw new ArgumentNullException(nameof(stateTo));

        var bFrom = ExtractBoardFromState(stateFrom);
        var bTo = ExtractBoardFromState(stateTo);

        int cell = Math.Max(4, stateSize / 8);
        using var boardBg = RenderBoardBitmap(bFrom, stateSize, drawPieces: false);
        using var outBmp = new Bitmap(boardBg.Width, boardBg.Height);
        using var g = Graphics.FromImage(outBmp);
        using var ms = new MemoryStream();

        g.Clear(Color.White);
        g.DrawImage(boardBg, 0, 0);

        void DrawPieces(char[,] board, float opacity)
        {
            PieceOverlay.DrawPieces(g, board, cell, opacity);
        }

        DrawPieces(bFrom, 0.35f);
        DrawPieces(bTo, 1.0f);

        if (action != null)
        {
            var moves = MoveHighlighter.DetectMoves(bFrom, bTo, action.ToString());
            if (moves.Count > 0)
            {
                MoveHighlighter.DrawMoves(g, cell, moves, Color.Red);
            }
        }

        outBmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public byte[] RenderTimelineGifUsingPngPairs(List<(byte[], byte[])> transitionPngPairs, int stateSize = 400)
    {
        if (transitionPngPairs == null || transitionPngPairs.Count == 0)
            throw new ArgumentNullException(nameof(transitionPngPairs));

        var bitmaps = new List<Bitmap>();
        foreach (var (fromPng, toPng) in transitionPngPairs)
        {
            try
            {
                var fromStream = new MemoryStream(fromPng);
                var toStream = new MemoryStream(toPng);
                var fromBmp = new Bitmap(fromStream);
                var toBmp = new Bitmap(toStream);

                bitmaps.Add(fromBmp);
                bitmaps.Add(toBmp);


            }
            catch
            {
                // Ignore any invalid PNGs and continue
            }
        }

        // Delegate composition to GifComposer (works from PNG pair bytes)
        return GifComposer.CombinePngPairsToGif([.. transitionPngPairs.Select(p => (p.Item1, p.Item2))], frameDelaysMs: 480);
    }

    public byte[] RenderTransitionSequencePng(IEnumerable<(TState stateFrom, TState stateTo, TAction action)> transitions, int stateSize = 400)
    {
        throw new NotImplementedException();
    }

    public byte[] RenderTransitionSequenceGif(IEnumerable<(TState stateFrom, TState stateTo, TAction action)> transitions, int stateSize = 400)
    {
        if (transitions == null) throw new ArgumentNullException(nameof(transitions));
        var frames = transitions.ToArray();
        if (frames.Length == 0) return Array.Empty<byte>();

        var bitmaps = frames.Select(f => RenderBoardBitmap(ExtractBoardFromState(f.stateTo), stateSize)).ToList();
        return GifComposer.CombineBitmapsToGif(bitmaps, frameDelaysMs: 0);
    }
}

