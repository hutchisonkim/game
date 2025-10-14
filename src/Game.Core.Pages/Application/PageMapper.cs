using Game.Core.Pages.Domain;
using Game.Core.Pages.Infrastructure;

namespace Game.Core.Pages.Application;

public static class PageMapper
{
    public static Page ToDomain(PageDataModel data)
        => new(data.Id, data.GameId, data.StateJson);

    public static PageDataModel ToDataModel(Page page)
        => new() { Id = page.Id, GameId = page.GameId, StateJson = page.StateJson };
}
