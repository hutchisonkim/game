namespace Game.Core.Renders;

public interface IView<TAction, TState>
    where TAction : IAction
    where TState : IState<TAction, TState>
{
    byte[] RenderStatePng(TState state, int stateSize = 400);
    byte[] RenderPreTransitionPng(TState stateFrom, TState stateTo, TAction action, int stateSize = 400);
    byte[] RenderPostTransitionPng(TState stateFrom, TState stateTo, TAction action, int stateSize = 400);
    byte[] RenderTransitionGif(TState stateFrom, TState stateTo, TAction action, int stateSize = 400);
    byte[] RenderTransitionSequenceGif(IEnumerable<(TState stateFrom, TState stateTo, TAction action)> transitions, int stateSize = 400);
}


public abstract class ViewBase<TAction, TState> : IView<TAction, TState>
    where TAction : IAction
    where TState : IState<TAction, TState>
{
    public abstract byte[] RenderStatePng(TState state, int stateSize = 400);
    public abstract byte[] RenderPreTransitionPng(TState stateFrom, TState stateTo, TAction action, int stateSize = 400);
    public abstract byte[] RenderPostTransitionPng(TState stateFrom, TState stateTo, TAction action, int stateSize = 400);
    public abstract byte[] RenderTransitionGif(TState stateFrom, TState stateTo, TAction action, int stateSize = 400);
    public abstract byte[] RenderTransitionSequenceGif(IEnumerable<(TState stateFrom, TState stateTo, TAction action)> transitions, int stateSize = 400);
}
