namespace Game.Chess.Entity;

public static class ChessEntityUtility
{
    public static ChessPiece[,] CreateInitialBoard(ChessPieceAttribute pieceAttributeOverride = ChessPieceAttribute.None)
    {
        var board = new ChessPiece[8, 8];

        bool hasOverride = pieceAttributeOverride != ChessPieceAttribute.None;
        ChessPieceAttribute pieceAttr;

        // Pawns
        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Pawn;
        for (int x = 0; x < 8; x++)
        {
            board[x, 1] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.Mint | pieceAttr);
            board[x, 6] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.Mint | pieceAttr);
        }

        // Rooks
        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Rook;
        board[0, 0] = board[7, 0] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.Mint | pieceAttr);
        board[0, 7] = board[7, 7] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.Mint | pieceAttr);

        // Knights
        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Knight;
        board[1, 0] = board[6, 0] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.Mint | pieceAttr);
        board[1, 7] = board[6, 7] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.Mint | pieceAttr);

        // Bishops
        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Bishop;
        board[2, 0] = board[5, 0] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.Mint | pieceAttr);
        board[2, 7] = board[5, 7] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.Mint | pieceAttr);

        // Queens
        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Queen;
        board[3, 0] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.Mint | pieceAttr);
        board[3, 7] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.Mint | pieceAttr);

        // Kings
        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.King;
        board[4, 0] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.Mint | pieceAttr);
        board[4, 7] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.Mint | pieceAttr);

        return board;
    }

    public static class Vector2
    {
        public static readonly (int X, int Y) OneByZero = (1, 0);
        public static readonly (int X, int Y) ZeroByOne = (0, 1);
        public static readonly (int X, int Y) OneByOne = (1, 1);
        public static readonly (int X, int Y) OneByTwo = (1, 2);
        public static readonly (int X, int Y) TwoByOne = (2, 1);
        public static readonly (int X, int Y) ZeroByTwo = (0, 2);
    }

    public static IEnumerable<ChessPattern> GetBasePatterns(ChessPiece piece)
    {
        return piece.TypeAttributes switch
        {
            var t when ((t & ChessPieceAttribute.Pawn) != 0) && ((t & ChessPieceAttribute.Mint) != 0) =>
            [
                new ChessPattern(Vector2.ZeroByOne, Mirrors: MirrorBehavior.Horizontal, Repeats: false, Captures: CaptureBehavior.Move),
                new ChessPattern(Vector2.ZeroByTwo, Mirrors: MirrorBehavior.Horizontal, Repeats: false, Captures: CaptureBehavior.Move),
                new ChessPattern(Vector2.OneByOne, Mirrors: MirrorBehavior.Horizontal, Repeats: false, Captures: CaptureBehavior.Replace),
            ],
            var t when ((t & ChessPieceAttribute.Pawn) != 0) && ((t & ChessPieceAttribute.Mint) == 0) =>
            [
                new ChessPattern(Vector2.ZeroByOne, Mirrors: MirrorBehavior.Horizontal, Repeats: false, Captures: CaptureBehavior.Move),
                new ChessPattern(Vector2.OneByOne, Mirrors: MirrorBehavior.Horizontal, Repeats: false, Captures: CaptureBehavior.Replace)
            ],
            var t when (t & ChessPieceAttribute.Rook) != 0 =>
            [
                new ChessPattern(Vector2.ZeroByOne),
                new ChessPattern(Vector2.OneByZero)
            ],
            var t when (t & ChessPieceAttribute.Knight) != 0 =>
            [
                new ChessPattern(Vector2.OneByTwo, Repeats: false),
                new ChessPattern(Vector2.TwoByOne, Repeats: false)
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
                new ChessPattern(Vector2.ZeroByOne, Repeats: false),
                new ChessPattern(Vector2.OneByZero, Repeats: false),
                new ChessPattern(Vector2.OneByOne, Repeats: false)
            ],
            _ => Array.Empty<ChessPattern>()
        };
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

}
