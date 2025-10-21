using Game.Core.Serialization;
using Game.Chess.Entity;

namespace Game.Chess.Serialization;

public sealed class ChessPieceSerializer : ISerializer<ChessPiece>
{
    public string Serialize(ChessPiece value) => ((int)value.Attributes).ToString();

    public ChessPiece Deserialize(string data)
    {
        if (!int.TryParse(data, out int attr))
            throw new FormatException("Invalid piece attribute data");
        return new ChessPiece((ChessPieceAttribute)attr);
    }
}