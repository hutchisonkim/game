//src\Game.Chess\ChessBehavior.cs

namespace Game.Chess.Policy;

// 🔹 Domain-agnostic piece behavior using the `Piece` record (Game.Chess.Piece)
public static class ChessBehavior
{
    public static (int X, int Y) ForwardAxis(Piece piece) => (1, piece.IsWhite ? 1 : -1); // determines the initial pattern direction that gets tiled (used for pawn movement that is forward-only)
    public static bool IsClockwise(Piece piece) => piece.IsWhite ? true : false; // determines the initial queen placement (white mirrors black)


    public static class Vector2
    {
        public static readonly (int X, int Y) OneByZero = (1, 0);
        public static readonly (int X, int Y) ZeroByOne = (0, 1);
        public static readonly (int X, int Y) OneByOne = (1, 1);
        public static readonly (int X, int Y) OneByTwo = (1, 2);
    }

    // Returns all abstract moves for the piece based on its type and attributes
    internal static IEnumerable<Pattern> GetPatternsFor(Piece piece)
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
    internal static Core.PatternDto ToPatternDto(Pattern pattern)
    {
        return new Core.PatternDto(
            Vector: pattern.Vector,
            Mirrors: (Core.MirrorBehavior)pattern.Mirrors,
            Repeats: (Core.RepeatBehavior)pattern.Repeats,
            Captures: (Core.CaptureBehavior)pattern.Captures,
            ForwardOnly: pattern.ForwardOnly,
            Jumps: pattern.Jumps
        );
    }

    public static IEnumerable<Core.PatternDto> GetPatternDtosFor(Piece piece)
    {
        return GetPatternsFor(piece).Select(ToPatternDto);
    }
}

