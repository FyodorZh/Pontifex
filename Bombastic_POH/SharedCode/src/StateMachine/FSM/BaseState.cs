
public class BaseState<TContext, TType> : IState<TType>
{
    /// <summary>
    /// контекст в котором выполняется состояние
    /// </summary>
    protected TContext _context;

    public BaseState( TContext context)
    {
        _context = context;
    }

    /// <summary>
    /// переинициализация состояния
    /// </summary>
    public virtual void ReInit()
    {
    }

    /// <summary>
    /// вход св состояние
    /// </summary>
    public virtual void Enter()
    {
    }

    /// <summary>
    /// выполнение состояния
    /// </summary>
    /// <param name="deltaTime"></param>
    /// <returns></returns>
    public virtual void Execute(float deltaTime = 0)
    {
    }

    /// <summary>
    /// выход из состояния
    /// </summary>
    public virtual void Exit()
    {
    }

    FiniteStateMachine<TType> _stateMachine;
    public FiniteStateMachine<TType> StateMachine
    {
        get
        {
            return _stateMachine;
        }
        set
        {
            _stateMachine = value;
        }
    }
}

public enum FSMResult
{
    Continiue,
    Finished,
    Cancel
}