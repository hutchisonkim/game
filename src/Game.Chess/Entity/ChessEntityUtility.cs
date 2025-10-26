namespace Game.Chess.Entity;

public static class ChessEntityUtility
{
    public static ChessPiece[,] CreateInitialBoard(ChessPieceAttribute pieceAttributeOverride = ChessPieceAttribute.None)
    {
        var board = new ChessPiece[8, 8];

        bool hasOverride = pieceAttributeOverride != ChessPieceAttribute.None;
        ChessPieceAttribute pieceAttr;

        // Pawns
        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Pawn;
        for (int x = 0; x < 8; x++)
        {
            board[x, 1] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.Mint | pieceAttr);
            board[x, 6] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.Mint | pieceAttr);
        }

        // Rooks
        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Rook;
        board[0, 0] = board[7, 0] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.Mint | pieceAttr);
        board[0, 7] = board[7, 7] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.Mint | pieceAttr);

        // Knights
        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Knight;
        board[1, 0] = board[6, 0] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.Mint | pieceAttr);
        board[1, 7] = board[6, 7] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.Mint | pieceAttr);

        // Bishops
        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Bishop;
        board[2, 0] = board[5, 0] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.Mint | pieceAttr);
        board[2, 7] = board[5, 7] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.Mint | pieceAttr);

        // Queens
        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.Queen;
        board[3, 0] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.Mint | pieceAttr);
        board[3, 7] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.Mint | pieceAttr);

        // Kings
        pieceAttr = hasOverride ? pieceAttributeOverride : ChessPieceAttribute.King;
        board[4, 0] = new ChessPiece(ChessPieceAttribute.White | ChessPieceAttribute.Mint | pieceAttr);
        board[4, 7] = new ChessPiece(ChessPieceAttribute.Black | ChessPieceAttribute.Mint | pieceAttr);

        return board;
    }

}
