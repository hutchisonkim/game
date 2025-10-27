using Game.Core.Serialization;
using Game.Chess.Entity;

namespace Game.Chess.Serialization;

//TODO: refactor as board serializer
public sealed class ChessPositionSerializer : ISerializer<(int X, int Y)>
{
    public string Serialize((int X, int Y) value) => value.ToString();

    public (int X, int Y) Deserialize(string data)
    {
        if (string.IsNullOrEmpty(data) || data.Length < 2)
            throw new FormatException("Invalid position text");

        char file = data[0];
        if (file < 'a' || file > 'h') throw new FormatException("Invalid file");

        if (!int.TryParse(data.AsSpan(1), out int rank)) throw new FormatException("Invalid rank");
        int col = file - 'a';
        int row = 8 - rank;
        (int X, int Y) pos = new(row, col);
        return pos;
    }
}
