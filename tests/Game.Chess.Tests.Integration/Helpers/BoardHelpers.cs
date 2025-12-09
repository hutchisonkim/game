using static Game.Chess.HistoryRefactor.ChessPolicyUtility;

namespace Game.Chess.Tests.Integration.Helpers;

/// <summary>
/// Helper methods for creating test board configurations.
/// </summary>
public static class BoardHelpers
{
    /// <summary>
    /// Creates an 8x8 board with all cells initialized to empty.
    /// </summary>
    public static Board CreateEmptyBoard()
    {
        var board = new Board(8, 8, new Piece[8, 8]);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                board.Cell[x, y] = Piece.Empty;
        return board;
    }

    /// <summary>
    /// Creates an 8x8 board with all cells initialized to empty, then places the specified piece at (x, y).
    /// </summary>
    public static Board CreateEmptyBoardWithPiece(int x, int y, Piece piece)
    {
        var board = CreateEmptyBoard();
        board.Cell[x, y] = piece;
        return board;
    }

    /// <summary>
    /// Creates an 8x8 board with all cells initialized to empty, then places multiple pieces.
    /// </summary>
    public static Board CreateBoardWithPieces(params (int X, int Y, Piece Piece)[] pieces)
    {
        var board = CreateEmptyBoard();
        foreach (var (x, y, piece) in pieces)
        {
            board.Cell[x, y] = piece;
        }
        return board;
    }
}
