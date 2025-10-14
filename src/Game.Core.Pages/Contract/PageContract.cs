namespace Game.Core.Pages.Contract;

public interface IPageContract
{
    Task<PageResult> ExecuteCommandAsync(PageCommand command);
    Task<PageResult> QueryAsync(PageQuery query);
}
