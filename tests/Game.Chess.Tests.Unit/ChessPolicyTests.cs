using Xunit;

namespace Game.Chess.Tests.Unit;

[Trait("Category", "Unit")]
public class ChessPolicyTests
{
    [Fact]
    [Trait("Feature", "PawnMovement")]
    public void GetAvailableActions_PawnInitialPosition_IncludesDoubleMove()
    {
        var s = new ChessBoard();
        var policy = new ChessRules();

        // White pawn at (1,0) should have move to (2,0) and (3,0)
        var moves = policy.GetAvailableActions(s)
            .Where(m => m.From.Row == 1 && m.From.Col == 0)
            .Select(m => (m.To.Row, m.To.Col))
            .ToArray();

        Assert.Contains((2, 0), moves);
        Assert.Contains((3, 0), moves);
    }

    [Fact]
    [Trait("Feature", "PawnCapture")]
    public void GetAvailableActions_PawnCapture_GeneratesCaptureAction()
    {
        var s = new ChessBoard();
        // place a black pawn diagonally in front of white pawn
        s.Board[2, 1] = new Piece(PieceColor.Black, PieceType.Pawn);

        var policy = new ChessRules();
        var moves = policy.GetAvailableActions(s)
            .Where(m => m.From.Row == 1 && m.From.Col == 0)
            .Select(m => (m.To.Row, m.To.Col))
            .ToArray();

        Assert.Contains((2, 1), moves);
    }

    [Fact]
    [Trait("Feature", "Initialization")]
    public void GetAvailableActions_InitialBoard_Has20Actions()
    {
        var s = new ChessBoard();
        var policy = new ChessRules();

        var actions = policy.GetAvailableActions(s)
            .ToArray();

        // At the initial position, White (starting side) should have 20 legal moves
        Assert.Equal(20, actions.Length);
    }
}
