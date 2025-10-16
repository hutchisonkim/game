using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Game.Core;

namespace Game.Chess.PolicyB;

// 🔹 Lightweight value object for move vectors
public readonly struct Vector2
{
    public int X { get; }
    public int Y { get; }

    public Vector2(int x, int y) => (X, Y) = (x, y);

    public static readonly Vector2 ZeroByOne = new(0, 1);
    public static readonly Vector2 OneByOne = new(1, 1);
    public static readonly Vector2 OneByTwo = new(1, 2);
}

// 🔹 Move rules
public class Move
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

    public Move(MoveOptions options)
    {
        Vector = options.Vector;
        Mirrors = options.Mirrors;
        Repeats = options.Repeats;
        Captures = options.Captures;
        ForwardOnly = options.ForwardOnly;
        Jumps = options.Jumps;
    }

    public record MoveOptions(
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
    public PieceColor Color { get; }
    public abstract PieceType Type { get; }
    public abstract PiecePolicy Policy { get; }
    public int ForwardAxis => Color == PieceColor.White ? -1 : 1;

    protected Piece(PieceColor color) => Color = color;

    // Returns all abstract moves for the piece
    public abstract IEnumerable<Move> GetMoves();

    // Returns all legal moves given the board and current position
    public IEnumerable<(int row, int col, Move move)> GetAvailableActions(ChessBoard board, int row, int col)
    {
        foreach (var move in GetMoves())
        {
            var directions = GetMirroredVectors(move);

            foreach (var dir in directions)
            {
                int dx = dir.X;
                int dy = dir.Y * ForwardAxis;
                int x = col;
                int y = row;

                int steps = 0;
                do
                {
                    x += dx;
                    y += dy;
                    steps++;

                    if (!board.IsInside(y, x)) break;

                    var target = board[y, x];

                    if (target == null)
                    {
                        if (move.Captures != Move.CaptureBehavior.CaptureOnly)
                            yield return (y, x, move);
                    }
                    else
                    {
                        if (target.Color != Color && (move.Captures != Move.CaptureBehavior.MoveOnly))
                        {
                            yield return (y, x, move);
                        }
                        break; // cannot jump over except knights
                    }

                    if (move.Repeats == Move.RepeatBehavior.NotRepeatable ||
                        (move.Repeats == Move.RepeatBehavior.RepeatableOnce && steps == 1) ||
                        move.Jumps)
                        break;

                } while (true);
            }
        }
    }

    private IEnumerable<Vector2> GetMirroredVectors(Move move)
    {
        yield return new Vector2(move.Vector.X, move.Vector.Y);

        if (move.Mirrors.HasFlag(Move.MirrorBehavior.Horizontal) && move.Vector.X != 0)
            yield return new Vector2(-move.Vector.X, move.Vector.Y);

        if (move.Mirrors.HasFlag(Move.MirrorBehavior.Vertical) && move.Vector.Y != 0)
            yield return new Vector2(move.Vector.X, -move.Vector.Y);

        if (move.Mirrors.HasFlag(Move.MirrorBehavior.All) && move.Vector.X != 0 && move.Vector.Y != 0)
            yield return new Vector2(-move.Vector.X, -move.Vector.Y);
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

    public override IEnumerable<Move> GetMoves() => new[]
    {
        new Move(new Move.MoveOptions(Vector2.ZeroByOne, Mirrors: Move.MirrorBehavior.Horizontal, Repeats: Move.RepeatBehavior.NotRepeatable, Captures: Move.CaptureBehavior.MoveOnly, ForwardOnly: true)),
        new Move(new Move.MoveOptions(Vector2.ZeroByOne, Mirrors: Move.MirrorBehavior.Horizontal, Repeats: Move.RepeatBehavior.RepeatableOnce, Captures: Move.CaptureBehavior.MoveOnly, ForwardOnly: true)),
        new Move(new Move.MoveOptions(Vector2.OneByOne, Mirrors: Move.MirrorBehavior.Horizontal, Repeats: Move.RepeatBehavior.NotRepeatable, Captures: Move.CaptureBehavior.CaptureOnly, ForwardOnly: true))
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

    public override IEnumerable<Move> GetMoves() => new[]
    {
        new Move(new Move.MoveOptions(Vector2.ZeroByOne))
    };
}

public class Knight : Piece
{
    public override PieceType Type => PieceType.Knight;
    public override PiecePolicy Policy => Standard;

    public static readonly PiecePolicy Standard = new(PieceType.Knight);

    public Knight(PieceColor color) : base(color) { }

    public override IEnumerable<Move> GetMoves() => new[]
    {
        new Move(new Move.MoveOptions(Vector2.OneByTwo, Repeats: Move.RepeatBehavior.NotRepeatable, Jumps: true))
    };
}

public class Bishop : Piece
{
    public override PieceType Type => PieceType.Bishop;
    public override PiecePolicy Policy => Standard;

    public static readonly PiecePolicy Standard = new(PieceType.Bishop);

    public Bishop(PieceColor color) : base(color) { }

    public override IEnumerable<Move> GetMoves() => new[]
    {
        new Move(new Move.MoveOptions(Vector2.OneByOne))
    };
}

public class Queen : Piece
{
    public override PieceType Type => PieceType.Queen;
    public override PiecePolicy Policy => Standard;

    public static readonly PiecePolicy Standard = new(PieceType.Queen);

    public Queen(PieceColor color) : base(color) { }

    public override IEnumerable<Move> GetMoves() => new[]
    {
        new Move(new Move.MoveOptions(Vector2.ZeroByOne)),
        new Move(new Move.MoveOptions(Vector2.OneByOne))
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

    public override IEnumerable<Move> GetMoves() => new[]
    {
        new Move(new Move.MoveOptions(Vector2.ZeroByOne, Repeats: Move.RepeatBehavior.NotRepeatable)),
        new Move(new Move.MoveOptions(Vector2.OneByOne, Repeats: Move.RepeatBehavior.NotRepeatable))
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
public class ChessBoard : IState<PositionDelta, ChessBoard>
{
    private readonly Piece?[,] _board = new Piece?[8, 8];

    public Piece?[,] Board => _board;
    public int TurnCount { get; private set; }

    public ChessBoard()
    {
        TurnCount = 0;

        for (int x = 0; x < 8; x++)
        {
            _board[1, x] = PieceFactory.Create(PieceType.Pawn, PieceColor.Black);
            _board[6, x] = PieceFactory.Create(PieceType.Pawn, PieceColor.White);
        }

        // Rooks
        _board[0, 0] = _board[0, 7] = PieceFactory.Create(PieceType.Rook, PieceColor.Black);
        _board[7, 0] = _board[7, 7] = PieceFactory.Create(PieceType.Rook, PieceColor.White);

        // Knights
        _board[0, 1] = _board[0, 6] = PieceFactory.Create(PieceType.Knight, PieceColor.Black);
        _board[7, 1] = _board[7, 6] = PieceFactory.Create(PieceType.Knight, PieceColor.White);

        // Bishops
        _board[0, 2] = _board[0, 5] = PieceFactory.Create(PieceType.Bishop, PieceColor.Black);
        _board[7, 2] = _board[7, 5] = PieceFactory.Create(PieceType.Bishop, PieceColor.White);

        // Queens
        _board[0, 3] = PieceFactory.Create(PieceType.Queen, PieceColor.Black);
        _board[7, 3] = PieceFactory.Create(PieceType.Queen, PieceColor.White);

        // Kings
        _board[0, 4] = PieceFactory.Create(PieceType.King, PieceColor.Black);
        _board[7, 4] = PieceFactory.Create(PieceType.King, PieceColor.White);
    }

    public Piece? this[int row, int col] => _board[row, col];

    public bool IsInside(int row, int col) => row >= 0 && row < 8 && col >= 0 && col < 8;

    public ChessBoard Clone()
    {
        var copy = new Piece?[8, 8];
        Array.Copy(_board, copy, _board.Length);
        var newBoard = new ChessBoard();
        Array.Copy(copy, newBoard._board, copy.Length);
        newBoard.TurnCount = TurnCount;
        return newBoard;
    }

    public ChessBoard Apply(PositionDelta action)
    {
        if (!IsInside(action.From.Row, action.From.Col) || !IsInside(action.To.Row, action.To.Col))
            throw new ArgumentException("Invalid move positions.");

        var piece = _board[action.From.Row, action.From.Col];
        if (piece == null)
            throw new InvalidOperationException("No piece at the source position.");

        var newBoard = new ChessBoard();
        Array.Copy(_board, newBoard._board, _board.Length);

        // Move the piece
        newBoard._board[action.To.Row, action.To.Col] = piece;
        newBoard._board[action.From.Row, action.From.Col] = null;

        newBoard.TurnCount = this.TurnCount + 1;

        return newBoard;
    }

    public IEnumerable<PositionDelta> GetAvailableActions()
    {
        var currentColor = (TurnCount % 2 == 0) ? PieceColor.White : PieceColor.Black;
        return GetAvailableActionsDetailed().ChessMoves
            .Where(m => m.Piece.Color == currentColor)
            .Select(m => m.PositionDelta);
    }

    public record AvailableActionsResult(
        IEnumerable<PieceMove> ChessMoves
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

    public ChessBoard GetNextState(PieceMove move) => Apply(move.PositionDelta);
    public AvailableActionsResult GetNextAvailableActionsDetailed(PieceMove move) => GetNextState(move).GetAvailableActionsDetailed();

    public AvailableActionsResult GetAvailableActionsDetailed()
    {

        var pieceMoves = new List<PieceMove>();

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = this[row, col];
                if (piece == null) continue;

                var actions = piece.GetAvailableActions(this, row, col)
                    .Select(t => new PositionDelta(new Position(row, col), new Position(t.row, t.col)));

                pieceMoves.AddRange(actions.Select(a => new PieceMove(a, piece)));
            }
        }

        return new AvailableActionsResult(pieceMoves);
    }

    public static IEnumerable<Position> GetCheckedPositions(ChessBoard board, PieceColor checkedColor)
    {
        return board.GetAvailableActionsDetailed()
            .ChessMoves
            .Where(m => m.Piece.Color != checkedColor && m.Piece.Type != PieceType.King)
            .Select(m => m.PositionDelta.To)
            .Distinct();
    }

    public class PieceMove
    {
        public PositionDelta PositionDelta { get; }
        public Piece Piece { get; }
        public PieceMove(PositionDelta positionDelta, Piece piece)
        {
            PositionDelta = positionDelta;
            Piece = piece;
        }
    }
}

