namespace Game.Core;

public interface IPolicy<TState, TAction>
    where TState : IState<TAction, TState>
    where TAction : IAction
{
    IEnumerable<TAction> GetAvailableActions(TState state);
}
