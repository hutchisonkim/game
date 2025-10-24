namespace Game.Core.Renders;

public interface IView<TAction, TState>
    where TAction : IAction
    where TState : IState<TAction, TState>
{
    byte[] RenderStatePng(TState state, int stateSize);
    byte[] RenderPreTransitionPng(TState fromState, TState toState, TAction action, int stateSize, bool anchorTip);
    byte[] RenderPostTransitionPng(TState fromState, TState toState, TAction action, int stateSize, bool anchorTip);
    byte[] RenderTransitionGif(TState fromState, TState toState, TAction action, int stateSize, bool anchorTip);
    byte[] RenderTransitionSequenceGif(IEnumerable<(TState fromState, TState toState, TAction action, bool selected)> transitions, int stateSize, bool anchorTip);
}


public abstract class ViewBase<TAction, TState> : IView<TAction, TState>
    where TAction : IAction
    where TState : IState<TAction, TState>
{
    public abstract byte[] RenderStatePng(TState state, int stateSize);
    public abstract byte[] RenderPreTransitionPng(TState fromState, TState toState, TAction action, int stateSize, bool anchorTip);
    public abstract byte[] RenderPostTransitionPng(TState fromState, TState toState, TAction action, int stateSize, bool anchorTip);
    public abstract byte[] RenderTransitionGif(TState fromState, TState toState, TAction action, int stateSize, bool anchorTip);
    public abstract byte[] RenderTransitionSequenceGif(IEnumerable<(TState fromState, TState toState, TAction action, bool selected)> transitions, int stateSize, bool anchorTip);
}
