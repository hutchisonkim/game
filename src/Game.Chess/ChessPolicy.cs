//src\Game.Chess\ChessPolicy.cs

using System.Collections.ObjectModel;
using Game.Core;

namespace Game.Chess.Policy;

// 🔹 Lightweight value object for move vectors
public readonly struct Vector2
{
    public int X { get; }
    public int Y { get; }

    public Vector2(int x, int y) => (X, Y) = (x, y);

    public static readonly Vector2 OneByZero = new(1, 0);
    public static readonly Vector2 ZeroByOne = new(0, 1);
    public static readonly Vector2 OneByOne = new(1, 1);
    public static readonly Vector2 OneByTwo = new(1, 2);
}

// 🔹 Move rules
public class Pattern
{
    [Flags]
    public enum MirrorBehavior { None = 0, Horizontal = 1, Vertical = 2, All = Horizontal | Vertical }
    public enum RepeatBehavior { NotRepeatable, Repeatable, RepeatableOnce }
    public enum CaptureBehavior { MoveOnly, CaptureOnly, MoveOrCapture, CastleOnly }

    public Vector2 Vector { get; }
    public MirrorBehavior Mirrors { get; }
    public RepeatBehavior Repeats { get; }
    public CaptureBehavior Captures { get; }
    public bool ForwardOnly { get; }
    public bool Jumps { get; }

    public Pattern(PatternOptions options)
    {
        Vector = options.Vector;
        Mirrors = options.Mirrors;
        Repeats = options.Repeats;
        Captures = options.Captures;
        ForwardOnly = options.ForwardOnly;
        Jumps = options.Jumps;
    }

    public record PatternOptions(
        Vector2 Vector,
        MirrorBehavior Mirrors = MirrorBehavior.All,
        RepeatBehavior Repeats = RepeatBehavior.Repeatable,
        CaptureBehavior Captures = CaptureBehavior.MoveOrCapture,
        bool ForwardOnly = false,
        bool Jumps = false
    );
}

// 🔹 Piece metadata
public class PiecePolicy
{
    public PieceType Type { get; }
    public IReadOnlyList<PiecePolicy> TransformsInto { get; }

    public PiecePolicy(PieceType type, IEnumerable<PiecePolicy>? transformsInto = null)
    {
        Type = type;
        TransformsInto = new ReadOnlyCollection<PiecePolicy>(
            transformsInto != null ? new List<PiecePolicy>(transformsInto) : new List<PiecePolicy>()
        );
    }
}


// 🔹 Base piece class
public abstract class Piece
{
    // public static char ToFenCharNew(PieceType type, PieceColor color)
    // {
    //     char fenChar;

    //     if ((type & PieceType.MovedPawn) == PieceType.MovedPawn)
    //         fenChar = 'X';
    //     else if ((type & PieceType.MovedRook) == PieceType.MovedRook)
    //         fenChar = 'Y';
    //     else if ((type & PieceType.MovedKing) == PieceType.MovedKing)
    //         fenChar = 'Z';
    //     else if ((type & PieceType.Pawn) == PieceType.Pawn)
    //         fenChar = 'P';
    //     else if ((type & PieceType.Rook) == PieceType.Rook)
    //         fenChar = 'R';
    //     else if ((type & PieceType.King) == PieceType.King)
    //         fenChar = 'K';
    //     else if ((type & PieceType.Queen) == PieceType.Queen)
    //         fenChar = 'Q';
    //     else if ((type & PieceType.Bishop) == PieceType.Bishop)
    //         fenChar = 'B';
    //     else if ((type & PieceType.Knight) == PieceType.Knight)
    //         fenChar = 'N';
    //     else
    //         throw new ArgumentOutOfRangeException(nameof(type));

    //     return color == PieceColor.White ? fenChar : char.ToLower(fenChar);
    // }
    public static char ToFenChar(PieceType type, PieceColor color)
    {
        char fenChar;

        if ((type & PieceType.Pawn) == PieceType.Pawn)
            fenChar = 'P';
        else if ((type & PieceType.Rook) == PieceType.Rook)
            fenChar = 'R';
        else if ((type & PieceType.King) == PieceType.King)
            fenChar = 'K';
        else if ((type & PieceType.Queen) == PieceType.Queen)
            fenChar = 'Q';
        else if ((type & PieceType.Bishop) == PieceType.Bishop)
            fenChar = 'B';
        else if ((type & PieceType.Knight) == PieceType.Knight)
            fenChar = 'N';
        else
            throw new ArgumentOutOfRangeException(nameof(type));

        return color == PieceColor.White ? fenChar : char.ToLower(fenChar);
    }

