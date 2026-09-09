using Actuarius.Collections;
using Actuarius.Concurrent;
using Actuarius.Memory;
using Scriba;

namespace Pontifex.Utils.FSM
{
    public class AsyncFSM<TState> : IConcurrentFSM<TState>, IReleasableResource
        where TState : struct
    {
        private readonly struct ActionRec: ICommandBearer
        {
            private readonly AsyncFSM<TState> _owner;
            private readonly bool _reset;
            private readonly TState _stateToSet;
            private readonly StateChangingPredicate<TState>? _onStateChanging;

            public ActionRec(AsyncFSM<TState> owner, bool reset, TState state, 
                StateChangingPredicate<TState>? onStateChanging)
            {
                _owner = owner;
                _reset = reset;
                _stateToSet = state;
                _onStateChanging = onStateChanging;
            }

            public void Invoke()
            {
                try
                {
                    if (_reset)
                    {
                        _owner._core.Reset();
                    }
                    else
                    {
                        _owner._core.SetState(_stateToSet, _onStateChanging);
                    }
                }
                finally
                {
                    _owner._curState.Value = _owner._core.State;    
                }
            }

            public void Fail()
            {
                // DO NOTHING
            }
        }

        private readonly IFSM<TState> _core;
        private readonly TState _initState;

        private readonly AtomicBox<TState> _curState = new AtomicBox<TState>();

        private readonly SerialCommandQueue<ActionRec> _ticker = new SerialCommandQueue<ActionRec>(
            new SystemConcurrentQueue<ActionRec?>(), Log.wtf);

        public AsyncFSM(IFSM<TState> core)
        {
            _core = core;
            _initState = core.InitState;
            _curState.Value = core.State;
        }

        public TState InitState => _initState;

        public TState State => _curState.Value;

        public void Reset()
        {
            _ticker.Schedule(new ActionRec(this, true, default, null));
        }

        public void SetState(TState nextState, StateChangingPredicate<TState>? onStateChanging = null)
        {
            _ticker.Schedule(new ActionRec(this, false, nextState, onStateChanging));
        }

        public void Release()
        {
            _ticker.Release();
        }
    }
}