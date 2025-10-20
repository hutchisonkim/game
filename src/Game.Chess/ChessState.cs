using Game.Core;

namespace Game.Chess;
// PieceColor and PieceType enums removed — using `PieceAttribute` flags instead

[Flags]
public enum PieceAttribute
{
    None = 0,
    White = 1 << 0,
    Black = 1 << 1,
    Demoted = 1 << 2,

    Pawn = 1 << 3,
    Rook = 1 << 4,
    Knight = 1 << 5,
    Bishop = 1 << 6,
    Queen = 1 << 7,
    King = 1 << 8
}


public sealed record Piece(PieceAttribute Attributes)
{
    // The backing attribute set
    public PieceAttribute Attributes { get; } = Attributes;

    public bool IsWhite => (Attributes & PieceAttribute.White) != 0;
    public bool IsBlack => (Attributes & PieceAttribute.Black) != 0;

    public PieceAttribute ColorFlag => Attributes & (PieceAttribute.White | PieceAttribute.Black);
    public PieceAttribute TypeFlag => Attributes & (PieceAttribute.Pawn | PieceAttribute.Rook | PieceAttribute.Knight | PieceAttribute.Bishop | PieceAttribute.Queen | PieceAttribute.King);
}
