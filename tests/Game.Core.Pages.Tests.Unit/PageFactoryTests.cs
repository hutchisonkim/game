using Xunit;
using Game.Core.Pages.Domain;

namespace Game.Core.Pages.Tests.Unit;

[Trait("Category", "Unit")]
public class PageFactoryTests
{
    [Fact]
    [Trait("Feature", "Factory")]
    public void CreateNew_WithGameIdAndState_ReturnsPageWithSameGameIdAndState()
    {
        var gameId = "game-123";
        var initial = "{\"status\":\"init\"}";

        var page = PageFactory.CreateNew(gameId, initial);

        Assert.NotNull(page);
        Assert.Equal(gameId, page.GameId);
        Assert.Equal(initial, page.StateJson);
        Assert.False(string.IsNullOrWhiteSpace(page.Id));
    }
}
