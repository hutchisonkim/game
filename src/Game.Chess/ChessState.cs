// Game.Chess/ChessState.cs
using System;
using Game.Core;

namespace Game.Chess;

public enum PieceColor { White, Black }

public enum PieceType { Pawn, Rook, Knight, Bishop, Queen, King }

public sealed record Piece(PieceColor Color, PieceType Type);

/// <summary>
/// Very small chessboard state holding pieces in an 8x8 array.
/// Board[row, col] where row 0 is White's back rank (convention used here).
/// This class implements IState.Clone for simple branching.
/// </summary>
public sealed class ChessState : IState
{
    // 8x8 board: [row, col]
    public Piece?[,] Board { get; }

    public ChessState()
    {
        Board = new Piece?[8, 8];
        SetupInitialPosition();
    }

    private ChessState(Piece?[,] board)
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

    /// <summary>
    /// Simple deep-ish clone of the board array and pieces (pieces are immutable records).
    /// </summary>
    public IState Clone()
    {
        var copy = new Piece?[8, 8];
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                copy[r, c] = Board[r, c];
        return new ChessState(copy);
    }

    /// <summary>
    /// Helper: get piece or null
    /// </summary>
    public Piece? PieceAt(Position p) => p.IsValid ? Board[p.Row, p.Col] : null;

    /// <summary>
    /// Apply a move (mutating) — returns a new ChessState with the move applied.
    /// Note: no legality checks are performed here; use the policy to enumerate legal moves.
    /// </summary>
    public ChessState ApplyMove(ChessMove m)
    {
        if (!m.From.IsValid || !m.To.IsValid) throw new ArgumentException("Invalid positions.");
        var clone = (ChessState)Clone();
        var piece = clone.Board[m.From.Row, m.From.Col];
        clone.Board[m.From.Row, m.From.Col] = null;
        clone.Board[m.To.Row, m.To.Col] = piece;
        return clone;
    }
}
