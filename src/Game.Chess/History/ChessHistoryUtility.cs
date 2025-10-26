using Game.Chess.Entity;
namespace Game.Chess.History;

// 🔹 Domain-agnostic piece behavior using the `Piece` record (Game.Chess.Piece)
public static class ChessHistoryUtility
{
    public static class Vector2
    {
        public static readonly (int X, int Y) OneByZero = (1, 0);
        public static readonly (int X, int Y) ZeroByOne = (0, 1);
        public static readonly (int X, int Y) OneByOne = (1, 1);
        public static readonly (int X, int Y) OneByTwo = (1, 2);
        public static readonly (int X, int Y) TwoByOne = (2, 1);
        public static readonly (int X, int Y) ZeroByTwo = (0, 2);
    }

    internal static IEnumerable<ChessPattern> GetBasePatterns(ChessPiece piece)
    {
        return piece.TypeAttributes switch
        {
            var t when ((t & ChessPieceAttribute.Pawn) != 0) && ((t & ChessPieceAttribute.Mint) != 0) =>
            [
                new ChessPattern(Vector2.ZeroByOne, mirrors: MirrorBehavior.Horizontal, repeats: false, captures: CaptureBehavior.Move),
                new ChessPattern(Vector2.ZeroByTwo, mirrors: MirrorBehavior.Horizontal, repeats: false, captures: CaptureBehavior.Move),
                new ChessPattern(Vector2.OneByOne, mirrors: MirrorBehavior.Horizontal, repeats: false, captures: CaptureBehavior.Replace),
            ],
            var t when ((t & ChessPieceAttribute.Pawn) != 0) && ((t & ChessPieceAttribute.Mint) == 0) =>
            [
                new ChessPattern(Vector2.ZeroByOne, mirrors: MirrorBehavior.Horizontal, repeats: false, captures: CaptureBehavior.Move),
                new ChessPattern(Vector2.OneByOne, mirrors: MirrorBehavior.Horizontal, repeats: false, captures: CaptureBehavior.Replace)
            ],
            var t when (t & ChessPieceAttribute.Rook) != 0 =>
            [
                new ChessPattern(Vector2.ZeroByOne),
                new ChessPattern(Vector2.OneByZero)
            ],
            var t when (t & ChessPieceAttribute.Knight) != 0 =>
            [
                new ChessPattern(Vector2.OneByTwo, repeats: false),
                new ChessPattern(Vector2.TwoByOne, repeats: false)
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
                new ChessPattern(Vector2.ZeroByOne, repeats: false),
                new ChessPattern(Vector2.OneByZero, repeats: false),
                new ChessPattern(Vector2.OneByOne, repeats: false)
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
        yield return new ChessPattern(pattern.Delta, MirrorBehavior.None, pattern.Repeats, pattern.Captures);

        if (pattern.Mirrors.HasFlag(MirrorBehavior.Horizontal) && pattern.Delta.X != 0)
            yield return new ChessPattern((-pattern.Delta.X, pattern.Delta.Y), pattern.Mirrors, pattern.Repeats, pattern.Captures);

        if (pattern.Mirrors.HasFlag(MirrorBehavior.Vertical) && pattern.Delta.Y != 0)
            yield return new ChessPattern((pattern.Delta.X, -pattern.Delta.Y), pattern.Mirrors, pattern.Repeats, pattern.Captures);

        if (pattern.Mirrors.HasFlag(MirrorBehavior.Both) && pattern.Delta.X != 0 && pattern.Delta.Y != 0)
            yield return new ChessPattern((-pattern.Delta.X, -pattern.Delta.Y), pattern.Mirrors, pattern.Repeats, pattern.Captures);
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

    public static IEnumerable<ChessActionCandidate> GetActionCandidates(ChessPiece[,] board, ChessPieceAttribute turnColor, bool includeTargetless, bool includeFriendlyfire)
    {
        int width = board.GetLength(0);
        int height = board.GetLength(1);

        for (int fromX = 0; fromX < width; fromX++)
        {
            for (int fromY = 0; fromY < height; fromY++)
            {
                ChessPiece fromPiece = board[fromX, fromY];
                if (fromPiece.IsEmpty) continue;
                if (!fromPiece.IsSameColor(turnColor)) continue;

                int maxSteps = Math.Max(width, height);

                var (fx, fy) = fromPiece.Forward();
                foreach (ChessPattern pattern in GetPatterns(fromPiece))
                {
                    int dx = pattern.Delta.X * fx;
                    int dy = pattern.Delta.Y * fy;
                    int toX = fromX;
                    int toY = fromY;
                    int steps = 0;

                    do
                    {
                        toX += dx;
                        toY += dy;
                        steps++;

                        if (toY < 0 || toY >= height || toX < 0 || toX >= width) break;

                        if (pattern.Delta == Vector2.ZeroByTwo)
                        {
                            //TODO: replace this block by replacing this loop and to instead use a distributed approach
                            // using such an approach, the intermediate cell becomes one of the two cells returned by the expansion operation.
                            // this trail mechanic would also support the castling mechanic rule about king castlings forbidden across cells that are under attack
                            ChessPiece intermediatePiece = board[fromX + (dx / 2), fromY + (dy / 2)];
                            if (!intermediatePiece.IsEmpty) break;
                        }

                        ChessPiece toPiece = board[toX, toY];
                        if (toPiece.IsEmpty && pattern.Captures.HasFlag(CaptureBehavior.Replace) && !pattern.Captures.HasFlag(CaptureBehavior.Move) && !includeTargetless) break;
                        if (!toPiece.IsEmpty && !pattern.Captures.HasFlag(CaptureBehavior.Replace)) break;
                        if (!toPiece.IsEmpty && toPiece.IsSameColor(turnColor) && !includeFriendlyfire) break;

                        yield return new ChessActionCandidate(
                            new ChessAction(new ChessPosition(fromX, fromY), new ChessPosition(toX, toY)),
                            pattern,
                            steps
                        );

                        if (!toPiece.IsEmpty && pattern.Captures.HasFlag(CaptureBehavior.Move) && pattern.Captures.HasFlag(CaptureBehavior.Replace)) break;
                        if (!pattern.Repeats) break;
                        if (steps >= maxSteps) break;

                    } while (true);
                }
            }
        }
    }

}

