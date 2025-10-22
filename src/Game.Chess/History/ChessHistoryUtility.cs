using Game.Chess.Entity;
namespace Game.Chess.History;

// 🔹 Domain-agnostic piece behavior using the `Piece` record (Game.Chess.Piece)
public static class ChessHistoryUtility
{
    public static (int X, int Y) ForwardAxis(ChessPiece piece) => (1, piece.IsWhite ? 1 : -1); // determines the initial pattern direction that gets tiled (used for pawn movement that is forward-only)
    public static bool IsClockwise(ChessPiece piece) => piece.IsWhite ? true : false; // determines the initial queen placement (white mirrors black)


    public static class Vector2
    {
        public static readonly (int X, int Y) OneByZero = (1, 0);
        public static readonly (int X, int Y) ZeroByOne = (0, 1);
        public static readonly (int X, int Y) OneByOne = (1, 1);
        public static readonly (int X, int Y) OneByTwo = (1, 2);
    }

    internal static IEnumerable<ChessPattern> GetBasePatterns(ChessPiece piece)
    {
        return piece.TypeFlag switch
        {
            var t when (t & ChessPieceAttribute.Pawn) != 0 =>
            [
                new ChessPattern(vector: Vector2.ZeroByOne, mirrors: MirrorBehavior.Horizontal, repeats: RepeatBehavior.NotRepeatable, captures: CaptureBehavior.MoveOnly, forwardOnly: true),
                new ChessPattern(Vector2.ZeroByOne, mirrors: MirrorBehavior.Horizontal, repeats: RepeatBehavior.RepeatableOnce, captures: CaptureBehavior.MoveOnly, forwardOnly: true),
                new ChessPattern(Vector2.OneByOne, mirrors: MirrorBehavior.Horizontal, repeats: RepeatBehavior.NotRepeatable, captures: CaptureBehavior.CaptureOnly, forwardOnly: true)
            ],
            var t when (t & ChessPieceAttribute.Rook) != 0 =>
            [
                new ChessPattern(Vector2.ZeroByOne),
                new ChessPattern(Vector2.OneByZero)
            ],
            var t when (t & ChessPieceAttribute.Knight) != 0 =>
            [
                new ChessPattern(Vector2.OneByTwo, repeats: RepeatBehavior.NotRepeatable, jumps: true)
            ],
            var t when (t & ChessPieceAttribute.Bishop) != 0 =>
            [
                new ChessPattern(Vector2.OneByOne)
            ],
            var t when (t & ChessPieceAttribute.Queen) != 0 =>
            [
                new ChessPattern(Vector2.ZeroByOne),
                new ChessPattern(Vector2.OneByZero),
                new ChessPattern(Vector2.OneByOne)
            ],
            var t when (t & ChessPieceAttribute.King) != 0 =>
            [
                new ChessPattern(Vector2.ZeroByOne, repeats: RepeatBehavior.NotRepeatable),
                new ChessPattern(Vector2.OneByZero, repeats: RepeatBehavior.NotRepeatable),
                new ChessPattern(Vector2.OneByOne, repeats: RepeatBehavior.NotRepeatable)
            ],
            _ => Array.Empty<ChessPattern>()
        };
    }




    public static IEnumerable<ChessPattern> GetPatterns(ChessPiece piece)
    {
        foreach (ChessPattern pattern in GetBasePatterns(piece))
        {
            foreach (ChessPattern mirroredPattern in GetMirroredPatterns(pattern))
            {
                yield return mirroredPattern;
            }
        }
    }

