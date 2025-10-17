using Game.Core;

namespace Game.Chess;

public enum PieceColor { White, Black }

public enum PieceType { Pawn, Rook, Knight, Bishop, Queen, King }

public sealed record Piece(PieceColor Color, PieceType Type);
