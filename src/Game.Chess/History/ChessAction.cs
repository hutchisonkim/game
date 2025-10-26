using Game.Core;

namespace Game.Chess.History;

public sealed class ChessAction((int X, int Y) from, (int X, int Y) to) : IAction
{
    public (int X, int Y) From { get; } = from;
    public (int X, int Y) To { get; } = to;
}
