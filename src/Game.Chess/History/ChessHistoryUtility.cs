using Game.Chess.Entity;
namespace Game.Chess.History;

public static class ChessHistoryUtility
{
    public static IEnumerable<ChessPattern> GetPatterns(ChessPiece piece)
    {
        foreach (ChessPattern pattern in ChessEntityUtility.GetBasePatterns(piece))
        {
            foreach (ChessPattern mirroredPattern in ChessEntityUtility.GetMirroredPatterns(pattern))
            {
                yield return mirroredPattern;
            }
        }
    }

    public class ChessActionCandidate(ChessAction action, ChessPattern pattern, int steps)
    {
        public ChessAction Action { get; } = action;
        public ChessPattern Pattern { get; } = pattern;
        public int Steps { get; } = steps;
    }

    public static IEnumerable<ChessActionCandidate> GetActionCandidates(ChessPiece[,] board, ChessPieceAttribute turnColor, bool includeTargetless, bool includeFriendlyfire)
    {
        int width = board.GetLength(0);
        int height = board.GetLength(1);

        for (int fromX = 0; fromX < width; fromX++)
        {
            for (int fromY = 0; fromY < height; fromY++)
            {
                //TODO: replace these loops to instead use a distributed approach
                // using such an approach, the intermediate cell becomes one of the two cells returned by the expansion operation.
                // this trail mechanic would also support the castling mechanic rule about king castlings forbidden across cells that are under attack
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

                        if (pattern.Delta == ChessEntityUtility.Vector2.ZeroByTwo)
                        {
                            ChessPiece intermediatePiece = board[fromX + (dx / 2), fromY + (dy / 2)];
                            if (!intermediatePiece.IsEmpty) break;
                        }

                        ChessPiece toPiece = board[toX, toY];
                        if (toPiece.IsEmpty && pattern.Captures.HasFlag(CaptureBehavior.Replace) && !pattern.Captures.HasFlag(CaptureBehavior.Move) && !includeTargetless) break;
                        if (!toPiece.IsEmpty && !pattern.Captures.HasFlag(CaptureBehavior.Replace)) break;
                        if (!toPiece.IsEmpty && toPiece.IsSameColor(turnColor) && !includeFriendlyfire) break;

                        yield return new ChessActionCandidate(
                            new ChessAction((fromX, fromY), (toX, toY)),
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

