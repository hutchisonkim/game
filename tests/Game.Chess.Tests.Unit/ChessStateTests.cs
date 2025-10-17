using Xunit;

namespace Game.Chess.Tests.Unit;

[Trait("Category", "Unit")]
public class ChessStateTests
{
    [Fact]
    [Trait("Feature", "InitialPosition")]
    public void Constructor_InitialPosition_HasCorrectPieces()
    {
        var s = new ChessBoard_Old();

        // Pawns
        for (int c = 0; c < 8; c++)
        {
            Assert.Equal(PieceType.Pawn, s.PieceAt(new Position(1, c))!.Type);
            Assert.Equal(PieceColor.White, s.PieceAt(new Position(1, c))!.Color);
            Assert.Equal(PieceType.Pawn, s.PieceAt(new Position(6, c))!.Type);
            Assert.Equal(PieceColor.Black, s.PieceAt(new Position(6, c))!.Color);
        }

        // Kings
        Assert.Equal(PieceType.King, s.PieceAt(new Position(0, 4))!.Type);
        Assert.Equal(PieceColor.White, s.PieceAt(new Position(0, 4))!.Color);
        Assert.Equal(PieceType.King, s.PieceAt(new Position(7, 4))!.Type);
        Assert.Equal(PieceColor.Black, s.PieceAt(new Position(7, 4))!.Color);
    }

    [Fact]
    [Trait("Feature", "ApplyMove")]
    public void ApplyMove_TwoSquarePawnMove_MovesPiece()
    {
        var s = new ChessBoard_Old();
        var from = new Position(1, 0); // white pawn
        var to = new Position(3, 0);   // two squares forward

        var moved = s.Apply(new BaseMove(from, to));

        Assert.Null(moved.PieceAt(from));
        var piece = moved.PieceAt(to);
        Assert.NotNull(piece);
        Assert.Equal(PieceType.Pawn, piece!.Type);
        Assert.Equal(PieceColor.White, piece.Color);
    }
}
