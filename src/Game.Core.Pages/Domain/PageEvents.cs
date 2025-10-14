namespace Game.Core.Pages.Domain;

public record PageUpdatedEvent(string PageId, string GameId, DateTime Timestamp);
