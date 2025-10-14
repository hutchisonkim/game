namespace Game.Core.Pages.Domain;

public class Page
{
    public string Id { get; }
    public string GameId { get; }
    public string StateJson { get; private set; }
    public DateTime LastUpdated { get; private set; }

    public Page(string id, string gameId, string stateJson)
    {
        Id = id;
        GameId = gameId;
        StateJson = stateJson;
        LastUpdated = DateTime.UtcNow;
    }

    public void ApplyState(string newStateJson)
    {
        StateJson = newStateJson;
        LastUpdated = DateTime.UtcNow;
    }
}
