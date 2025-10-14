namespace Game.Chess.Pages;

public class ChessPagePresenter
{
    public ChessPageViewModel Present(string gameStateJson)
    {
        // Future: parse FEN, render moves, etc.
        return new ChessPageViewModel(gameStateJson);
    }
}