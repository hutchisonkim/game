namespace Game.Core.Serialization;

/// <summary>
/// Generic serializer contract used across games and renderers.
/// Implementations should use only simple/primitive public types in their API.
/// </summary>
/// <typeparam name="T">Type that will be serialized/deserialized.</typeparam>
public interface ISerializer<T>
{
    /// <summary>
    /// Serialize the provided object to a string representation.
    /// </summary>
    string Serialize(T value);

    /// <summary>
    /// Deserialize the provided string back into <typeparamref name="T"/>.
    /// Implementations should throw on invalid input.
    /// </summary>
    T Deserialize(string data);
}
