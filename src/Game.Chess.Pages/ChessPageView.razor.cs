using Microsoft.AspNetCore.Components;
using Game.Core.Pages.Contract;

namespace Game.Chess.Pages;

public partial class ChessPageView : ComponentBase
{
    [Inject] 
    public required IPageContract PageService { get; set; }

    protected ChessPageViewModel? ViewModel { get; private set; }

    private readonly ChessPagePresenter _presenter = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadPageStateAsync();
    }

    private async Task LoadPageStateAsync()
    {
        var result = await PageService.QueryAsync(new PageQuery("chess", "page-1", "GetState"));
        if (result.Success && result.Data is string json)
        {
            ViewModel = _presenter.Present(json);
        }
        else
        {
            ViewModel = new ChessPageViewModel("{ \"status\": \"error\" }");
        }

        StateHasChanged();
    }

    protected async Task OnMoveAsync(string from, string to)
    {
        var command = new PageCommand(
            "chess", 
            "page-1", 
            "MovePiece", 
            new() { ["from"] = from, ["to"] = to }
        );

        var result = await PageService.ExecuteCommandAsync(command);

        if (result.Success && result.Data is string json)
        {
            ViewModel = _presenter.Present(json);
        }

        StateHasChanged();
    }
}
