using Game.Core;
using Game.Chess.Entity;
using static Game.Chess.History.ChessHistoryUtility;

namespace Game.Chess.History;

public class ChessState : IState<ChessAction, ChessState>
{
    public ChessBoard Board { get; private set; }
    public int TurnCount { get; private set; }
    public ChessPieceAttribute PieceAttributeOverride { get; private set; }

    public ChessState(ChessPieceAttribute pieceAttributeOverride = ChessPieceAttribute.None)
    {
        Board = ChessBoard.Default;
        Board.Initialize(pieceAttributeOverride);

        PieceAttributeOverride = pieceAttributeOverride;
    }

    public ChessState Clone()
    {
        ChessPiece[,] newCells = (ChessPiece[,])Board.Cell.Clone(); // clones the array instance and its values
        ChessBoard newBoard = new(Board.Width, Board.Height, newCells);

        return new ChessState(PieceAttributeOverride)
        {
            Board = newBoard,
            TurnCount = TurnCount
        };
    }

    public ChessState Apply(ChessAction chessAction)
    {
        ChessPiece piece = Board.Cell[chessAction.From.X, chessAction.From.Y];
        ChessState newBoard = Clone();

        // Move the piece — when a piece moves, it ceases to be "Mint"
        ChessPieceAttribute newPieceAttributes = piece.Attributes & ~ChessPieceAttribute.Mint;

        newBoard.Board.Cell[chessAction.To.X, chessAction.To.Y] = new ChessPiece(newPieceAttributes);
        newBoard.Board.Cell[chessAction.From.X, chessAction.From.Y] = ChessPiece.Empty;

        newBoard.TurnCount = TurnCount + 1;

        return newBoard;
    }

    public ChessPieceAttribute TurnColor => (TurnCount % 2 == 0) ? ChessPieceAttribute.White : ChessPieceAttribute.Black;

    public IEnumerable<ChessActionCandidate> GetActionCandidates(ChessPieceAttribute colorOverride = ChessPieceAttribute.None) =>
        ChessHistoryUtility.GetActionCandidates(Board.Cell, colorOverride == ChessPieceAttribute.None ? TurnColor : colorOverride, false, false);

    public IEnumerable<ChessActionCandidate> GetAttackingActionCandidates(ChessPieceAttribute attackingColor, bool includeTargetless, bool includeFriendlyfire) =>
        ChessHistoryUtility.GetActionCandidates(Board.Cell, attackingColor, includeTargetless, includeFriendlyfire)
            .Where(candidate => candidate.Pattern.CanCapture);

}

