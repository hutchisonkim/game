namespace Game.Chess.Entity;

public class ChessPattern
{
    public (int X, int Y) Delta { get; }
    public MirrorBehavior Mirrors { get; }
    public bool Repeats { get; }
    public CaptureBehavior Captures { get; }

    public ChessPattern((int X, int Y) vector,
        MirrorBehavior mirrors = MirrorBehavior.All,
        bool repeats = true,
        CaptureBehavior captures = CaptureBehavior.MoveOrCapture)
    {
        Delta = vector;
        Mirrors = mirrors;
        Repeats = repeats;
        Captures = captures;
    }
}

[Flags]
public enum MirrorBehavior { None = 0, Horizontal = 1, Vertical = 2, All = Horizontal | Vertical }
[Flags]
public enum CaptureBehavior { None = 0, Move = 1, Capture = 2, MoveOrCapture = Move | Capture, Castle = 4 }
