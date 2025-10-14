using Game.Core;

namespace Game.Chess;

public readonly record struct Position(int Row, int Col)
{
    public bool IsValid => Row >= 0 && Row < 8 && Col >= 0 && Col < 8;
    public override string ToString() => $"({Row},{Col})";
}

public sealed class ChessMove(Position from, Position to) : IAction
{
    public Position From { get; } = from;
    public Position To { get; } = to;
    public string Description => $"{From} -> {To}";

    public IEnumerable<(int fromR, int fromC, int toR, int toC)> Moves
    {
        get
        {
            if (!From.IsValid || !To.IsValid) return Array.Empty<(int, int, int, int)>();
            return [(From.Row, From.Col, To.Row, To.Col)];
        }
    }

    public IStateDelta<ChessBoard> CreateDelta(ChessBoard state)
    {
        // Create a simple delta that when applied will move the piece.
        return new MoveDelta(From, To);
    }

    private sealed class MoveDelta : IStateDelta<ChessBoard>
    {
        private readonly Position _from;
        private readonly Position _to;

        public MoveDelta(Position from, Position to)
        {
            _from = from;
            _to = to;
        }

        public ChessBoard Apply(ChessBoard state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!_from.IsValid || !_to.IsValid) throw new ArgumentException("Invalid positions.");
            // Delegate to the state's Apply(ChessMove) implementation which knows how to construct
            // a correct next ChessBoard (including turn count handling).
            return state.Apply(new ChessMove(_from, _to));
        }
    }
}
