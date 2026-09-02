namespace Pontifex.Utils.FSM
{
    public class SerializedFSM<TState> : IFSM<TState>
    {
        private readonly IFSM<TState> _inner;
        private readonly object _locker = new object();

        public SerializedFSM(IFSM<TState> inner)
        {
            _inner = inner;
        }

        public TState InitState
        {
            get
            {
                lock (_locker)
                {
                    return _inner.InitState;
                }
            }
        }

        public TState State
        {
            get
            {
                lock (_locker)
                {
                    return _inner.State;
                }
            }
        }

        public void Reset()
        {
            lock (_locker)
            {
                _inner.Reset();
            }
        }

        public bool SetState(TState nextState, StateChangingPredicate<TState>? onStateChanging = null)
        {
            lock (_locker)
            {
                return _inner.SetState(nextState, onStateChanging);
            }
        }
    }
}
