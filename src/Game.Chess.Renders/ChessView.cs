using System.Drawing;
using System.Drawing.Imaging;
using Game.Core.Renders;

namespace Game.Chess.Renders;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class ChessView : IView<ChessMove, ChessBoard, ChessView>
{
    public byte[] RenderTransitionGif(ChessBoard stateFrom, ChessBoard stateTo, ChessMove action, int stateSize = 400)
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

    public byte[] RenderStatePng(ChessBoard state, int stateSize = 400)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        var board = ExtractBoardFromState(state);
        using var bmp = RenderBoardBitmap(board, stateSize);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public byte[] RenderTransitionSequenceGif(IEnumerable<(ChessBoard stateFrom, ChessBoard stateTo, ChessMove action)> transitions, int stateSize = 400)
    {
        if (transitions == null) throw new ArgumentNullException(nameof(transitions));
        var frames = transitions.ToArray();
        if (frames.Length == 0) return Array.Empty<byte>();

        var bitmaps = frames.Select(f => RenderBoardBitmap(ExtractBoardFromState(f.stateTo), stateSize)).ToList();
        return GifComposer.CombineBitmapsToGif(bitmaps, frameDelaysMs: 0);
    }

    public byte[] RenderPreTransitionPng(ChessBoard stateFrom, ChessBoard stateTo, ChessMove action, int stateSize = 400)
    {
        if (stateFrom == null) throw new ArgumentNullException(nameof(stateFrom));
        var board = ExtractBoardFromState(stateFrom);
        var cell = Math.Max(4, stateSize / 8);
        using var bmp = RenderBoardBitmap(board, stateSize);
        try
        {
            using var g = Graphics.FromImage(bmp);
            MoveHighlighter.DrawMoves(g, cell, new List<(int, int, int, int)> { (action.From.Row, action.From.Col, action.To.Row, action.To.Col) }, Color.Orange);
        }
        catch
        {
            // best-effort: ignore drawing failures
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public byte[] RenderPostTransitionPng(ChessBoard stateFrom, ChessBoard stateTo, ChessMove action, int stateSize = 400)
    {
        if (stateTo == null) throw new ArgumentNullException(nameof(stateTo));
        var board = ExtractBoardFromState(stateTo);
        var cell = Math.Max(4, stateSize / 8);
        using var bmp = RenderBoardBitmap(board, stateSize);
        try
        {
            using var g = Graphics.FromImage(bmp);
            MoveHighlighter.DrawMoves(g, cell, new List<(int, int, int, int)> { (action.From.Row, action.From.Col, action.To.Row, action.To.Col) }, Color.Red);
        }
        catch
        {
            // best-effort: ignore drawing failures
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public static byte[] RenderTimelineGifUsingPngPairs(List<(byte[], byte[])> transitionPngPairs, int stateSize = 400)
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
        return GifComposer.CombinePngPairsToGif(transitionPngPairs.Select(p => (p.Item1, p.Item2)).ToList());
    }
}

