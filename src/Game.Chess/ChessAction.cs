using Game.Core;

namespace Game.Chess;

public readonly record struct Position(int Row, int Col)
{
    public bool IsValid => Row >= 0 && Row < 8 && Col >= 0 && Col < 8;
    public override string ToString() => $"({Row},{Col})";
}

public sealed class PositionDelta(Position from, Position to) : IAction
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

    public IStateDelta<ChessBoard_Old> CreateDelta(ChessBoard_Old state)
    {
        // Create a simple delta that when applied will move the piece.
        return new MoveDelta(From, To);
    }

    private sealed class MoveDelta : IStateDelta<ChessBoard_Old>
    {
        private readonly Position _from;
        private readonly Position _to;

        public MoveDelta(Position from, Position to)
        {
            _from = from;
            _to = to;
        }

        public ChessBoard_Old Apply(ChessBoard_Old state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!_from.IsValid || !_to.IsValid) throw new ArgumentException("Invalid positions.");
            return state.Apply(new PositionDelta(_from, _to));
        }
    }
}
