namespace Game.Chess.Entity;

public class ChessPattern
{
    public (int X, int Y) Delta { get; }
    public MirrorBehavior Mirrors { get; }
    public RepeatBehavior Repeats { get; }
    public CaptureBehavior Captures { get; }
    public bool ForwardOnly { get; }
    public bool Jumps { get; }

    public ChessPattern((int X, int Y) vector,
        MirrorBehavior mirrors = MirrorBehavior.All,
        RepeatBehavior repeats = RepeatBehavior.Repeatable,
        CaptureBehavior captures = CaptureBehavior.MoveOrCapture,
        bool forwardOnly = false,
        bool jumps = false)
    {
        Delta = vector;
        Mirrors = mirrors;
        Repeats = repeats;
        Captures = captures;
        ForwardOnly = forwardOnly;
        Jumps = jumps;
    }
}

[Flags]
public enum MirrorBehavior { None = 0, Horizontal = 1, Vertical = 2, All = Horizontal | Vertical }
public enum RepeatBehavior { NotRepeatable, Repeatable }
public enum CaptureBehavior { MoveOnly, CaptureOnly, MoveOrCapture, CastleOnly }
