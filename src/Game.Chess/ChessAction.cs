// Game.Chess/ChessAction.cs
using Game.Core;

namespace Game.Chess;

public readonly record struct Position(int Row, int Col)
{
    public bool IsValid => Row >= 0 && Row < 8 && Col >= 0 && Col < 8;
    public override string ToString() => $"({Row},{Col})";
}

/// <summary>
/// Chess move: from -> to. Promotion info omitted for now.
/// </summary>
public sealed class ChessMove : IAction
{
    public Position From { get; }
    public Position To { get; }

    public string Description => $"{From} -> {To}";

    public ChessMove(Position from, Position to)
    {
        From = from;
        To = to;
    }
}
