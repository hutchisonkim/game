using Xunit;

namespace Game.Core.Tests.Unit;

[Trait("Category", "Unit")]
public class StateTests
{
    [Fact]
    [Trait("Feature", "Clone")]
    public void Clone_WhenCalled_ReturnsDistinctCopy()
    {
        // Arrange: create a simple state implementation for test
        var state = new TestState();

        // Act
        var clone = state.Clone();

        // Assert: different reference but equal behavior
        Assert.NotSame(state, clone);
        Assert.IsType<TestState>(clone);
        var s2 = clone;
        Assert.Equal("value", s2.Data);

        // Mutate original and ensure clone unaffected
        state.Data = "changed";
        Assert.Equal("value", s2.Data);
    }

    private sealed class TestState : IState<TestAction, TestState>
    {
        public string Data { get; set; } = "value";
        public TestState Clone()
        {
            return new TestState { Data = Data };
        }

        public TestState Apply(TestAction action)
        {
            return this;
        }
    }

    private sealed class TestAction : IAction
    {
    }
}
