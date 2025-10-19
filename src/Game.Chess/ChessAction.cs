using Game.Core;

namespace Game.Chess;

public readonly record struct Position(int Row, int Col)
{
    public bool IsValid => Row >= 0 && Row < 8 && Col >= 0 && Col < 8;
    public override string ToString() => $"{ColToFile(Col)}{RowToRank(Row)}";
    private static string RowToRank(int row) => (8 - row).ToString();
    private static string ColToFile(int col) => ((char)('a' + col)).ToString();
}

public sealed class ChessAction(Position from, Position to) : IAction
{
    public Position From { get; } = from;
    public Position To { get; } = to;
    public string Description => $"{From}:{To}";
}
