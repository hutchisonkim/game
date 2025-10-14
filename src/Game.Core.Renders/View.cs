namespace Game.Core.Renders;

public interface IView<TAction, TState, TView>
    where TAction : IAction
    where TState : IState<TAction, TState>
    where TView : IView<TAction, TState, TView>
{
    byte[] RenderStatePng(TState state, int stateSize = 400);
    byte[] RenderPreTransitionPng(TState stateFrom, TState stateTo, TAction action, int stateSize = 400);
    byte[] RenderPostTransitionPng(TState stateFrom, TState stateTo, TAction action, int stateSize = 400);
    byte[] RenderTransitionGif(TState stateFrom, TState stateTo, TAction action, int stateSize = 400);
    byte[] RenderTransitionSequenceGif(IEnumerable<(TState stateFrom, TState stateTo, TAction action)> transitions, int stateSize = 400);
}
