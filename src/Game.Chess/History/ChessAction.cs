using Game.Core;
using Game.Chess.Entity;

namespace Game.Chess.History;

public sealed class ChessAction(ChessPosition from, ChessPosition to) : IAction
{
    public ChessPosition From { get; } = from;
    public ChessPosition To { get; } = to;
}
