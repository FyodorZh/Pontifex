public interface IState<T>
{
    FiniteStateMachine<T> StateMachine {get; set;}

    void ReInit();
    void Enter();
    void Execute(float deltaTime = 0);
    void Exit();
}

public interface IParametrizedState<TContext>
{
    void SetParameters(TContext parameters);
}