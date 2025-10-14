using Game.Core.Pages.Domain;
namespace Game.Core.Pages.Infrastructure;

public class PageRepository
{
    private readonly Dictionary<string, Page> _store = new();

    public Task<Page?> LoadAsync(string gameId, string pageId)
    {
        _store.TryGetValue($"{gameId}:{pageId}", out var page);
        return Task.FromResult(page);
    }

    public Task SaveAsync(Page page)
    {
        _store[$"{page.GameId}:{page.Id}"] = page;
        return Task.CompletedTask;
    }
}
