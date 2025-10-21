using System;
using Game.Core.Serialization;

namespace Game.Chess.Serialization;

public sealed class PositionSerializer : ISerializer<Position>
{
    public string Serialize(Position value) => value.ToString();

    public Position Deserialize(string data)
    {
        if (string.IsNullOrEmpty(data) || data.Length < 2)
            throw new FormatException("Invalid position text");

        char file = data[0];
        if (file < 'a' || file > 'h') throw new FormatException("Invalid file");

        if (!int.TryParse(data.AsSpan(1), out int rank)) throw new FormatException("Invalid rank");
        int col = file - 'a';
        int row = 8 - rank;
        Position pos = new(row, col);
        if (!pos.IsValid) throw new FormatException("Position out of range");
        return pos;
    }
}
