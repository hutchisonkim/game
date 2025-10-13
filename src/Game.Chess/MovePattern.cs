using System.Collections.Generic;

namespace Game.Chess;

// Represents a reusable description of how a piece can move relative to its position.
public interface IMovePattern
{
    IEnumerable<(int dr, int dc)> GetVectors();
    bool IsRepeatable { get; } // e.g., Rook slides, Knight doesn't
    bool CaptureOnly { get; }
    bool MoveOnly { get; }
}

public sealed class DirectionalPattern : IMovePattern
{
    private readonly (int dr, int dc)[] vectors;
    public DirectionalPattern((int dr, int dc)[] vectors, bool isRepeatable, bool captureOnly = false, bool moveOnly = false)
    {
        this.vectors = vectors;
        IsRepeatable = isRepeatable;
        CaptureOnly = captureOnly;
        MoveOnly = moveOnly;
    }

    public IEnumerable<(int dr, int dc)> GetVectors() => vectors;
    public bool IsRepeatable { get; }
    public bool CaptureOnly { get; }
    public bool MoveOnly { get; }
}

public static class MovePatterns
{
    public static readonly IMovePattern Rook = new DirectionalPattern(new[] { (1,0), (-1,0), (0,1), (0,-1) }, isRepeatable: true);
    public static readonly IMovePattern Bishop = new DirectionalPattern(new[] { (1,1), (1,-1), (-1,1), (-1,-1) }, isRepeatable: true);
    public static readonly IMovePattern Queen = new DirectionalPattern(new[] { (1,0), (-1,0), (0,1), (0,-1), (1,1), (1,-1), (-1,1), (-1,-1) }, isRepeatable: true);
    public static readonly IMovePattern Knight = new DirectionalPattern(new[] { (2,1),(1,2),(-1,2),(-2,1),(-2,-1),(-1,-2),(1,-2),(2,-1) }, isRepeatable: false);
    public static readonly IMovePattern King = new DirectionalPattern(new[] { (1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1) }, isRepeatable: false);
}
