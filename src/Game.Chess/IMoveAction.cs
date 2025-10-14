namespace Game.Core;

/// <summary>
/// Optional interface for actions that explicitly carry one or more move coordinates.
/// Views can use this to render move overlays without parsing action text or diffing boards.
/// </summary>
public interface IMoveAction : IAction
{
    /// <summary>
    /// Returns a sequence of moves as tuples: (fromRow, fromCol, toRow, toCol).
    /// Implementations should return an empty sequence if no explicit move is present.
    /// </summary>
    IEnumerable<(int fromR, int fromC, int toR, int toC)> Moves { get; }
}
