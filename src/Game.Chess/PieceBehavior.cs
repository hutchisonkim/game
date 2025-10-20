//src\Game.Chess\PieceBehavior.cs

using System.Linq;

namespace Game.Chess.Policy;

// 🔹 Domain-agnostic piece behavior using the `Piece` record (Game.Chess.Piece)
public static class PieceBehavior
{
    public static char ToFenChar(PieceAttribute typeFlag, bool isWhite)
    {
        char fenChar = typeFlag switch
        {
            var t when (t & PieceAttribute.Pawn) != 0 => 'P',
            var t when (t & PieceAttribute.Rook) != 0 => 'R',
            var t when (t & PieceAttribute.King) != 0 => 'K',
            var t when (t & PieceAttribute.Queen) != 0 => 'Q',
            var t when (t & PieceAttribute.Bishop) != 0 => 'B',
            var t when (t & PieceAttribute.Knight) != 0 => 'N',
            _ => throw new ArgumentOutOfRangeException(nameof(typeFlag))
        };
        return isWhite ? fenChar : char.ToLower(fenChar);
    }

    public static string PieceTypeDescription(Piece piece) => $"{ToFenChar(piece.TypeFlag, piece.IsWhite)}";

    public static (int X, int Y) ForwardAxis(Piece piece) => (1, piece.IsWhite ? 1 : -1);

    public static class Vector2
    {
        public static readonly (int X, int Y) OneByZero = (1, 0);
        public static readonly (int X, int Y) ZeroByOne = (0, 1);
        public static readonly (int X, int Y) OneByOne = (1, 1);
        public static readonly (int X, int Y) OneByTwo = (1, 2);
    }

    // Returns all abstract moves for the piece based on its type and attributes
    public static IEnumerable<Pattern> GetPatternsFor(Piece piece)
    {
        return piece.TypeFlag switch
        {
            var t when (t & PieceAttribute.Pawn) != 0 =>
            [
                new Pattern(new Pattern.PatternOptions(Vector2.ZeroByOne, Mirrors: Pattern.MirrorBehavior.Horizontal, Repeats: Pattern.RepeatBehavior.NotRepeatable, Captures: Pattern.CaptureBehavior.MoveOnly, ForwardOnly: true)),
                new Pattern(new Pattern.PatternOptions(Vector2.ZeroByOne, Mirrors: Pattern.MirrorBehavior.Horizontal, Repeats: Pattern.RepeatBehavior.RepeatableOnce, Captures: Pattern.CaptureBehavior.MoveOnly, ForwardOnly: true)),
                new Pattern(new Pattern.PatternOptions(Vector2.OneByOne, Mirrors: Pattern.MirrorBehavior.Horizontal, Repeats: Pattern.RepeatBehavior.NotRepeatable, Captures: Pattern.CaptureBehavior.CaptureOnly, ForwardOnly: true))
            ],
            var t when (t & PieceAttribute.Rook) != 0 =>
            [
                new Pattern(new Pattern.PatternOptions(Vector2.ZeroByOne)),
                new Pattern(new Pattern.PatternOptions(Vector2.OneByZero))
            ],
            var t when (t & PieceAttribute.Knight) != 0 =>
            [
                new Pattern(new Pattern.PatternOptions(Vector2.OneByTwo, Repeats: Pattern.RepeatBehavior.NotRepeatable, Jumps: true))
            ],
            var t when (t & PieceAttribute.Bishop) != 0 =>
            [
                new Pattern(new Pattern.PatternOptions(Vector2.OneByOne))
            ],
            var t when (t & PieceAttribute.Queen) != 0 =>
            [
                new Pattern(new Pattern.PatternOptions(Vector2.ZeroByOne)),
                new Pattern(new Pattern.PatternOptions(Vector2.OneByZero)),
                new Pattern(new Pattern.PatternOptions(Vector2.OneByOne))
            ],
            var t when (t & PieceAttribute.King) != 0 =>
            [
                new Pattern(new Pattern.PatternOptions(Vector2.ZeroByOne, Repeats: Pattern.RepeatBehavior.NotRepeatable)),
                new Pattern(new Pattern.PatternOptions(Vector2.OneByZero, Repeats: Pattern.RepeatBehavior.NotRepeatable)),
                new Pattern(new Pattern.PatternOptions(Vector2.OneByOne, Repeats: Pattern.RepeatBehavior.NotRepeatable))
            ],
            _ => Array.Empty<Pattern>()
        };
    }

