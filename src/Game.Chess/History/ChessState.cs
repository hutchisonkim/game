using Game.Core;
using Game.Chess.Entity;
using static Game.Chess.History.ChessHistoryUtility;

namespace Game.Chess.History;

// 🔹 Chess Board
public class ChessState : IState<ChessAction, ChessState>
{
    public ChessBoard Board { get; private set; }
    public int TurnCount { get; private set; }
    public ChessPieceAttribute PieceAttributeOverride { get; private set; }
    public bool UpsTurns { get; private set; }

    public ChessState(ChessPieceAttribute pieceAttributeOverride = ChessPieceAttribute.None, bool upsTurns = true)
    {
        Board = ChessBoard.Default;
        Board.Initialize(pieceAttributeOverride);

        PieceAttributeOverride = pieceAttributeOverride;
        UpsTurns = upsTurns;
    }

    public ChessState Clone()
    {
        return new ChessState(PieceAttributeOverride, UpsTurns)
        {
            Board = Board,
            TurnCount = TurnCount
        };
    }

    public ChessState Apply(ChessAction chessAction)
    {
        if (!Board.IsInside(chessAction.From.X, chessAction.From.Y) || !Board.IsInside(chessAction.To.X, chessAction.To.Y))
            throw new ArgumentException("Invalid move positions.");

        ChessPiece piece = Board.Cell[chessAction.From.X, chessAction.From.Y];
        if (piece.IsEmpty)
            throw new InvalidOperationException("No piece at the source position.");

        ChessState newBoard = new();
        Array.Copy(Board.Cell, newBoard.Board.Cell, Board.Cell.Length);

        // Move the piece — when a piece moves, it ceases to be "Mint"
        var movedAttributes = piece.Attributes & ~ChessPieceAttribute.Mint;
        newBoard.Board.Cell[chessAction.To.X, chessAction.To.Y] = new ChessPiece(movedAttributes);
        newBoard.Board.Cell[chessAction.From.X, chessAction.From.Y] = ChessPiece.Empty;

        if (UpsTurns)
        {
            newBoard.TurnCount = TurnCount + 1;
        }

        return newBoard;
    }

    public ChessPieceAttribute TurnColor => (TurnCount % 2 == 0) ? ChessPieceAttribute.White : ChessPieceAttribute.Black;

    public IEnumerable<ChessActionCandidate> GetActionCandidates(ChessPieceAttribute colorOverride = ChessPieceAttribute.None) =>
        ChessHistoryUtility.GetActionCandidates(Board.Cell, colorOverride == ChessPieceAttribute.None ? TurnColor : colorOverride, false, false);

    public IEnumerable<ChessActionCandidate> GetAttackingActionCandidates(ChessPieceAttribute attackingColor, bool includeTargetless, bool includeFriendlyfire) =>
        ChessHistoryUtility.GetActionCandidates(Board.Cell, attackingColor, includeTargetless, includeFriendlyfire)
            .Where(candidate => candidate.Pattern.CanCapture);

}

