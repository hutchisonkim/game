namespace Game.Core.View;

public interface IView<TState>
where TState : IState<IAction, TState>
{
    byte[] RenderPng(TState state, int width = 400, int height = 300);
}