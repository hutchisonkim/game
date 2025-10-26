namespace Game.Core;

public interface IState<TAction, TSelf>
    where TSelf : IState<TAction, TSelf>
{
    TSelf Clone();
    TSelf Apply(TAction action);
}