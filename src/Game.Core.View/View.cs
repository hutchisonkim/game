namespace Game.Core.View;

public interface IView<TAction, TState, TView>
where TAction : IAction
where TState : IState<TAction, TState>
where TView : IView<TAction, TState, TView>
{
    byte[] RenderStatePng(TState state, int stateSize = 400);
    byte[] RenderTransitionPng(TState stateFrom, TState stateTo, TAction action, int stateSize = 400);
    byte[] RenderTimelinePng(IEnumerable<(TState state, TAction action)> history, int stateSize = 50);
    byte[] RenderTimelineGif(IEnumerable<(TState state, TAction action)> history, int stateSize = 400);
}