    public PieceColor Color { get; }
    public abstract PieceType Type { get; }
    public string PieceTypeDescription => $"{ToFenChar(Type, Color)}";
    public abstract PiecePolicy Policy { get; }
    public int ForwardAxis => Color == PieceColor.White ? 1 : -1;

    protected Piece(PieceColor color) => Color = color;

    // Returns all abstract moves for the piece
    public abstract IEnumerable<Pattern> GetPatterns();

    // Returns all legal moves given the board and current position
    public IEnumerable<(int row, int col, Pattern move)> GetAvailableActions(ChessState board, int row, int col, bool forceIncludeCaptures = false, bool forceExcludeMoves = false)
    {
        foreach (var basePatterns in GetPatterns())
        {
            var directions = GetMirroredVectors(basePatterns);

            foreach (var direction in directions)
            {
                int dx = direction.X;
                int dy = direction.Y * ForwardAxis;
                int x = col;
                int y = row;

                int steps = 0;
                do
                {
                    x += dx;
                    y += dy;
                    steps++;

                    if (!board.IsInside(y, x)) break;

                    Piece? pieceTo = board[y, x];

                    if (pieceTo == null)
                    {
                        if (basePatterns.Captures == Pattern.CaptureBehavior.MoveOnly || basePatterns.Captures == Pattern.CaptureBehavior.MoveOrCapture)
                        {
                            if (!forceExcludeMoves)
                            {
                                yield return (y, x, basePatterns);
                            }
                        }
                        if (forceIncludeCaptures)
                        {
                            if (basePatterns.Captures == Pattern.CaptureBehavior.CaptureOnly || basePatterns.Captures == Pattern.CaptureBehavior.MoveOrCapture)
                            {
                                yield return (y, x, basePatterns);
                            }
                        }
                    }
                    else
                    {
                        if (pieceTo.Color != Color)
                        {
                            if (basePatterns.Captures == Pattern.CaptureBehavior.CaptureOnly || basePatterns.Captures == Pattern.CaptureBehavior.MoveOrCapture)
                            {
                                yield return (y, x, basePatterns);
                            }
                        }
                        break; // cannot jump over except knights
                    }

                    if (basePatterns.Repeats == Pattern.RepeatBehavior.NotRepeatable ||
                        (basePatterns.Repeats == Pattern.RepeatBehavior.RepeatableOnce && steps == 1) ||
                        basePatterns.Jumps)
                        break;

                } while (true);
            }
        }
    }

    private IEnumerable<Vector2> GetMirroredVectors(Pattern pattern)
    {
        yield return new Vector2(pattern.Vector.X, pattern.Vector.Y);

        if (pattern.Mirrors.HasFlag(Pattern.MirrorBehavior.Horizontal) && pattern.Vector.X != 0)
            yield return new Vector2(-pattern.Vector.X, pattern.Vector.Y);

        if (pattern.Mirrors.HasFlag(Pattern.MirrorBehavior.Vertical) && pattern.Vector.Y != 0)
            yield return new Vector2(pattern.Vector.X, -pattern.Vector.Y);

        if (pattern.Mirrors.HasFlag(Pattern.MirrorBehavior.All) && pattern.Vector.X != 0 && pattern.Vector.Y != 0)
            yield return new Vector2(-pattern.Vector.X, -pattern.Vector.Y);
    }
}

// 🔹 Concrete piece types
public class Pawn : Piece
{
    public override PieceType Type => PieceType.Pawn;
    public override PiecePolicy Policy { get; }

    public static readonly PiecePolicy Standard = new(PieceType.Pawn);
    public static readonly PiecePolicy Unmoved = new(PieceType.Pawn, new[] { Standard });

    public Pawn(PieceColor color) : base(color) => Policy = Unmoved;

    public override IEnumerable<Pattern> GetPatterns() => new[]
    {
        new Pattern(new Pattern.PatternOptions(Vector2.ZeroByOne, Mirrors: Pattern.MirrorBehavior.Horizontal, Repeats: Pattern.RepeatBehavior.NotRepeatable, Captures: Pattern.CaptureBehavior.MoveOnly, ForwardOnly: true)),
        new Pattern(new Pattern.PatternOptions(Vector2.ZeroByOne, Mirrors: Pattern.MirrorBehavior.Horizontal, Repeats: Pattern.RepeatBehavior.RepeatableOnce, Captures: Pattern.CaptureBehavior.MoveOnly, ForwardOnly: true)),
        new Pattern(new Pattern.PatternOptions(Vector2.OneByOne, Mirrors: Pattern.MirrorBehavior.Horizontal, Repeats: Pattern.RepeatBehavior.NotRepeatable, Captures: Pattern.CaptureBehavior.CaptureOnly, ForwardOnly: true))
    };
}

