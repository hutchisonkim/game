namespace Game.Core.Pages.Domain;

public static class PageFactory
{
    public static Page CreateNew(string gameId, string initialStateJson)
        => new(Guid.NewGuid().ToString(), gameId, initialStateJson);
}
