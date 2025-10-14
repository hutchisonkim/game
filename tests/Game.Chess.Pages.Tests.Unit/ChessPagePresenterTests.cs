using Xunit;
using Game.Chess.Pages;

namespace Game.Chess.Pages.Tests.Unit;

[Trait("Category", "Unit")]
public class ChessPagePresenterTests
{
    [Fact]
    [Trait("Feature", "Presentation")]
    public void Present_WithJson_ReturnsViewModelContainingJson()
    {
        var presenter = new ChessPagePresenter();
        var json = "{\"state\":\"x\"}";

        var vm = presenter.Present(json);

        Assert.NotNull(vm);
        Assert.Equal(json, vm.StateJson);
    }
}