public class Rook : Piece
{
    public override PieceType Type => PieceType.Rook;
    public override PiecePolicy Policy { get; }

    public static readonly PiecePolicy Castled = new(PieceType.Rook);
    public static readonly PiecePolicy Standard = new(PieceType.Rook, new[] { Castled });
    public static readonly PiecePolicy Unmoved = new(PieceType.Rook, new[] { Standard });

    public Rook(PieceColor color) : base(color) => Policy = Unmoved;

    public override IEnumerable<Pattern> GetPatterns() => new[]
    {
        new Pattern(new Pattern.PatternOptions(Vector2.ZeroByOne)),
        new Pattern(new Pattern.PatternOptions(Vector2.OneByZero))
    };
}

public class Knight : Piece
{
    public override PieceType Type => PieceType.Knight;
    public override PiecePolicy Policy => Standard;

    public static readonly PiecePolicy Standard = new(PieceType.Knight);

    public Knight(PieceColor color) : base(color) { }

    public override IEnumerable<Pattern> GetPatterns() => new[]
    {
        new Pattern(new Pattern.PatternOptions(Vector2.OneByTwo, Repeats: Pattern.RepeatBehavior.NotRepeatable, Jumps: true))
    };
}

public class Bishop : Piece
{
    public override PieceType Type => PieceType.Bishop;
    public override PiecePolicy Policy => Standard;

    public static readonly PiecePolicy Standard = new(PieceType.Bishop);

    public Bishop(PieceColor color) : base(color) { }

    public override IEnumerable<Pattern> GetPatterns() => new[]
    {
        new Pattern(new Pattern.PatternOptions(Vector2.OneByOne))
    };
}

public class Queen : Piece
{
    public override PieceType Type => PieceType.Queen;
    public override PiecePolicy Policy => Standard;

    public static readonly PiecePolicy Standard = new(PieceType.Queen);

    public Queen(PieceColor color) : base(color) { }

    public override IEnumerable<Pattern> GetPatterns() => new[]
    {
        new Pattern(new Pattern.PatternOptions(Vector2.ZeroByOne)),
        new Pattern(new Pattern.PatternOptions(Vector2.OneByZero)),
        new Pattern(new Pattern.PatternOptions(Vector2.OneByOne))
    };
}

public class King : Piece
{
    public override PieceType Type => PieceType.King;
    public override PiecePolicy Policy { get; }

    public static readonly PiecePolicy Castled = new(PieceType.King);
    public static readonly PiecePolicy Standard = new(PieceType.King, new[] { Castled });
    public static readonly PiecePolicy Unmoved = new(PieceType.King, new[] { Standard });

    public King(PieceColor color) : base(color) => Policy = Unmoved;

    public override IEnumerable<Pattern> GetPatterns() => new[]
    {
        new Pattern(new Pattern.PatternOptions(Vector2.ZeroByOne, Repeats: Pattern.RepeatBehavior.NotRepeatable)),
        new Pattern(new Pattern.PatternOptions(Vector2.OneByZero, Repeats: Pattern.RepeatBehavior.NotRepeatable)),
        new Pattern(new Pattern.PatternOptions(Vector2.OneByOne, Repeats: Pattern.RepeatBehavior.NotRepeatable))
    };
}

