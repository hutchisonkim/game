namespace Game.Chess.Entity;

public readonly record struct ChessBoard(int Width, int Height, ChessPiece[,] Cell)
{
    public void Initialize(ChessPieceAttribute pieceAttributeOverride = ChessPieceAttribute.None)
    {
        var initial = ChessEntityUtility.CreateInitialBoard(pieceAttributeOverride);
        Array.Copy(initial, Cell, Cell.Length);
    }

    public bool IsInside(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    public static ChessBoard Default => new(8, 8, new ChessPiece[8, 8]);
}
