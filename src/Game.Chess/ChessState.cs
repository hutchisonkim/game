using Game.Core;

namespace Game.Chess;

public enum PieceColor { White, Black }

public enum PieceType { Pawn, Rook, Knight, Bishop, Queen, King }

public sealed record Piece(PieceColor Color, PieceType Type);

public sealed class ChessBoard : IState<ChessMove, ChessBoard>
{
    // 8x8 board: [row, col]
    public Piece?[,] Board { get; }

    public ChessBoard()
    {
        Board = new Piece?[8, 8];
        SetupInitialPosition();
    }

    private ChessBoard(Piece?[,] board)
    {
        Board = board;
    }

    private void SetupInitialPosition()
    {
        // White pieces (convention: White on rows 0-1, Black on rows 6-7)
        // Pawns
        for (int c = 0; c < 8; c++)
        {
            Board[1, c] = new Piece(PieceColor.White, PieceType.Pawn);
            Board[6, c] = new Piece(PieceColor.Black, PieceType.Pawn);
        }

        // White back rank (row 0)
        Board[0, 0] = new Piece(PieceColor.White, PieceType.Rook);
        Board[0, 1] = new Piece(PieceColor.White, PieceType.Knight);
        Board[0, 2] = new Piece(PieceColor.White, PieceType.Bishop);
        Board[0, 3] = new Piece(PieceColor.White, PieceType.Queen);
        Board[0, 4] = new Piece(PieceColor.White, PieceType.King);
        Board[0, 5] = new Piece(PieceColor.White, PieceType.Bishop);
        Board[0, 6] = new Piece(PieceColor.White, PieceType.Knight);
        Board[0, 7] = new Piece(PieceColor.White, PieceType.Rook);

        // Black back rank (row 7)
        Board[7, 0] = new Piece(PieceColor.Black, PieceType.Rook);
        Board[7, 1] = new Piece(PieceColor.Black, PieceType.Knight);
        Board[7, 2] = new Piece(PieceColor.Black, PieceType.Bishop);
        Board[7, 3] = new Piece(PieceColor.Black, PieceType.Queen);
        Board[7, 4] = new Piece(PieceColor.Black, PieceType.King);
        Board[7, 5] = new Piece(PieceColor.Black, PieceType.Bishop);
        Board[7, 6] = new Piece(PieceColor.Black, PieceType.Knight);
        Board[7, 7] = new Piece(PieceColor.Black, PieceType.Rook);
    }

    public ChessBoard Clone()
    {
        var copy = new Piece?[8, 8];
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                copy[r, c] = Board[r, c];
        return new ChessBoard(copy);
    }

    public Piece? PieceAt(Position p) => p.IsValid ? Board[p.Row, p.Col] : null;

    public ChessBoard Apply(ChessMove m)
    {
        if (!m.From.IsValid || !m.To.IsValid) throw new ArgumentException("Invalid positions.");
        var clone = Clone();
        var piece = clone.Board[m.From.Row, m.From.Col];
        clone.Board[m.From.Row, m.From.Col] = null;
        clone.Board[m.To.Row, m.To.Col] = piece;
        return clone;
    }

    ChessBoard IState<ChessMove, ChessBoard>.Apply(ChessMove m)
    {
        return Apply(m);
    }
}
