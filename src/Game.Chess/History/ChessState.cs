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

    public void InitializeBoard(ChessPieceAttribute pieceAttributeOverride = ChessPieceAttribute.None)
    {
        TurnCount = 0;
        bool hasOverride = pieceAttributeOverride != ChessPieceAttribute.None;
        ChessPieceAttribute pieceAttr;


        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Pawn;
        for (int x = 0; x < 8; x++)
        {
            _board[x, 1] = new ChessPiece(ChessPieceAttribute.White | pieceAttr);
            _board[x, 6] = new ChessPiece(ChessPieceAttribute.Black | pieceAttr);
        }

        // Rooks
        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Rook;
        _board[0, 0] = _board[7, 0] = new ChessPiece(ChessPieceAttribute.White | pieceAttr);
        _board[0, 7] = _board[7, 7] = new ChessPiece(ChessPieceAttribute.Black | pieceAttr);

        // Knights
        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Knight;
        _board[1, 0] = _board[6, 0] = new ChessPiece(ChessPieceAttribute.White | pieceAttr);
        _board[1, 7] = _board[6, 7] = new ChessPiece(ChessPieceAttribute.Black | pieceAttr);

        // Bishops
        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Bishop;
        _board[2, 0] = _board[5, 0] = new ChessPiece(ChessPieceAttribute.White | pieceAttr);
        _board[2, 7] = _board[5, 7] = new ChessPiece(ChessPieceAttribute.Black | pieceAttr);

        // Queens
        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Queen;
        _board[3, 0] = new ChessPiece(ChessPieceAttribute.White | pieceAttr);
        _board[3, 7] = new ChessPiece(ChessPieceAttribute.Black | pieceAttr);

        // Kings
        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.King;
        _board[4, 0] = new ChessPiece(ChessPieceAttribute.White | pieceAttr);
        _board[4, 7] = new ChessPiece(ChessPieceAttribute.Black | pieceAttr);

        TurnCount = 0;
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

    //refactor below as ChessActionCandidate methods
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



    public IEnumerable<(int X, int Y)> GetBlockedPositions(ChessPieceAttribute blockedColor)
    {
        // start by getting all available actions for the current turn color and record their "from" position
        // then find all cells that are occupied by pieces of the same color
        // then find the cells that are occupied by pieces of the opposite color and are not in the "from" position of any available action
        // those are the blocked cells
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                var piece = this[x, y];
                if (piece.IsEmpty) continue;
                if (!piece.IsSameColor(blockedColor)) continue;
                var actionsFromCell = GetActionCandidates()
                    .Where(c => c.Action.From.X == x && c.Action.From.Y == y)
                    .ToList();
                if (actionsFromCell.Count != 0) continue;
                yield return (x, y);
            }
        }
    }

//because we are using simulatedBoard.TurnColor, we should use the TurnColor rather than an argument. same for all such methods
    public IEnumerable<(int X, int Y)> GetPinnedCells(ChessPieceAttribute pinnedColor, bool includeTargetless, bool includeFriendlyfire)
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
            .Where(c => !this[c.Action.From.X, c.Action.From.Y].IsSameColor(pinnedColor))];

        //for each opposing action candidate, simulate the move by applying it to a cloned board and check if the king is checked
        foreach (ChessActionCandidate checkingActionCandidate in opposingActionCandidates)
        {
            ChessState simulatedBoard = Apply(checkingActionCandidate.Action);
            IEnumerable<ChessActionCandidate> checkingCandidates = simulatedBoard.GetCheckingActionCandidates(simulatedBoard.TurnColor, includeTargetless, includeFriendlyfire);
            if (checkingCandidates.Any()) continue;
            (int Row, int Col) pinnedCell = (checkingActionCandidate.Action.From.X, checkingActionCandidate.Action.From.Y);
            if (pinnedCells.Contains(pinnedCell)) continue;
            pinnedCells.Add(pinnedCell);
        }


        return pinnedCells;
    }



    public IReadOnlyCollection<(int X, int Y)> GetPiecesByType(ChessPieceAttribute pieceColor, ChessPieceAttribute pieceType)
    {
        var positions = new List<(int X, int Y)>();
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                var piece = this[x, y];
                if (piece.IsEmpty) continue;
                if (!piece.IsSameColor(pieceColor)) continue;
                if (!piece.IncludesType(pieceType)) continue;
                positions.Add((x, y));
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

