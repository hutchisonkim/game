namespace Game.Chess.Entity;

public class ChessPattern((int X, int Y) vector,
    MirrorBehavior mirrors = MirrorBehavior.Both,
    bool repeats = true,
    CaptureBehavior captures = CaptureBehavior.MoveOrReplace)
{
    public (int X, int Y) Delta { get; } = vector;
    public MirrorBehavior Mirrors { get; } = mirrors;
    public bool Repeats { get; } = repeats;
    public CaptureBehavior Captures { get; } = captures;
}

[Flags]
public enum MirrorBehavior { None = 0, Horizontal = 1, Vertical = 2, Both = Horizontal | Vertical }
[Flags]
public enum CaptureBehavior { None = 0, Move = 1, Replace = 2, MoveOrReplace = Move | Replace, Swap = 4 }
