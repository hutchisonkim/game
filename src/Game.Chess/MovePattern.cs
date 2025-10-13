namespace Game.Chess;

// Represents a reusable description of how a piece can move relative to its position.
public interface IMovePattern
{
    IEnumerable<(int dr, int dc)> GetVectors();
    bool IsRepeatable { get; } // e.g., Rook slides, Knight doesn't
    bool CaptureOnly { get; }
    bool MoveOnly { get; }
}

public sealed class DirectionalPattern((int dr, int dc)[] vectors, bool isRepeatable, bool captureOnly = false, bool moveOnly = false) : IMovePattern
{
    private readonly (int dr, int dc)[] vectors = vectors;

    public IEnumerable<(int dr, int dc)> GetVectors() => vectors;
    public bool IsRepeatable { get; } = isRepeatable;
    public bool CaptureOnly { get; } = captureOnly;
    public bool MoveOnly { get; } = moveOnly;
}

public static class MovePatterns
{
    public static readonly IMovePattern Rook = new DirectionalPattern([(1,0), (-1,0), (0,1), (0,-1)], isRepeatable: true);
    public static readonly IMovePattern Bishop = new DirectionalPattern([(1,1), (1,-1), (-1,1), (-1,-1)], isRepeatable: true);
    public static readonly IMovePattern Queen = new DirectionalPattern([(1,0), (-1,0), (0,1), (0,-1), (1,1), (1,-1), (-1,1), (-1,-1)], isRepeatable: true);
    public static readonly IMovePattern Knight = new DirectionalPattern([(2,1),(1,2),(-1,2),(-2,1),(-2,-1),(-1,-2),(1,-2),(2,-1)], isRepeatable: false);
    public static readonly IMovePattern King = new DirectionalPattern([(1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)], isRepeatable: false);
}