// 🔹 Piece Factory
public static class PieceFactory
{
    public static Piece Create(PieceType type, PieceColor color) => type switch
    {
        PieceType.Pawn => new Pawn(color),
        PieceType.Rook => new Rook(color),
        PieceType.Knight => new Knight(color),
        PieceType.Bishop => new Bishop(color),
        PieceType.Queen => new Queen(color),
        PieceType.King => new King(color),
        _ => throw new InvalidOperationException("Unknown piece type")
    };
}

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
            _board[1, x] = PieceFactory.Create(PieceType.Pawn, PieceColor.White);
            _board[6, x] = PieceFactory.Create(PieceType.Pawn, PieceColor.Black);
        }

        // Rooks
        _board[0, 0] = _board[0, 7] = PieceFactory.Create(PieceType.Rook, PieceColor.White);
        _board[7, 0] = _board[7, 7] = PieceFactory.Create(PieceType.Rook, PieceColor.Black);

        // Knights
        _board[0, 1] = _board[0, 6] = PieceFactory.Create(PieceType.Knight, PieceColor.White);
        _board[7, 1] = _board[7, 6] = PieceFactory.Create(PieceType.Knight, PieceColor.Black);

        // Bishops
        _board[0, 2] = _board[0, 5] = PieceFactory.Create(PieceType.Bishop, PieceColor.White);
        _board[7, 2] = _board[7, 5] = PieceFactory.Create(PieceType.Bishop, PieceColor.Black);

        // Queens
        _board[0, 3] = PieceFactory.Create(PieceType.Queen, PieceColor.White);
        _board[7, 3] = PieceFactory.Create(PieceType.Queen, PieceColor.Black);

        // Kings
        _board[0, 4] = PieceFactory.Create(PieceType.King, PieceColor.White);
        _board[7, 4] = PieceFactory.Create(PieceType.King, PieceColor.Black);

    }

    public void PrintBoard()
    {
        for (int row = 7; row >= 0; row--)
        {
            for (int col = 7; col >= 0; col--)
            {
                var piece = _board[row, col];
                if (piece == null)
                {
                    Console.Write(". ");
                }
                else
                {
                    Console.WriteLine($"({row}, {col}): {piece.Color} {piece.Type}");
                }
            }
            Console.WriteLine();
        }
        Console.WriteLine();
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
    public PieceColor CurrentTurnColor => (TurnCount % 2 == 0) ? PieceColor.White : PieceColor.Black;

    public IEnumerable<ChessAction> GetAvailableActions()
    {
        var currentColor = CurrentTurnColor;
        return GetAvailableActionsDetailed().PieceMoves
            .Where(m => m.Piece.Color == currentColor)
            .Select(m => m.ChessAction);
    }

    public record AvailableActionsResult(
        IEnumerable<PieceAction> PieceMoves
    );

    //implement ChessGame.GetAvailableActions and ChessPlayer.GetAvailableActions
    //have each entity implement a markup for tagging moves as forbidden.
    // for example, the chess game can mark as disabled the moves of the player whose turn it is not.
    // similarly, a player can mark as disabled moves from pieces that are not theirs.
    // similarly, the board can mark as disabled moves that would place pieces outside the 8x8 grid.
    // similarly, the board can mark as disabled moves that would place a piece on a square occupied by a piece of the same color.
    // similarly, the board can mark as disabled moves that would place a piece on a square occupied by a piece of the opposite color, unless the move is a capture move.
    // the chess actions (board delta) could be perceived as either create, move, delete, or transform.
    // in chess, the environment is the game > board, while the actors are the players > factions > pieces.

    public ChessState GetNextState(PieceAction pieceAction) => Apply(pieceAction.ChessAction);
    public AvailableActionsResult GetNextAvailableActionsDetailed(PieceAction pieceAction, bool forceIncludeCaptures = false, bool forceExcludeMoves = false) => GetNextState(pieceAction).GetAvailableActionsDetailed(forceIncludeCaptures, forceExcludeMoves);

    public AvailableActionsResult GetAvailableActionsDetailed(bool forceIncludeCaptures = false, bool forceExcludeMoves = false)
    {

        var pieceActions = new List<PieceAction>();

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = this[row, col];
                if (piece == null) continue;

                IEnumerable<(int row, int col, Pattern pattern)> actionsA = piece.GetAvailableActions(this, row, col, forceIncludeCaptures, forceExcludeMoves);

                IEnumerable<ChessAction> chessActions = actionsA.Select(t => new ChessAction(new Position(row, col), new Position(t.row, t.col)));

                pieceActions.AddRange(chessActions.Select(chessAction => new PieceAction(chessAction, piece)));
            }
        }

        return new AvailableActionsResult(pieceActions);
    }

    public class BoardCell
    {
        public int Row { get; }
        public int Col { get; }
        public Piece? OccupyingPiece { get; }

        public BoardCell(int row, int col, Piece? occupyingPiece = null)
        {
            Row = row;
            Col = col;
            OccupyingPiece = occupyingPiece;
        }
    }

    public class AttackedCell : BoardCell
    {
        public IEnumerable<PieceAction> AttackingPieceActions { get; }

        public AttackedCell(int row, int col, Piece? occupyingPiece, IEnumerable<PieceAction> attackingMoves)
            : base(row, col, occupyingPiece)
        {
            AttackingPieceActions = attackingMoves;
        }
    }

    public IEnumerable<AttackedCell> GetAttackedCells(PieceColor attackedColor)
    {
        var cellsDict = new Dictionary<(int, int), List<PieceAction>>();
        var availableActions = GetAvailableActionsDetailed(forceIncludeCaptures: true, forceExcludeMoves: true).PieceMoves;

        foreach (var move in availableActions)
        {
            if (move.Piece.Color != attackedColor) continue; // keep only attacking moves

            var toPos = move.ChessAction.To;
            var attackedPosition = (toPos.Row, toPos.Col);

            if (!cellsDict.TryGetValue(attackedPosition, out List<PieceAction>? attackingMoves))
            {
                attackingMoves = [];
                cellsDict[attackedPosition] = attackingMoves;
            }

            attackingMoves.Add(move);
        }
        return [.. cellsDict.Select(kvp => new AttackedCell(
            row: kvp.Key.Item1,
            col: kvp.Key.Item2,
            occupyingPiece: this[kvp.Key.Item1, kvp.Key.Item2],
            attackingMoves: kvp.Value
        ))];
    }


    public IEnumerable<BoardCell> GetThreatenedCells(PieceColor threatenedColor) => GetAttackedCells(threatenedColor)
        .Where(c => c.OccupyingPiece != null && c.OccupyingPiece.Color == threatenedColor);

    public IEnumerable<BoardCell> GetCheckedCells(PieceColor checkedColor)
    {
        var kingPositions = GetPiecesByType(checkedColor, PieceType.King);
        if (kingPositions.Count != 1) return Enumerable.Empty<BoardCell>();

        var kingPos = kingPositions.First();
        var attackedCells = GetAttackedCells(checkedColor);

        return attackedCells.Where(c => c.Row == kingPos.Row && c.Col == kingPos.Col);
    }

    public IEnumerable<BoardCell> GetEmptyCells()
    {
        var emptyCells = new List<BoardCell>();
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                if (this[row, col] == null)
                {
                    emptyCells.Add(new BoardCell(row, col));
                }
            }
        }
        return emptyCells;
    }

    public IEnumerable<BoardCell> GetOccupiedCells()
    {
        var occupiedCells = new List<BoardCell>();
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = this[row, col];
                if (piece != null)
                {
                    occupiedCells.Add(new BoardCell(row, col, piece));
                }
            }
        }
        return occupiedCells;
    }

    public IEnumerable<BoardCell> GetBlockedCells(PieceColor currentTurnColor)
    {
        // start by getting all available actions for the current turn color and record their "from" position
        // then find all cells that are occupied by pieces of the same color
        // then find the cells that are occupied by pieces of the opposite color and are not in the "from" position of any available action
        // those are the blocked cells
        var availableActions = GetAvailableActionsDetailed().PieceMoves
            .Where(m => m.Piece.Color == currentTurnColor)
            .Select(m => (m.ChessAction.From.Row, m.ChessAction.From.Col))
            .ToHashSet();
        var blockedCells = new List<BoardCell>();
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = _board[row, col];
                if (piece != null && piece.Color == currentTurnColor)
                {
                    //print
                    if (!availableActions.Contains((row, col)))
                    {
                        blockedCells.Add(new BoardCell(row, col, piece));
                    }
                }
            }
        }
        return blockedCells;
    }

    public IEnumerable<BoardCell> GetPinnedCells(PieceColor pinnedColor)
    {
        // start by getting the king position for the pinned color
        // then get all available actions for the opposite color
        // then, for each action, simulate the move and see if it results in a check on the king
        // flag each action with this information
        // then, for each cell occupied by a piece of the pinned color, flag the cell as pinned if all actions from that cell result in a check on the king
        var kingPositions = GetPiecesByType(pinnedColor, PieceType.King);
        if (kingPositions.Count != 1) return Enumerable.Empty<BoardCell>();
        var kingPos = kingPositions.First();
        var oppositeColor = pinnedColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
        var oppositeActions = GetAvailableActionsDetailed().PieceMoves
            .Where(m => m.Piece.Color == oppositeColor)
            .ToList();
        var pinnedCells = new List<BoardCell>();
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = this[row, col];
                if (piece != null && piece.Color == pinnedColor)
                {
                    var actionsFromCell = oppositeActions
                        .Where(m => m.ChessAction.From.Row == row && m.ChessAction.From.Col == col)
                        .ToList();
                    if (actionsFromCell.Count > 0 && actionsFromCell.All(a => GetNextState(a).GetCheckedCells(pinnedColor).Any()))
                    {
                        pinnedCells.Add(new BoardCell(row, col, piece));
                    }
                }
            }
        }
        return pinnedCells;
    }



    public IReadOnlyCollection<Position> GetPiecesByType(PieceColor pieceColor, PieceType pieceType)
    {
        var positions = new List<Position>();
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = this[row, col];
                if (piece != null && piece.Color == pieceColor && piece.Type == pieceType)
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

