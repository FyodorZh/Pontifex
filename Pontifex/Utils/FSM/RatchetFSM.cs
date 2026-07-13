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

        private TState _curState;

        public RatchetFSM(Comparison<TState> comparator, TState initState)
        {
            _initState = initState;
            _comparator = comparator;
            _curState = initState;
        }

        public TState InitState => _initState;

        public TState State => _curState;

        public void Reset()
        {
            _curState = _initState;
        }

        public void SetState(TState nextState, StateChangeReaction<TState>? onStateChanging = null, Action<TState>? onStateChanged = null)
        {
            int cmp = _comparator(_curState, nextState);
            if (cmp < 0)
            {
                if (onStateChanging?.Invoke(_curState, nextState) ?? true)
                {
                    _curState = nextState;
                    onStateChanged?.Invoke(nextState);
                }
            }
        }
    }
}