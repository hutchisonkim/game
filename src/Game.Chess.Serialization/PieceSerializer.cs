using System;
using Game.Core.Serialization;

namespace Game.Chess.Serialization;

public sealed class PieceSerializer : ISerializer<Piece>
{
    public string Serialize(Piece value) => ((int)value.Attributes).ToString();

    public Piece Deserialize(string data)
    {
        if (!int.TryParse(data, out int attr))
            throw new FormatException("Invalid piece attribute data");
        return new Piece((PieceAttribute)attr);
    }
}