using System.Linq;
using Xunit;
using Game.Chess;

namespace Game.Chess.Tests.Unit;

[Trait("Category","Unit")]
public class ChessPolicyTests
{
    [Fact]
    [Trait("Feature","PawnMoves")]
    public void Pawn_InitialPosition_IncludesDoubleMove()
    {
        var s = new ChessState();
        var policy = new ChessPolicy();

        // White pawn at (1,0) should have move to (2,0) and (3,0)
        var moves = policy.GetAvailableActions(s)
            .Where(m => m.From.Row == 1 && m.From.Col == 0)
            .Select(m => (m.To.Row, m.To.Col))
            .ToArray();

        Assert.Contains((2, 0), moves);
        Assert.Contains((3, 0), moves);
    }

    [Fact]
    [Trait("Feature","PawnCapture")]
    public void Pawn_Capture_IsGenerated()
    {
        var s = new ChessState();
        // place a black pawn diagonally in front of white pawn
        s.Board[2, 1] = new Piece(PieceColor.Black, PieceType.Pawn);

        var policy = new ChessPolicy();
        var moves = policy.GetAvailableActions(s)
            .Where(m => m.From.Row == 1 && m.From.Col == 0)
            .Select(m => (m.To.Row, m.To.Col))
            .ToArray();

        Assert.Contains((2, 1), moves);
    }
}
