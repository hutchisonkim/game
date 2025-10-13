// Game.Core/Action.cs
namespace Game.Core;

public interface IAction
{
    /// <summary>
    /// Human-readable description useful for debugging.
    /// </summary>
    string Description { get; }
}
