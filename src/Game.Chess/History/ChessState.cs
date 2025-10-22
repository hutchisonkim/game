//src\Game.Chess\ChessPolicy.cs

using Game.Core;
using Game.Chess.Entity;
using Game.Core.Wip;

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

    // Current turn represented as a color flag
    public ChessPieceAttribute CurrentTurnColorFlag => (TurnCount % 2 == 0) ? ChessPieceAttribute.White : ChessPieceAttribute.Black;

    public IEnumerable<ChessAction> GetAvailableActions()
    {
        var currentColorFlag = CurrentTurnColorFlag;
        return GetAvailableActionsDetailed().PieceMoves
            .Where(m => (m.Piece.ColorFlag & currentColorFlag) != 0)
            .Select(m => m.ChessAction);
    }

    public record AvailableActionsResult(
        IEnumerable<PieceAction> PieceMoves
    );

    public ChessState GetNextState(PieceAction pieceAction) => Apply(pieceAction.ChessAction);
    public AvailableActionsResult GetNextAvailableActionsDetailed(PieceAction pieceAction) => GetNextState(pieceAction).GetAvailableActionsDetailed();

    public AvailableActionsResult GetAvailableActionsDetailed()
    {

        var pieceActions = new List<PieceAction>();

        // Build a lightweight Board using the new core types and expand piece actions
        var board = new Game.Core.Wip.Board(8, 8);

        // Map ChessPiece -> core Piece with policy and faction
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var cp = this[row, col];
                if (cp.IsEmpty) continue;

                var faction = cp.IsWhite ? Game.Core.Wip.Faction.White : Game.Core.Wip.Faction.Black;
                var piecePolicy = new Game.Core.Wip.PiecePolicy(ChessHistoryUtility.GetPatternDtosFor(cp));
                var corePiece = new Game.Core.Wip.Piece(piecePolicy, faction, row, col);
                board.AddPiece(corePiece);
            }
        }

        // Expand board-level actions and apply capture filtering similar to previous logic
        var candidatesAll = board.GetActions(CurrentTurnColorFlag == ChessPieceAttribute.White ? Game.Core.Wip.Faction.White : Game.Core.Wip.Faction.Black);

        foreach (var cand in candidatesAll)
        {
            var fromPiece = board.PieceAt(cand.FromRow, cand.FromCol);
            if (fromPiece == null) continue;

            // Translate board coordinates to chess representation and check capture rules
            var target = this[cand.ToRow, cand.ToCol];
            var captures = cand.Pattern.Captures;

            bool accept = false;
            if (target.IsEmpty)
            {
                if (captures == CaptureBehavior.MoveOnly || captures == CaptureBehavior.MoveOrCapture)
                    accept = true;
            }
            else
            {
                if (target.IsWhite != (fromPiece.Faction == Game.Core.Wip.Faction.White))
                {
                    if (captures == CaptureBehavior.CaptureOnly || captures == CaptureBehavior.MoveOrCapture)
                        accept = true;
                }
            }

            if (!accept) continue;

            var chessAction = new ChessAction(new ChessPosition(cand.FromRow, cand.FromCol), new ChessPosition(cand.ToRow, cand.ToCol));
            // Retrieve original chess piece from state for the result payload
            var originalPiece = this[cand.FromRow, cand.FromCol];
            pieceActions.Add(new PieceAction(chessAction, originalPiece));
        }

        return new AvailableActionsResult(pieceActions);
    }

    public class Cell
    {
        public int Row { get; }
        public int Col { get; }
        public ChessPiece OccupyingPiece => _state.Board[Row, Col];
        private ChessState _state;
        public Cell(int row, int col, ChessState state)
        {
            Row = row;
            Col = col;
            _state = state;
        }
    }

    public class AttackedCell : Cell
    {
        public IEnumerable<PieceAction> AttackingPieceActions { get; }

        public AttackedCell(int row, int col, ChessState state, IEnumerable<PieceAction> attackingMoves)
            : base(row, col, state)
        {
            AttackingPieceActions = attackingMoves;
        }
    }

    public IEnumerable<AttackedCell> GetAttackedCells(ChessPieceAttribute attackedColorFlag)
    {
        var cellsDict = new Dictionary<(int, int), List<PieceAction>>();
        var availableActions = GetAvailableActionsDetailed().PieceMoves;

        foreach (var move in availableActions)
        {
            if ((move.Piece.ColorFlag & attackedColorFlag) == 0) continue; // keep only attacking moves

            var toPos = move.ChessAction.To;
            var attackedPosition = (toPos.Row, toPos.Col);

            if (!cellsDict.TryGetValue(attackedPosition, out List<PieceAction>? attackingMoves))
            {
                attackingMoves = new List<PieceAction>();
                cellsDict[attackedPosition] = attackingMoves;
            }

            attackingMoves.Add(move);
        }

        return cellsDict.Select(kvp => new AttackedCell(
            row: kvp.Key.Item1,
            col: kvp.Key.Item2,
            state: this,
            attackingMoves: kvp.Value
        ));
    }


    public IEnumerable<Cell> GetThreatenedCells(ChessPieceAttribute threatenedColorFlag) => GetAttackedCells(threatenedColorFlag)
        .Where(c => !c.OccupyingPiece.IsEmpty && ((c.OccupyingPiece.ColorFlag & threatenedColorFlag) != 0));

    public IEnumerable<Cell> GetCheckedCells(ChessPieceAttribute checkedColorFlag)
    {
        var kingPositions = GetPiecesByType(checkedColorFlag, ChessPieceAttribute.King);
        if (kingPositions.Count != 1) return Enumerable.Empty<Cell>();

        var kingPos = kingPositions.First();
        var attackedCells = GetAttackedCells(checkedColorFlag);

        return attackedCells.Where(c => c.Row == kingPos.Row && c.Col == kingPos.Col);
    }

    public IEnumerable<Cell> GetEmptyCells()
    {
        var emptyCells = new List<Cell>();
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                if (this[row, col].IsEmpty)
                {
                    emptyCells.Add(new Cell(row, col, this));
                }
            }
        }
        return emptyCells;
    }

    public IEnumerable<Cell> GetOccupiedCells()
    {
        var occupiedCells = new List<Cell>();
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = this[row, col];
                if (!piece.IsEmpty)
                {
                    occupiedCells.Add(new Cell(row, col, this));
                }
            }
        }
        return occupiedCells;
    }

    public IEnumerable<Cell> GetBlockedCells(ChessPieceAttribute currentTurnColorFlag)
    {
        // start by getting all available actions for the current turn color and record their "from" position
        // then find all cells that are occupied by pieces of the same color
        // then find the cells that are occupied by pieces of the opposite color and are not in the "from" position of any available action
        // those are the blocked cells
        var availableActions = GetAvailableActionsDetailed().PieceMoves
            .Where(m => (m.Piece.ColorFlag & currentTurnColorFlag) != 0)
            .Select(m => (m.ChessAction.From.Row, m.ChessAction.From.Col))
            .ToHashSet();
        var blockedCells = new List<Cell>();
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = _board[row, col];
                if (!piece.IsEmpty && ((piece.ColorFlag & currentTurnColorFlag) != 0))
                {
                    //print
                    if (!availableActions.Contains((row, col)))
                    {
                        blockedCells.Add(new Cell(row, col, this));
                    }
                }
            }
        }
        return blockedCells;
    }

    public IEnumerable<Cell> GetPinnedCells(ChessPieceAttribute pinnedColor)
    {
        // start by getting the king position for the pinned color
        // then get all available actions for the opposite color
        // then, for each action, simulate the move and see if it results in a check on the king
        // flag each action with this information
        // then, for each cell occupied by a piece of the pinned color, flag the cell as pinned if all actions from that cell result in a check on the king
        var kingPositions = GetPiecesByType(pinnedColor, ChessPieceAttribute.King);
        if (kingPositions.Count != 1) return Enumerable.Empty<Cell>();
        var kingPos = kingPositions.First();
        var oppositeColor = (pinnedColor & ChessPieceAttribute.White) != 0 ? ChessPieceAttribute.Black : ChessPieceAttribute.White;
        var oppositeActions = GetAvailableActionsDetailed().PieceMoves
            .Where(m => (m.Piece.ColorFlag & oppositeColor) != 0)
            .ToList();
        var pinnedCells = new List<Cell>();
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = this[row, col];
                if (!piece.IsEmpty && ((piece.ColorFlag & pinnedColor) != 0))
                {
                    var actionsFromCell = oppositeActions
                        .Where(m => m.ChessAction.From.Row == row && m.ChessAction.From.Col == col)
                        .ToList();
                    if (actionsFromCell.Count > 0 && actionsFromCell.All(a => GetNextState(a).GetCheckedCells(pinnedColor).Any()))
                    {
                        pinnedCells.Add(new Cell(row, col, this));
                    }
                }
            }
        }
        return pinnedCells;
    }



    public IReadOnlyCollection<ChessPosition> GetPiecesByType(ChessPieceAttribute pieceColorFlag, ChessPieceAttribute pieceTypeFlag)
    {
        var positions = new List<ChessPosition>();
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = this[row, col];
                if (!piece.IsEmpty && ((piece.ColorFlag & pieceColorFlag) != 0) && ((piece.TypeFlag & pieceTypeFlag) != 0))
                {
                    positions.Add(new ChessPosition(row, col));
                }
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

