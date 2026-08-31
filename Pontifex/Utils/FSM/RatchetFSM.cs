using System;

namespace Pontifex.Utils.FSM
{
    /// <summary>
    /// Стейтмашина по типу храпового механизма. Позволяет только "увеличивать" стейт
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    public class RatchetFSM<TState> : IFSM<TState>
    {
        private readonly TState _initState;
        private readonly Comparison<TState> _comparator;
        private readonly StateChangedReaction<TState>? _onStateChanged;

        private TState _curState;

        public RatchetFSM(Comparison<TState> comparator, TState initState, 
            StateChangedReaction<TState>? onStateChanged = null)
        {
            _initState = initState;
            _comparator = comparator;
            _onStateChanged = onStateChanged;
            _curState = initState;
        }

        public TState InitState => _initState;

        public TState State => _curState;

        public void Reset()
        {
            _curState = _initState;
        }

        public void SetState(TState nextState, StateChangingPredicate<TState>? onStateChanging = null)
        {
            int cmp = _comparator(_curState, nextState);
            if (cmp < 0)
            {
                var oldState = _curState;
                if (onStateChanging?.Invoke(oldState, nextState) ?? true)
                {
                    _curState = nextState;
                    _onStateChanged?.Invoke(oldState, nextState);
                }
            }
        }
    }
}