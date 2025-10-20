//src\Game.Chess\ChessPolicy.cs

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Game.Core;
using Game.Chess;

namespace Game.Chess.Policy;



// 🔹 Chess Board
public class ChessState : IState<ChessAction, ChessState>
{
    private readonly Piece?[,] _board = new Piece?[8, 8];

    public Piece?[,] Board => _board;
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
            _board[1, x] = new Piece(PieceAttribute.White | PieceAttribute.Pawn);
            _board[6, x] = new Piece(PieceAttribute.Black | PieceAttribute.Pawn);
        }

        // Rooks
        _board[0, 0] = _board[0, 7] = new Piece(PieceAttribute.White | PieceAttribute.Rook);
        _board[7, 0] = _board[7, 7] = new Piece(PieceAttribute.Black | PieceAttribute.Rook);

        // Knights
        _board[0, 1] = _board[0, 6] = new Piece(PieceAttribute.White | PieceAttribute.Knight);
        _board[7, 1] = _board[7, 6] = new Piece(PieceAttribute.Black | PieceAttribute.Knight);

        // Bishops
        _board[0, 2] = _board[0, 5] = new Piece(PieceAttribute.White | PieceAttribute.Bishop);
        _board[7, 2] = _board[7, 5] = new Piece(PieceAttribute.Black | PieceAttribute.Bishop);

        // Queens
        _board[0, 3] = new Piece(PieceAttribute.White | PieceAttribute.Queen);
        _board[7, 3] = new Piece(PieceAttribute.Black | PieceAttribute.Queen);

        // Kings
        _board[0, 4] = new Piece(PieceAttribute.White | PieceAttribute.King);
        _board[7, 4] = new Piece(PieceAttribute.Black | PieceAttribute.King);

    }


    public Piece? this[int row, int col] => _board[row, col];

    public bool IsInside(int row, int col) => row >= 0 && row < 8 && col >= 0 && col < 8;

    public ChessState Clone()
    {
        var copy = new Piece?[8, 8];
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

        Piece? piece = _board[chessAction.From.Row, chessAction.From.Col];
        if (piece == null)
            throw new InvalidOperationException("No piece at the source position.");

        ChessState newBoard = new ChessState();
        Array.Copy(_board, newBoard._board, _board.Length);

        // Move the piece
        newBoard._board[chessAction.To.Row, chessAction.To.Col] = piece;
        newBoard._board[chessAction.From.Row, chessAction.From.Col] = null;

        if (UpsTurns)
        {
            newBoard.TurnCount = this.TurnCount + 1;
        }

        return newBoard;
    }

    // Current turn represented as a color flag
    public PieceAttribute CurrentTurnColorFlag => (TurnCount % 2 == 0) ? PieceAttribute.White : PieceAttribute.Black;

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

        // Build an 8x8 grid of CellActors
        var cells = new CellActor[8, 8];
        var cellList = new List<CellActor>();
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var cell = new CellActor(r, c);
                cells[r, c] = cell;
                cellList.Add(cell);
            }
        }

        // Create PieceActors and attach them to their respective CellActors
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = this[row, col];
                if (piece == null) continue;

                var factionPolicy = new DelegateFactionPolicy(() => PieceBehavior.ForwardAxis(piece));
                var factionActor = new FactionActor(factionPolicy);

                var piecePolicy = new DelegatePiecePolicy(() => PieceBehavior.GetPatternDtosFor(piece));
                var pieceActor = new PieceActor(piecePolicy, factionActor);

                cells[row, col].Piece = pieceActor;
            }
        }

        // After collecting all CellActors for the board, expand them via a BoardActor
        var boardActor = new BoardActor(cellList, isInside: (r, c) => IsInside(r, c), getPieceAt: (r, c) => this[r, c]);

        // Do not apply game-level filtering here - return all board candidates.
        // The higher-level `GetAvailableActions` wrapper applies turn-based filtering when needed.
        var candidatesAll = boardActor.GetCandidates();

        foreach (var cand in candidatesAll)
        {
            // identify the originating piece for this candidate
            var fromPiece = this[cand.FromRow, cand.FromCol];
            if (fromPiece == null) continue; // defensive

            var target = this[cand.ToRow, cand.ToCol];
            var captures = cand.Pattern.Captures;

            bool accept = false;
            if (target == null)
            {
                if (captures == Game.Core.CaptureBehavior.MoveOnly || captures == Game.Core.CaptureBehavior.MoveOrCapture)
                    accept = true;
            }
            else
            {
                // target occupied; allow if owner differs
                if (target.IsWhite != fromPiece.IsWhite)
                {
                    if (captures == Game.Core.CaptureBehavior.CaptureOnly || captures == Game.Core.CaptureBehavior.MoveOrCapture)
                        accept = true;
                }
            }

            if (!accept) continue;

            var chessAction = new ChessAction(new Position(cand.FromRow, cand.FromCol), new Position(cand.ToRow, cand.ToCol));
            pieceActions.Add(new PieceAction(chessAction, fromPiece));
        }

        return new AvailableActionsResult(pieceActions);
    }

    public class Cell
    {
        public int Row { get; }
        public int Col { get; }
        public Piece? OccupyingPiece => _state.Board[Row, Col];
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

    public IEnumerable<AttackedCell> GetAttackedCells(PieceAttribute attackedColorFlag)
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


    public IEnumerable<Cell> GetThreatenedCells(PieceAttribute threatenedColorFlag) => GetAttackedCells(threatenedColorFlag)
        .Where(c => c.OccupyingPiece != null && ((c.OccupyingPiece.ColorFlag & threatenedColorFlag) != 0));

    public IEnumerable<Cell> GetCheckedCells(PieceAttribute checkedColorFlag)
    {
        var kingPositions = GetPiecesByType(checkedColorFlag, PieceAttribute.King);
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
                if (this[row, col] == null)
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
                if (piece != null)
                {
                    occupiedCells.Add(new Cell(row, col, this));
                }
            }
        }
        return occupiedCells;
    }

    public IEnumerable<Cell> GetBlockedCells(PieceAttribute currentTurnColorFlag)
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
                if (piece != null && ((piece.ColorFlag & currentTurnColorFlag) != 0))
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

    public IEnumerable<Cell> GetPinnedCells(PieceAttribute pinnedColor)
    {
        // start by getting the king position for the pinned color
        // then get all available actions for the opposite color
        // then, for each action, simulate the move and see if it results in a check on the king
        // flag each action with this information
        // then, for each cell occupied by a piece of the pinned color, flag the cell as pinned if all actions from that cell result in a check on the king
        var kingPositions = GetPiecesByType(pinnedColor, PieceAttribute.King);
        if (kingPositions.Count != 1) return Enumerable.Empty<Cell>();
        var kingPos = kingPositions.First();
        var oppositeColor = (pinnedColor & PieceAttribute.White) != 0 ? PieceAttribute.Black : PieceAttribute.White;
        var oppositeActions = GetAvailableActionsDetailed().PieceMoves
            .Where(m => (m.Piece.ColorFlag & oppositeColor) != 0)
            .ToList();
        var pinnedCells = new List<Cell>();
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = this[row, col];
                if (piece != null && ((piece.ColorFlag & pinnedColor) != 0))
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



    public IReadOnlyCollection<Position> GetPiecesByType(PieceAttribute pieceColorFlag, PieceAttribute pieceTypeFlag)
    {
        var positions = new List<Position>();
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = this[row, col];
                if (piece != null && ((piece.ColorFlag & pieceColorFlag) != 0) && ((piece.TypeFlag & pieceTypeFlag) != 0))
                {
                    positions.Add(new Position(row, col));
                }
            }
        }
        return positions;
    }

    public class PieceAction
    {
        public ChessAction ChessAction { get; }
        public Piece Piece { get; }
        public PieceAction(ChessAction chessAction, Piece piece)
        {
            ChessAction = chessAction;
            Piece = piece;
        }
    }
}

