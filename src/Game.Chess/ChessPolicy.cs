using Game.Core;

namespace Game.Chess;

/// <summary>
/// Chess policy that enumerates legal moves for all pieces on the board.
/// This implementation focuses on move generation rules:
/// - Pawn: forward moves (single, double from starting rank) and captures (no en-passant)
/// - Rook, Bishop, Queen: sliding pieces that stop at first piece; capture allowed
/// - Knight: L-shaped jumps
/// - King: one-square moves (no castling implemented)
/// This is intentionally minimal but complete for basic legal move generation.
/// </summary>
public sealed class ChessRules : IPolicy<ChessBoard_Old, ChessMove>
{
    public IEnumerable<ChessMove> GetAvailableActions(ChessBoard_Old state)
    {
        // Determine side to move from TurnCount: even -> White, odd -> Black
        var sideToMove = (state.TurnCount % 2 == 0) ? PieceColor.White : PieceColor.Black;

        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var from = new Position(r, c);
                var piece = state.PieceAt(from);
                if (piece == null) continue;

                // only generate moves for the side to move
                if (piece.Color != sideToMove) continue;

                foreach (var mv in GetMovesForPiece(state, from, piece))
                    yield return mv;
            }
        }
    }

    private static IEnumerable<ChessMove> GetMovesForPiece(ChessBoard_Old state, Position from, Piece piece)
    {
        return piece.Type switch
        {
            PieceType.Pawn => PawnMoves(state, from, piece),
            PieceType.Rook => PatternMoves(state, from, piece, MovePatterns.Rook),
            PieceType.Bishop => PatternMoves(state, from, piece, MovePatterns.Bishop),
            PieceType.Queen => PatternMoves(state, from, piece, MovePatterns.Queen),
            PieceType.Knight => PatternMoves(state, from, piece, MovePatterns.Knight),
            PieceType.King => PatternMoves(state, from, piece, MovePatterns.King),
            _ => []
        };
    }

    // Direction vectors are now provided by MovePatterns in MovePattern.cs

    #region Move Generators

    private static IEnumerable<ChessMove> PatternMoves(ChessBoard_Old state, Position from, Piece piece, IMovePattern pattern)
    {
        foreach (var (dr, dc) in pattern.GetVectors())
        {
            int r = from.Row + dr, c = from.Col + dc;

            while (r >= 0 && r < 8 && c >= 0 && c < 8)
            {
                var to = new Position(r, c);
                var target = state.PieceAt(to);

                if (pattern.MoveOnly)
                {
                    if (target == null)
                        yield return new ChessMove(from, to);
                    else
                        break; // blocked
                }
                else if (pattern.CaptureOnly)
                {
                    if (target != null && target.Color != piece.Color)
                        yield return new ChessMove(from, to);
                    if (target != null) break;
                }
                else
                {
                    if (target == null)
                        yield return new ChessMove(from, to);
                    else
                    {
                        if (target.Color != piece.Color)
                            yield return new ChessMove(from, to);
                        break; // blocked by any piece
                    }
                }

                if (!pattern.IsRepeatable) break;
                r += dr; c += dc;
            }
        }
    }

    // Knight and King move logic is handled by PatternMoves with MovePatterns.Knight and MovePatterns.King.

    private static IEnumerable<ChessMove> PawnMoves(ChessBoard_Old state, Position from, Piece piece)
    {
        int dir = piece.Color == PieceColor.White ? +1 : -1;
        int startRow = piece.Color == PieceColor.White ? 1 : 6;

        // one forward
        var oneFwd = new Position(from.Row + dir, from.Col);
        if (oneFwd.IsValid && state.PieceAt(oneFwd) == null)
        {
            yield return new ChessMove(from, oneFwd);

            // two-forward from starting rank (no obstruction)
            var twoFwd = new Position(from.Row + 2 * dir, from.Col);
            if (from.Row == startRow && twoFwd.IsValid && state.PieceAt(twoFwd) == null)
                yield return new ChessMove(from, twoFwd);
        }

        // captures
        var diagLeft = new Position(from.Row + dir, from.Col - 1);
        var diagRight = new Position(from.Row + dir, from.Col + 1);

        if (diagLeft.IsValid)
        {
            var t = state.PieceAt(diagLeft);
            if (t != null && t.Color != piece.Color)
                yield return new ChessMove(from, diagLeft);
        }
        if (diagRight.IsValid)
        {
            var t = state.PieceAt(diagRight);
            if (t != null && t.Color != piece.Color)
                yield return new ChessMove(from, diagRight);
        }
    }

    #endregion
}
