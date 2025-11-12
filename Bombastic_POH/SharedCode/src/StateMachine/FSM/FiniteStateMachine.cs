using System.Collections.Generic;

public class FiniteStateMachine<T>
{
    public event System.Action<T> StateChanged;
    public event System.Action<T, T> StateWillChange;

    public bool IsFrozen { get; set; }

    public bool LogEnabled { get; set; }
    
    public T CurrentStateType
    {
        get { return _currentStateType; }
    }

    public virtual IState<T> CurrentState
    {
        get { return _currentState;  }
    }

    protected Dictionary<T, IState<T>> _states = new Dictionary<T, IState<T>>();
    protected IState<T> _currentState;
    protected T _currentStateType;

    public FiniteStateMachine()
    {
        
    }

    public void AddState(T nameOfState, IState<T> newState)
    {
        newState.StateMachine = this;
        _states.Add( nameOfState, newState );
    }

    public void SetStartState(T startState )
    {
        _currentState =  _states[startState];
        _currentStateType = startState;
        if (LogEnabled)
        {
            Log.i("<b>" + this.GetType() + ": Start state was changed to " + _currentStateType + "</b>");
        }

        _currentState.Enter();
    }

    // TODO: code duplication for efficiency below now. Remove in the future
    public virtual void SwitchStateWithParameters<TContext>(T nextStateType, TContext parameters, bool force = false)
    {
        if (nextStateType.Equals(_currentStateType) && !force)
        {
            if (LogEnabled)
            {
                Log.w("Trying switch to same state, switch disabled " + nextStateType.ToString());
            }
            return;
        }
        IState<T> newState = GetState(nextStateType);

        if (newState != null)
        {
            if (_currentState != null)
            {
                _currentState.Exit();
            }

            var prevStateType = _currentStateType;

            _currentStateType = nextStateType;
            _currentState = newState;
            _currentState.ReInit();

            var parameterizedState = _currentState as IParametrizedState<TContext>;
            if (parameterizedState != null)
            {
                parameterizedState.SetParameters(parameters);
            }
            else
            {
                throw new System.Exception("SwitchStateWithParameters with null argument. Possibly missing base class generic param");
            }
            if (LogEnabled)
            {
                Log.i("<b>{0}: Transition {1} -> {2} ({3})</b>", this.GetType(), prevStateType, _currentStateType, parameters);
            }
            _currentState.Enter();
            if (StateChanged != null)
            {
                StateChanged.Invoke(CurrentStateType);
            }
        }
    }

    public virtual void SwitchState(T nextStateType, bool force = false)
    {
        if (nextStateType.Equals(_currentStateType) && !force)
        {
            if (LogEnabled)
            {
                Log.w("Trying switch to same state, switch disabled " + nextStateType.ToString());
            }
            return;
        }
        IState<T> newState = GetState(nextStateType);

        if (newState != null)
        {
            if (_currentState != null)
            {
                _currentState.Exit();
            }

            var prevStateType = _currentStateType;
            if (StateWillChange != null)
            {
                StateWillChange.Invoke(prevStateType, nextStateType);
            }

            _currentState = newState;
            _currentState.ReInit();
            _currentStateType = nextStateType;

            if (LogEnabled)
            {
                Log.i("<b>{0}: Transition {1} -> {2}</b>", this.GetType(), prevStateType, _currentStateType);
            }
            _currentState.Enter();
            if (StateChanged != null)
            {
                StateChanged.Invoke(CurrentStateType);
            }
        }
     }

    public virtual void Execute(float deltaState = 0)
    {
        if (IsFrozen)
        {
            return;
        }

        if (_currentState != null)
        {
            _currentState.Execute(deltaState);
        }
    }

    public IState<T> GetState(T stateType)   
    {
        IState<T> result = null;
      
        if (_states.ContainsKey(stateType))
        {
            result = _states[stateType];
            
        }
        if (result == null)
        {
            Log.e("FiniteStateMachine.GetState: fsm {0} don't have state with name: {1}", GetType(), stateType);
        }
        return result;
    }
}
