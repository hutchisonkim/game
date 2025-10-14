using Game.Core.Pages.Contract;
using Game.Core.Pages.Domain;
using Game.Core.Pages.Infrastructure;

namespace Game.Core.Pages.Application;

public class PageHandler : IPageContract
{
    private readonly PageRepository _repo;

    public PageHandler(PageRepository repo)
    {
        _repo = repo;
    }

    public async Task<PageResult> ExecuteCommandAsync(PageCommand command)
    {
        Page page = await _repo.LoadAsync(command.GameId, command.PageId);
        if (page == null)
            return new PageResult(false, "Page not found", null);

        if (!PagePolicy.CanExecute(command.ActionType, "Player"))
            return new PageResult(false, "Forbidden", null);

        // Example: integrate with game core (placeholder)
        page.ApplyState("{\"status\":\"updated\"}");

        await _repo.SaveAsync(page);
        return new PageResult(true, "Command executed", page.StateJson);
    }

    public async Task<PageResult> QueryAsync(PageQuery query)
    {
        Page page = await _repo.LoadAsync(query.GameId, query.PageId);
        return page == null
            ? new PageResult(false, "Page not found", null)
            : new PageResult(true, null, page.StateJson);
    }
}
