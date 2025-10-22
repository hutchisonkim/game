//src\Game.Chess\ChessPolicy.cs

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
        TurnCount = 0;

        InitializeBoard();
        // PrintBoard();
    }

    public void InitializeBoard()
    {

        for (int x = 0; x < 8; x++)
        {
            _board[1, x] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.Pawn);
            _board[6, x] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.Pawn);
        }

        // Rooks
        _board[0, 0] = _board[0, 7] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.Rook);
        _board[7, 0] = _board[7, 7] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.Rook);

        // Knights
        _board[0, 1] = _board[0, 6] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.Knight);
        _board[7, 1] = _board[7, 6] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.Knight);

        // Bishops
        _board[0, 2] = _board[0, 5] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.Bishop);
        _board[7, 2] = _board[7, 5] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.Bishop);

        // Queens
        _board[0, 3] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.Queen);
        _board[7, 3] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.Queen);

        // Kings
        _board[0, 4] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.King);
        _board[7, 4] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.King);

    }


    public ChessPiece this[int row, int col] => _board[row, col];

    public bool IsInside(int row, int col) => row >= 0 && row < 8 && col >= 0 && col < 8;

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
        if (!IsInside(chessAction.From.Row, chessAction.From.Col) || !IsInside(chessAction.To.Row, chessAction.To.Col))
            throw new ArgumentException("Invalid move positions.");

        ChessPiece piece = _board[chessAction.From.Row, chessAction.From.Col];
        if (piece.IsEmpty)
            throw new InvalidOperationException("No piece at the source position.");

        ChessState newBoard = new();
        Array.Copy(_board, newBoard._board, _board.Length);

        // Move the piece
        newBoard._board[chessAction.To.Row, chessAction.To.Col] = piece;
        newBoard._board[chessAction.From.Row, chessAction.From.Col] = ChessPiece.Empty;

        if (UpsTurns)
        {
            newBoard.TurnCount = this.TurnCount + 1;
        }

        return newBoard;
    }

    public ChessPieceAttribute TurnColor => (TurnCount % 2 == 0) ? ChessPieceAttribute.White : ChessPieceAttribute.Black;

    //refactor below as ChessActionCandidate methods
    public IEnumerable<ChessActionCandidate> GetActionCandidates() =>
        ChessHistoryUtility.GetActionCandidates(_board, TurnColor);

    public IEnumerable<ChessActionCandidate> GetAttackingActionCandidates(ChessPieceAttribute attackingColor) =>
        ChessHistoryUtility.GetActionCandidates(_board, attackingColor)
            .Where(candidate => candidate.Pattern.Captures != CaptureBehavior.MoveOnly);

    public IEnumerable<ChessActionCandidate> GetThreateningActionCandidates(ChessPieceAttribute threateningColor) =>
        GetAttackingActionCandidates(threateningColor)
            .Where(candidate =>
            {
                var threatenedPiece = this[candidate.Action.To.Row, candidate.Action.To.Col];
                return !threatenedPiece.IsEmpty && !threatenedPiece.IsSameColor(threateningColor);
            });

    public IEnumerable<ChessActionCandidate> GetCheckingActionCandidates(ChessPieceAttribute checkingColor) =>
        GetThreateningActionCandidates(checkingColor)
            .Where(candidate =>
            {
                var threatenedPiece = this[candidate.Action.To.Row, candidate.Action.To.Col];
                return (threatenedPiece.TypeFlag & ChessPieceAttribute.King) != 0;
            });



    public IEnumerable<(int X, int Y)> GetBlockedPositions(ChessPieceAttribute blockedColor)
    {
        // start by getting all available actions for the current turn color and record their "from" position
        // then find all cells that are occupied by pieces of the same color
        // then find the cells that are occupied by pieces of the opposite color and are not in the "from" position of any available action
        // those are the blocked cells
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = this[row, col];
                if (piece.IsEmpty) continue;
                if (!piece.IsSameColor(blockedColor)) continue;
                var actionsFromCell = GetActionCandidates()
                    .Where(c => c.Action.From.Row == row && c.Action.From.Col == col)
                    .ToList();
                if (actionsFromCell.Count != 0) continue;
                yield return (row, col);
            }
        }
    }

//because we are using simulatedBoard.TurnColor, we should use the TurnColor rather than an argument. same for all such methods
    public IEnumerable<(int X, int Y)> GetPinnedCells(ChessPieceAttribute pinnedColor)
    {
        // start by getting the king position for the pinned color
        // then get all available actions for the opposite color
        // then, for each action, simulate the move and see if it results in a check on the king
        // flag each action with this information
        // then, for each cell occupied by a piece of the pinned color, flag the cell as pinned if all actions from that cell result in a check on the king
        List<(int X, int Y)> pinnedCells = new List<(int X, int Y)>();
        IReadOnlyCollection<(int X, int Y)> kingPositions = GetPiecesByType(pinnedColor, ChessPieceAttribute.King);
        if (kingPositions.Count != 1) return pinnedCells;
        (int X, int Y) kingPosition = kingPositions.First();
        ChessPiece kingPiece = this[kingPosition.X, kingPosition.Y];
        if (kingPiece.IsEmpty) throw new InvalidOperationException("King piece not found at expected position.");
        if (!kingPiece.IsSameColor(pinnedColor)) throw new InvalidOperationException("King piece color does not match pinned color.");

        List<ChessActionCandidate> opposingActionCandidates = [.. GetActionCandidates()
            .Where(c => !this[c.Action.From.Row, c.Action.From.Col].IsSameColor(pinnedColor))];

        //for each opposing action candidate, simulate the move by applying it to a cloned board and check if the king is checked
        foreach (ChessActionCandidate checkingActionCandidate in opposingActionCandidates)
        {
            ChessState simulatedBoard = Apply(checkingActionCandidate.Action);
            IEnumerable<ChessActionCandidate> checkingCandidates = simulatedBoard.GetCheckingActionCandidates(simulatedBoard.TurnColor);
            if (checkingCandidates.Any()) continue;
            (int Row, int Col) pinnedCell = (checkingActionCandidate.Action.From.Row, checkingActionCandidate.Action.From.Col);
            if (pinnedCells.Contains(pinnedCell)) continue;
            pinnedCells.Add(pinnedCell);
        }


        return pinnedCells;
    }



    public IReadOnlyCollection<(int X, int Y)> GetPiecesByType(ChessPieceAttribute pieceColor, ChessPieceAttribute pieceType)
    {
        var positions = new List<(int X, int Y)>();
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = this[row, col];
                if (piece.IsEmpty) continue;
                if (!piece.IsSameColor(pieceColor)) continue;
                if (!piece.IncludesType(pieceType)) continue;
                positions.Add((row, col));
            }
        }
        return positions;
    }

    public class PieceAction
    {
        public ChessAction ChessAction { get; }
        public ChessPiece Piece { get; }
        public PieceAction(ChessAction chessAction, ChessPiece piece)
        {
            ChessAction = chessAction;
            Piece = piece;
        }
    }
}

