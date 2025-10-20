//src\Game.Chess\ChessPolicy.cs

using System.Collections.ObjectModel;
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
    public AvailableActionsResult GetNextAvailableActionsDetailed(PieceAction pieceAction, bool forceIncludeCaptures = false, bool forceExcludeMoves = false) => GetNextState(pieceAction).GetAvailableActionsDetailed(forceIncludeCaptures, forceExcludeMoves);

    public AvailableActionsResult GetAvailableActionsDetailed(bool forceIncludeCaptures = false, bool forceExcludeMoves = false)
    {

        // PIECE LEVEL -> DTO list for Spark: each piece produces raw candidates from its patterns
        var dtoList = new List<ChessActionDto>();
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = this[row, col];
                if (piece == null) continue;

                var moves = PieceBehavior.GetAvailableActions(piece, this, row, col, forceIncludeCaptures, forceExcludeMoves);
                foreach (var (toRow, toCol, pattern) in moves)
                {
                    var dest = this[toRow, toCol];
                    int destColor = dest == null ? -1 : (int)dest.ColorFlag;
                    dtoList.Add(new ChessActionDto(
                        FromRow: row,
                        FromCol: col,
                        ToRow: toRow,
                        ToCol: toCol,
                        PieceColorFlag: (int)piece.ColorFlag,
                        PieceTypeFlag: (int)piece.TypeFlag,
                        PatternCaptures: (int)pattern.Captures,
                        DestColorFlag: destColor
                    ));
                }
            }
        }

    // Prepare a spark session (lightweight GetOrCreate)
    var spark = Microsoft.Spark.Sql.SparkSession.Builder().GetOrCreate();
    var schema = ChessActionDtoExtensions.Schema();
    var rows = dtoList.ToRows();
    var query = new Game.Core.ActionQuerySpark(spark, schema, rows);

        // expression: keep only in-bounds (redundant but explicit) and enforce capture/move semantics
        var sql = @"FromRow >= 0 AND FromRow < 8 AND FromCol >= 0 AND FromCol < 8 AND ToRow >= 0 AND ToRow < 8 AND ToCol >= 0 AND ToCol < 8 AND ( (DestColorFlag = -1 AND (PatternCaptures = 0 OR PatternCaptures = 2)) OR (DestColorFlag != -1 AND DestColorFlag != PieceColorFlag AND (PatternCaptures = 1 OR PatternCaptures = 2)) )";
        var rowsOut = query.WhereSql(sql).CollectRows();

        // convert Row[] back into ChessActionDto
        var filteredDtos = rowsOut.Select(r => new ChessActionDto(
            FromRow: r.GetAs<int>("FromRow"),
            FromCol: r.GetAs<int>("FromCol"),
            ToRow: r.GetAs<int>("ToRow"),
            ToCol: r.GetAs<int>("ToCol"),
            PieceColorFlag: r.GetAs<int>("PieceColorFlag"),
            PieceTypeFlag: r.GetAs<int>("PieceTypeFlag"),
            PatternCaptures: r.GetAs<int>("PatternCaptures"),
            DestColorFlag: r.GetAs<int>("DestColorFlag")
        )).ToList();

        // GAME LEVEL: filter by current turn color and convert back to PieceAction
        var currentColorFlag = (int)CurrentTurnColorFlag;
        var pieceActions = filteredDtos
            .Where(d => (d.PieceColorFlag & currentColorFlag) != 0)
            .Select(d => new PieceAction(new ChessAction(new Position(d.FromRow, d.FromCol), new Position(d.ToRow, d.ToCol)), this[d.FromRow, d.FromCol]!))
            .ToList();

        // POST-PROCESS: merge castling-like moves (simple heuristic: pair rook+king adjacent moves into single action)
        var merged = MergeCastleLikeMoves(pieceActions);

        return new AvailableActionsResult(merged);
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
        var availableActions = GetAvailableActionsDetailed(forceIncludeCaptures: true, forceExcludeMoves: true).PieceMoves;

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

    // Very small heuristic to merge a king move and rook move into a single castling-like action.
    // Real chess castling has many preconditions; this implementation simply looks for two actions
    // of the same color where one is a king move of two squares and another is a rook move that
    // moves into the king's intermediate square; they are merged into a single PieceAction using
    // the king's PieceAction as representative (payload Piece left as king).
    private static List<PieceAction> MergeCastleLikeMoves(List<PieceAction> pieceActions)
    {
        var result = new List<PieceAction>(pieceActions);

        // Find candidate king moves of length 2 horizontally
        var kings = pieceActions.Where(p => (p.Piece.TypeFlag & PieceAttribute.King) != 0 && Math.Abs(p.ChessAction.To.Col - p.ChessAction.From.Col) == 2).ToList();
        foreach (var kingAction in kings)
        {
            // find a rook action belonging to same color that moves into the square the king passes over
            int dir = Math.Sign(kingAction.ChessAction.To.Col - kingAction.ChessAction.From.Col);
            var kingIntermediateCol = kingAction.ChessAction.From.Col + dir;
            var rookMatch = pieceActions.FirstOrDefault(p => (p.Piece.TypeFlag & PieceAttribute.Rook) != 0
                                                            && ((p.Piece.ColorFlag & kingAction.Piece.ColorFlag) != 0)
                                                            && p.ChessAction.To.Row == kingAction.ChessAction.From.Row
                                                            && p.ChessAction.To.Col == kingIntermediateCol);
            if (rookMatch != null)
            {
                // remove individual actions and add merged (use kingAction as representative)
                result.Remove(kingAction);
                result.Remove(rookMatch);
                result.Add(kingAction);
            }
        }

        return result;
    }
}