    // Convert Pattern to Game.Core.PatternDto and expose a helper for pieces
    public static Game.Core.PatternDto ToPatternDto(Pattern pattern)
    {
        return new Game.Core.PatternDto(
            Vector: pattern.Vector,
            Mirrors: (Game.Core.MirrorBehavior)pattern.Mirrors,
            Repeats: (Game.Core.RepeatBehavior)pattern.Repeats,
            Captures: (Game.Core.CaptureBehavior)pattern.Captures,
            ForwardOnly: pattern.ForwardOnly,
            Jumps: pattern.Jumps
        );
    }

    public static IEnumerable<Game.Core.PatternDto> GetPatternDtosFor(Piece piece)
    {
        return GetPatternsFor(piece).Select(ToPatternDto);
    }

    // Returns all legal moves given the board and current position for a generic Piece record
    public static IEnumerable<(int row, int col, Pattern move)> GetAvailableActions(Piece piece, ChessState board, int row, int col, bool forceIncludeCaptures = false, bool forceExcludeMoves = false)
    {
        foreach (var basePatterns in GetPatternsFor(piece))
        {
            var directions = GetMirroredVectors(basePatterns);

            foreach (((int X, int Y) Vector, Pattern.MirrorBehavior Mirrors) direction in directions)
            {
                (int X, int Y) forward = ForwardAxis(piece);
                int dx = direction.Vector.X * forward.X;
                int dy = direction.Vector.Y * forward.Y;
                int x = col;
                int y = row;

                int steps = 0;
                do
                {
                    x += dx;
                    y += dy;
                    steps++;

                    if (!board.IsInside(y, x)) break;

                    var pieceTo = board[y, x];

                    if (pieceTo == null)
                    {
                        if (basePatterns.Captures == Pattern.CaptureBehavior.MoveOnly || basePatterns.Captures == Pattern.CaptureBehavior.MoveOrCapture)
                        {
                            if (!forceExcludeMoves)
                            {
                                yield return (y, x, basePatterns);
                            }
                        }
                        if (forceIncludeCaptures)
                        {
                            if (basePatterns.Captures == Pattern.CaptureBehavior.CaptureOnly || basePatterns.Captures == Pattern.CaptureBehavior.MoveOrCapture)
                            {
                                yield return (y, x, basePatterns);
                            }
                        }
                    }
                    else
                    {
                        if ((pieceTo.TypeFlag & piece.TypeFlag) == 0 ? pieceTo.IsWhite != piece.IsWhite : (pieceTo.ColorFlag & piece.ColorFlag) == 0)
                        {
                            if (basePatterns.Captures == Pattern.CaptureBehavior.CaptureOnly || basePatterns.Captures == Pattern.CaptureBehavior.MoveOrCapture)
                            {
                                yield return (y, x, basePatterns);
                            }
                        }
                        break; // cannot jump over except knights
                    }

                    if (basePatterns.Repeats == Pattern.RepeatBehavior.NotRepeatable ||
                        (basePatterns.Repeats == Pattern.RepeatBehavior.RepeatableOnce && steps == 1) ||
                        basePatterns.Jumps)
                        break;

                } while (true);
            }
        }
    }

    private static IEnumerable<((int X, int Y) Vector, Pattern.MirrorBehavior Mirrors)> GetMirroredVectors(Pattern pattern)
    {
        yield return (pattern.Vector, pattern.Mirrors);

        if (pattern.Mirrors.HasFlag(Pattern.MirrorBehavior.Horizontal) && pattern.Vector.X != 0)
            yield return ((-pattern.Vector.X, pattern.Vector.Y), pattern.Mirrors);

        if (pattern.Mirrors.HasFlag(Pattern.MirrorBehavior.Vertical) && pattern.Vector.Y != 0)
            yield return ((pattern.Vector.X, -pattern.Vector.Y), pattern.Mirrors);

        if (pattern.Mirrors.HasFlag(Pattern.MirrorBehavior.All) && pattern.Vector.X != 0 && pattern.Vector.Y != 0)
            yield return ((-pattern.Vector.X, -pattern.Vector.Y), pattern.Mirrors);
    }
}

