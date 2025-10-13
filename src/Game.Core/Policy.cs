// Game.Core/Policy.cs
using System.Collections.Generic;

namespace Game.Core;

public interface IPolicy<TState, TAction>
    where TState : IState
    where TAction : IAction
{
    /// <summary>
    /// Enumerate all available actions from the given state.
    /// </summary>
    IEnumerable<TAction> GetAvailableActions(TState state);
}
