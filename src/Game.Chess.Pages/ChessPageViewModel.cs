namespace Game.Chess.Pages;

public class ChessPageViewModel
{
    public string StateJson { get; }

    public ChessPageViewModel(string stateJson)
    {
        StateJson = stateJson;
    }
}