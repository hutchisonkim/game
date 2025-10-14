namespace Game.Core.Pages.Contract;

public record PageQuery(
    string GameId,
    string PageId,
    string QueryType
);