    internal static IEnumerable<ChessPattern> GetMirroredPatterns(ChessPattern pattern)
    {
        yield return new ChessPattern(pattern.Delta, MirrorBehavior.None, pattern.Repeats, pattern.Captures, pattern.ForwardOnly, pattern.Jumps);

        if (pattern.Mirrors.HasFlag(MirrorBehavior.Horizontal) && pattern.Delta.X != 0)
            yield return new ChessPattern((-pattern.Delta.X, pattern.Delta.Y), MirrorBehavior.None, pattern.Repeats, pattern.Captures, pattern.ForwardOnly, pattern.Jumps);

        if (pattern.Mirrors.HasFlag(MirrorBehavior.Vertical) && pattern.Delta.Y != 0)
            yield return new ChessPattern((pattern.Delta.X, -pattern.Delta.Y), MirrorBehavior.None, pattern.Repeats, pattern.Captures, pattern.ForwardOnly, pattern.Jumps);

        if (pattern.Mirrors.HasFlag(MirrorBehavior.All) && pattern.Delta.X != 0 && pattern.Delta.Y != 0)
            yield return new ChessPattern((-pattern.Delta.X, -pattern.Delta.Y), MirrorBehavior.None, pattern.Repeats, pattern.Captures, pattern.ForwardOnly, pattern.Jumps);
    }

    public static IEnumerable<ChessAction> GetActionCandidates(ChessPiece[,] board, (int X, int Y) from, ChessPieceAttribute turnColorAttribute)
    {
        ChessPiece fromPiece = board[from.Y, from.X];
        if (fromPiece.IsEmpty) yield break;
        if (!fromPiece.IsSameColor(turnColorAttribute)) yield break;

        var boardSize = board.GetLength(0);
        int maxSteps = boardSize;

        (int x, int y) forward = fromPiece.ForwardAxis();
        foreach (ChessPattern pattern in GetPatterns(fromPiece))
        {
            int dx = pattern.Delta.X * forward.x;
            int dy = pattern.Delta.Y * forward.y;
            int x = from.X;
            int y = from.Y;
            int steps = 0;

            do
            {
                x += dx;
                y += dy;
                steps++;

                // check bounds using maxSteps as the limit
                if (x < 0 || x >= maxSteps || y < 0 || y >= maxSteps) break;
                yield return new ChessAction(new ChessPosition(from.X, from.Y), new ChessPosition(x, y));

                if (pattern.Repeats == RepeatBehavior.NotRepeatable) break;
                if (pattern.Repeats == RepeatBehavior.RepeatableOnce && steps == 1) break;
                if (pattern.Jumps) break;
                if (steps >= maxSteps) break;

            } while (true);
        }
    }

    public class ChessActionCandidate
    {
        public ChessAction Action { get; }
        public ChessPattern Pattern { get; }
        public int Steps { get; }

        public ChessActionCandidate(ChessAction action, ChessPattern pattern, int steps)
        {
            Action = action;
            Pattern = pattern;
            Steps = steps;
        }
    }

    public static IEnumerable<ChessActionCandidate> GetActionCandidates(ChessPiece[,] board, ChessPieceAttribute turnColor)
    {
        int width = board.GetLength(0);
        int height = board.GetLength(1);

        for (int row = 0; row < width; row++)
        {
            for (int col = 0; col < height; col++)
            {
                ChessPiece fromPiece = board[row, col];
                if (fromPiece.IsEmpty) yield break;
                if (!fromPiece.IsSameColor(turnColor)) yield break;

                int maxSteps = Math.Max(width, height);

                (int x, int y) forward = fromPiece.ForwardAxis();
                foreach (ChessPattern pattern in GetPatterns(fromPiece))
                {
                    int dx = pattern.Delta.X * forward.x;
                    int dy = pattern.Delta.Y * forward.y;
                    int x = row;
                    int y = col;
                    int steps = 0;

                    do
                    {
                        x += dx;
                        y += dy;
                        steps++;

                        if (x < 0 || x >= width || y < 0 || y >= height) break;

                        yield return new ChessActionCandidate(
                            new ChessAction(new ChessPosition(row, col), new ChessPosition(x, y)),
                            pattern,
                            steps
                        );

                        if (pattern.Repeats == RepeatBehavior.NotRepeatable) break;
                        if (pattern.Repeats == RepeatBehavior.RepeatableOnce && steps == 1) break;
                        if (pattern.Jumps) break;
                        if (steps >= maxSteps) break;

                    } while (true);
                }
            }
        }
    }
}

