// Game.Core/State.cs
using System;

namespace Game.Core;

public interface IState
{
    /// <summary>
    /// Create a deep-ish clone suitable for branching/inspection.
    /// Implementations should copy internal mutable structures.
    /// </summary>
    IState Clone();
}
