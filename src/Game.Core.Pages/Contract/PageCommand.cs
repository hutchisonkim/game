namespace Game.Core.Pages.Contract;

public record PageCommand(
    string GameId,
    string PageId,
    string ActionType,
    Dictionary<string, object>? Payload = null
);
