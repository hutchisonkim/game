using Xunit;
using Game.Core;

namespace Game.Core.Tests.Unit;

[Trait("Category","Unit")]
public class StateTests
{
    [Fact]
    [Trait("Feature","Clone")]
    public void Clone_ReturnsDistinctCopy()
    {
        // Arrange: create a simple state implementation for test
        var state = new TestState();

        // Act
        var clone = state.Clone();

        // Assert: different reference but equal behavior
        Assert.NotSame(state, clone);
        Assert.IsType<TestState>(clone);
        var s2 = (TestState)clone;
        Assert.Equal("value", s2.Data);

        // Mutate original and ensure clone unaffected
        state.Data = "changed";
        Assert.Equal("value", s2.Data);
    }

    private sealed class TestState : IState
    {
        public string Data { get; set; } = "value";
        public IState Clone()
        {
            // shallow clone for test
            return new TestState { Data = this.Data };
        }
    }
}
