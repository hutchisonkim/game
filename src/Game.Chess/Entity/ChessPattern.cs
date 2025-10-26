namespace Game.Chess.Entity;

public readonly record struct ChessPattern(
    (int X, int Y) Delta,
    MirrorBehavior Mirrors = MirrorBehavior.Both,
    bool Repeats = true,
    CaptureBehavior Captures = CaptureBehavior.MoveOrReplace)
{
    public bool CanCapture => Captures.HasFlag(CaptureBehavior.Replace);
}

[Flags]
public enum MirrorBehavior { None = 0, Horizontal = 1, Vertical = 2, Both = Horizontal | Vertical }
[Flags]
public enum CaptureBehavior { None = 0, Move = 1, Replace = 2, MoveOrReplace = Move | Replace, Swap = 4 }
