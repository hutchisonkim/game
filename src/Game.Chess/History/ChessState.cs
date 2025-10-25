using Game.Core;
using Game.Chess.Entity;
using static Game.Chess.History.ChessHistoryUtility;

namespace Game.Chess.History;

// 🔹 Chess Board
public class ChessState : IState<ChessAction, ChessState>
{
    private readonly ChessPiece[,] _board = new ChessPiece[8, 8];

    public ChessPiece[,] Board => _board;
    public int TurnCount { get; private set; }
    public bool UpsTurns { get; set; } = true;

    public ChessState()
    {
        InitializeBoard();
    }


    public ChessPiece this[int x, int y] => _board[x, y];

    public bool IsInside(int x, int y) => x >= 0 && x < 8 && y >= 0 && y < 8;

    public ChessState Clone()
    {
        var copy = new ChessPiece[8, 8];
        Array.Copy(_board, copy, _board.Length);
        var newBoard = new ChessState();
        Array.Copy(copy, newBoard._board, copy.Length);
        newBoard.TurnCount = TurnCount;
        newBoard.UpsTurns = UpsTurns;
        return newBoard;
    }

    public ChessState Apply(ChessAction chessAction)
    {
        if (!IsInside(chessAction.From.X, chessAction.From.Y) || !IsInside(chessAction.To.X, chessAction.To.Y))
            throw new ArgumentException("Invalid move positions.");

        ChessPiece piece = _board[chessAction.From.X, chessAction.From.Y];
        if (piece.IsEmpty)
            throw new InvalidOperationException("No piece at the source position.");

        ChessState newBoard = new();
        Array.Copy(_board, newBoard._board, _board.Length);

        // Move the piece
        newBoard._board[chessAction.To.X, chessAction.To.Y] = piece;
        newBoard._board[chessAction.From.X, chessAction.From.Y] = ChessPiece.Empty;

        if (UpsTurns)
        {
            newBoard.TurnCount = TurnCount + 1;
        }

        return newBoard;
    }

    public ChessPieceAttribute TurnColor => (TurnCount % 2 == 0) ? ChessPieceAttribute.White : ChessPieceAttribute.Black;

    public IEnumerable<ChessActionCandidate> GetActionCandidates(ChessPieceAttribute colorOverride = ChessPieceAttribute.None) =>
        ChessHistoryUtility.GetActionCandidates(_board, colorOverride == ChessPieceAttribute.None ? TurnColor : colorOverride, false, false);

    public IEnumerable<ChessActionCandidate> GetAttackingActionCandidates(ChessPieceAttribute attackingColor, bool includeTargetless, bool includeFriendlyfire) =>
        ChessHistoryUtility.GetActionCandidates(_board, attackingColor, includeTargetless, includeFriendlyfire)
            .Where(candidate => candidate.Pattern.Captures != CaptureBehavior.Move);

    public IEnumerable<ChessActionCandidate> GetThreateningActionCandidates(ChessPieceAttribute threateningColor, bool includeTargetless, bool includeFriendlyfire) =>
        GetAttackingActionCandidates(threateningColor, includeTargetless, includeFriendlyfire)
            .Where(candidate =>
            {
                var threatenedPiece = this[candidate.Action.To.X, candidate.Action.To.Y];
                return !threatenedPiece.IsEmpty && !threatenedPiece.IsSameColor(threateningColor);
            });

    public IEnumerable<ChessActionCandidate> GetCheckingActionCandidates(ChessPieceAttribute checkingColor, bool includeTargetless, bool includeFriendlyfire) =>
        GetThreateningActionCandidates(checkingColor, includeTargetless, includeFriendlyfire)
            .Where(candidate =>
            {
                var threatenedPiece = this[candidate.Action.To.X, candidate.Action.To.Y];
                return (threatenedPiece.TypeFlag & ChessPieceAttribute.King) != 0;
            });


    public void InitializeBoard(ChessPieceAttribute pieceAttributeOverride = ChessPieceAttribute.None)
    {
        var initial = ChessStateUtility.CreateInitialBoard(pieceAttributeOverride);
        Array.Copy(initial, _board, _board.Length);
        TurnCount = 0;
    }


}

