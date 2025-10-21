using System;

namespace Game.Chess.Serialization;

// Chess-specific serialization helpers. Use only primitive or simple types in the public API
// to avoid creating circular project references with the domain project.
public static class ChessSerializer
{
    // Accept piece type flag as an integer (enum underlying value) and white/black as bool.
    public static char ToFenChar(int typeFlagValue, bool isWhite)
    {
        // Use the same flag bit checks but operate on the integer value.
        // Constants mirror PieceAttribute bit flags from the domain project.
        const int Pawn = 1 << 3;
        const int Rook = 1 << 4;
        const int Knight = 1 << 5;
        const int Bishop = 1 << 6;
        const int Queen = 1 << 7;
        const int King = 1 << 8;

        char fenChar = typeFlagValue switch
        {
            var t when (t & Pawn) != 0 => 'P',
            var t when (t & Rook) != 0 => 'R',
            var t when (t & Knight) != 0 => 'N',
            var t when (t & Bishop) != 0 => 'B',
            var t when (t & Queen) != 0 => 'Q',
            var t when (t & King) != 0 => 'K',
            _ => throw new ArgumentOutOfRangeException(nameof(typeFlagValue))
        };

        return isWhite ? fenChar : char.ToLower(fenChar);
    }

    // Return a short piece type description (FEN-like single-char string)
    public static string PieceTypeDescription(int typeFlagValue, bool isWhite)
        => $"{ToFenChar(typeFlagValue, isWhite)}";

    // Serialize a single action description fragment. Kept simple so callers can pass any description.
    public static string SerializeActionDescription(string description)
        => $"{description}:";

    // Serialize an initial-setup fragment for a square with position and piece type description.
    // positionText should already be computed by the domain (e.g., Position.ToString()).
    public static string SerializeInitialSquare(string positionText, string pieceTypeDescription)
        => $":{positionText}:{pieceTypeDescription}";
}
