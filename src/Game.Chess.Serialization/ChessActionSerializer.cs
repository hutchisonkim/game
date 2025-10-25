using Game.Core.Serialization;
using Game.Chess.Entity;
using Game.Chess.History;

namespace Game.Chess.Serialization;

public sealed class ChessActionSerializer : ISerializable<ChessAction>
{
    private readonly char _separator = ':';
    private readonly ChessPositionSerializer _pos = new();
    public string Serialize(ChessAction value) => string.Format("{0}{1}{2}", _pos.Serialize(value.From), _separator, _pos.Serialize(value.To));
    public ChessAction Deserialize(string data)
    {
        if (string.IsNullOrEmpty(data)) throw new FormatException("Invalid action text");
        string[] parts = data.Split(_separator);
        if (parts.Length != 2) throw new FormatException("Action must be 'from-to'");
        try
        {
            ChessPosition from = _pos.Deserialize(parts[0]);
            ChessPosition to = _pos.Deserialize(parts[1]);
            return new ChessAction(from, to);
        }
        catch (FormatException ex)
        {
            throw new FormatException("Invalid position in action", ex);
        }
    }
}