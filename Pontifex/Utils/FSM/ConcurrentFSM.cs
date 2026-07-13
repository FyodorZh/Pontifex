using System;
using Actuarius.Collections;
using Actuarius.Concurrent;
using Actuarius.Memory;

namespace Pontifex.Utils.FSM
{
    public class ConcurrentFSM<TState> : IConcurrentFSM<TState>, IReleasableResource
        where TState : struct
    {
        private readonly struct ActionRec: ActionQueue<ActionRec>.IAction
        {
            private readonly ConcurrentFSM<TState> _owner;
            private readonly bool _reset;
            private readonly TState _stateToSet;
            private readonly StateChangeReaction<TState>? _onStateChanging;
            private readonly Action<TState>? _onStateChanged;

            public ActionRec(ConcurrentFSM<TState> owner, bool reset, TState state, 
                StateChangeReaction<TState>? onStateChanging, Action<TState>? onStateChanged)
            {
                _owner = owner;
                _reset = reset;
                _stateToSet = state;
                _onStateChanging = onStateChanging;
                _onStateChanged = onStateChanged;
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
                        _owner._core.SetState(_stateToSet, _onStateChanging, _onStateChanged);
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

        private readonly ActionQueue<ActionRec> _ticker = new ActionQueue<ActionRec>(new SystemConcurrentQueue<ActionRec>());

        public ConcurrentFSM(IFSM<TState> core)
        {
            _core = core;
            _initState = core.InitState;
            _curState.Value = core.State;
        }

        public TState InitState => _initState;

        public TState State => _curState.Value;

        public void Reset()
        {
            _ticker.Put(new ActionRec(this, true, default, null, null));
        }

        public void SetState(TState nextState, StateChangeReaction<TState>? onStateChanging, Action<TState>? onStateChanged)
        {
            _ticker.Put(new ActionRec(this, false, nextState, onStateChanging, onStateChanged));
        }

        public void Release()
        {
            _ticker.Release();
        }
    }
}