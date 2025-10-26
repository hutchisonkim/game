namespace Game.Chess.Entity;

[Flags]
public enum ChessPieceAttribute
{
    None = 0,
    White = 1 << 0,
    Black = 1 << 1,
    Mint = 1 << 2,

    Pawn = 1 << 3,
    Rook = 1 << 4,
    Knight = 1 << 5,
    Bishop = 1 << 6,
    Queen = 1 << 7,
    King = 1 << 8
}


public readonly record struct ChessPiece(ChessPieceAttribute Attributes)
{
    public ChessPieceAttribute Attributes { get; } = Attributes;

    public readonly bool IsWhite => (Attributes & ChessPieceAttribute.White) != 0;
    public readonly bool IsMint => (Attributes & ChessPieceAttribute.Mint) != 0;

    public readonly (int X, int Y) Forward() => (1, IsWhite ? 1 : -1);

    public readonly bool IsEmpty => Attributes == ChessPieceAttribute.None;

    public static readonly ChessPiece Empty = new(ChessPieceAttribute.None);

    public readonly ChessPieceAttribute ColorAttributes => Attributes & (ChessPieceAttribute.White | ChessPieceAttribute.Black);
    public readonly ChessPieceAttribute TypeAttributes => Attributes & (ChessPieceAttribute.Pawn | ChessPieceAttribute.Rook | ChessPieceAttribute.Knight | ChessPieceAttribute.Bishop | ChessPieceAttribute.Queen | ChessPieceAttribute.King | ChessPieceAttribute.Mint);

    public readonly bool IsSameColor(ChessPieceAttribute otherColorAttributes) => (ColorAttributes & otherColorAttributes) != 0;
    public readonly bool IsSameType(ChessPieceAttribute otherTypeAttributes) => (TypeAttributes & otherTypeAttributes) != 0;
    
}
