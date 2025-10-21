namespace Game.Core;

public interface IAction
{
    // Serialization of actions is intentionally moved out of the core domain.
    // Implementations should not expose a string Description here; serializers
    // live in language-specific serialization projects.
}