//src\Game.Chess\ChessBehavior.cs

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

    // Returns all abstract moves for the piece based on its type and attributes
    internal static IEnumerable<ChessPattern> GetPatternsFor(ChessPiece piece)
    {
        return piece.TypeFlag switch
        {
            var t when (t & ChessPieceAttribute.Pawn) != 0 =>
            [
                new ChessPattern(new ChessPattern.PatternOptions(Vector2.ZeroByOne, Mirrors: ChessPattern.MirrorBehavior.Horizontal, Repeats: ChessPattern.RepeatBehavior.NotRepeatable, Captures: ChessPattern.CaptureBehavior.MoveOnly, ForwardOnly: true)),
                new ChessPattern(new ChessPattern.PatternOptions(Vector2.ZeroByOne, Mirrors: ChessPattern.MirrorBehavior.Horizontal, Repeats: ChessPattern.RepeatBehavior.RepeatableOnce, Captures: ChessPattern.CaptureBehavior.MoveOnly, ForwardOnly: true)),
                new ChessPattern(new ChessPattern.PatternOptions(Vector2.OneByOne, Mirrors: ChessPattern.MirrorBehavior.Horizontal, Repeats: ChessPattern.RepeatBehavior.NotRepeatable, Captures: ChessPattern.CaptureBehavior.CaptureOnly, ForwardOnly: true))
            ],
            var t when (t & ChessPieceAttribute.Rook) != 0 =>
            [
                new ChessPattern(new ChessPattern.PatternOptions(Vector2.ZeroByOne)),
                new ChessPattern(new ChessPattern.PatternOptions(Vector2.OneByZero))
            ],
            var t when (t & ChessPieceAttribute.Knight) != 0 =>
            [
                new ChessPattern(new ChessPattern.PatternOptions(Vector2.OneByTwo, Repeats: ChessPattern.RepeatBehavior.NotRepeatable, Jumps: true))
            ],
            var t when (t & ChessPieceAttribute.Bishop) != 0 =>
            [
                new ChessPattern(new ChessPattern.PatternOptions(Vector2.OneByOne))
            ],
            var t when (t & ChessPieceAttribute.Queen) != 0 =>
            [
                new ChessPattern(new ChessPattern.PatternOptions(Vector2.ZeroByOne)),
                new ChessPattern(new ChessPattern.PatternOptions(Vector2.OneByZero)),
                new ChessPattern(new ChessPattern.PatternOptions(Vector2.OneByOne))
            ],
            var t when (t & ChessPieceAttribute.King) != 0 =>
            [
                new ChessPattern(new ChessPattern.PatternOptions(Vector2.ZeroByOne, Repeats: ChessPattern.RepeatBehavior.NotRepeatable)),
                new ChessPattern(new ChessPattern.PatternOptions(Vector2.OneByZero, Repeats: ChessPattern.RepeatBehavior.NotRepeatable)),
                new ChessPattern(new ChessPattern.PatternOptions(Vector2.OneByOne, Repeats: ChessPattern.RepeatBehavior.NotRepeatable))
            ],
            _ => Array.Empty<ChessPattern>()
        };
    }

    // Convert Pattern to Game.Core.PatternDto and expose a helper for pieces
    internal static Core.PatternDto ToPatternDto(ChessPattern pattern)
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

    public static IEnumerable<Core.PatternDto> GetPatternDtosFor(ChessPiece piece)
    {
        return GetPatternsFor(piece).Select(ToPatternDto);
    }
}

