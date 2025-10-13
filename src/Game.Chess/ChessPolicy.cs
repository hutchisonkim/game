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
public sealed class ChessRules : IPolicy<ChessBoard, ChessMove>
{
    public IEnumerable<ChessMove> GetAvailableActions(ChessBoard state)
    {
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var from = new Position(r, c);
                var piece = state.PieceAt(from);
                if (piece == null) continue;

                foreach (var mv in GetMovesForPiece(state, from, piece))
                    yield return mv;
            }
        }
    }

    private static IEnumerable<ChessMove> GetMovesForPiece(ChessBoard state, Position from, Piece piece)
    {
        return piece.Type switch
        {
            PieceType.Pawn => PawnMoves(state, from, piece),
            PieceType.Rook => SlidingMoves(state, from, piece, directionsRook),
            PieceType.Bishop => SlidingMoves(state, from, piece, directionsBishop),
            PieceType.Queen => SlidingMoves(state, from, piece, directionsQueen),
            PieceType.Knight => KnightMoves(state, from, piece),
            PieceType.King => KingMoves(state, from, piece),
            _ => []
        };
    }

    #region Directions

    private static readonly (int dr, int dc)[] directionsRook = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
    private static readonly (int dr, int dc)[] directionsBishop = new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) };
    private static readonly (int dr, int dc)[] directionsQueen = new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1,1), (1,-1), (-1,1), (-1,-1) };

    #endregion

    #region Move Generators

    private static IEnumerable<ChessMove> SlidingMoves(ChessBoard state, Position from, Piece piece, (int dr, int dc)[] directions)
    {
        foreach (var (dr, dc) in directions)
        {
            int r = from.Row + dr, c = from.Col + dc;
            while (r >= 0 && r < 8 && c >= 0 && c < 8)
            {
                var target = state.PieceAt(new Position(r, c));
                if (target == null)
                {
                    yield return new ChessMove(from, new Position(r, c));
                }
                else
                {
                    if (target.Color != piece.Color)
                        yield return new ChessMove(from, new Position(r, c));
                    break; // blocked
                }

                r += dr; c += dc;
            }
        }
    }

    private static IEnumerable<ChessMove> KnightMoves(ChessBoard state, Position from, Piece piece)
    {
        var jumps = new (int dr, int dc)[]
        {
            (2,1),(1,2),(-1,2),(-2,1),(-2,-1),(-1,-2),(1,-2),(2,-1)
        };

        foreach (var (dr, dc) in jumps)
        {
            var to = new Position(from.Row + dr, from.Col + dc);
            if (!to.IsValid) continue;
            var target = state.PieceAt(to);
            if (target == null || target.Color != piece.Color)
                yield return new ChessMove(from, to);
        }
    }

    private static IEnumerable<ChessMove> KingMoves(ChessBoard state, Position from, Piece piece)
    {
        for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                var to = new Position(from.Row + dr, from.Col + dc);
                if (!to.IsValid) continue;
                var target = state.PieceAt(to);
                if (target == null || target.Color != piece.Color)
                    yield return new ChessMove(from, to);
            }
    }

    private static IEnumerable<ChessMove> PawnMoves(ChessBoard state, Position from, Piece piece)
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
