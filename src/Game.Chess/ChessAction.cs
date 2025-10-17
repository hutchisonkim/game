using Game.Core;

namespace Game.Chess;

public readonly record struct Position(int Row, int Col)
{
    public bool IsValid => Row >= 0 && Row < 8 && Col >= 0 && Col < 8;
    public override string ToString() => $"({Row},{Col})";
}

public sealed class BaseAction(Position from, Position to) : IAction
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
}
