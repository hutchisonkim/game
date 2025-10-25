namespace Game.Chess.Entity;

public class ChessPattern
{
    public (int X, int Y) Delta { get; }
    public MirrorBehavior Mirrors { get; }
    public bool Repeats { get; }
    public CaptureBehavior Captures { get; }
    public bool ForwardOnly { get; }

    public ChessPattern((int X, int Y) vector,
        MirrorBehavior mirrors = MirrorBehavior.All,
        bool repeats = true,
        CaptureBehavior captures = CaptureBehavior.MoveOrCapture,
        bool forwardOnly = false)
    {
        Delta = vector;
        Mirrors = mirrors;
        Repeats = repeats;
        Captures = captures;
        ForwardOnly = forwardOnly;
    }
}

[Flags]
public enum MirrorBehavior { None = 0, Horizontal = 1, Vertical = 2, All = Horizontal | Vertical }
public enum CaptureBehavior { MoveOnly, CaptureOnly, MoveOrCapture, CastleOnly }
