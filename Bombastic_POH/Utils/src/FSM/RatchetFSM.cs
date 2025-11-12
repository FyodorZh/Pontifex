namespace Shared.FSM
{
    /// <summary>
    /// Стейтмашина по типу храпового механизма. Позволяет только "увеличивать" стейт
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    public class RatchetFSM<TState> : IFSM<TState>
    {
        private readonly TState mInitState;
        private readonly System.Comparison<TState> mComparator;

        private TState mCurState;

        public RatchetFSM(System.Comparison<TState> comparator, TState initState)
        {
            mInitState = initState;
            mComparator = comparator;
            mCurState = initState;
        }

        public TState InitState
        {
            get { return mInitState; }
        }

        public TState State
        {
            get { return mCurState; }
        }

        public void Reset()
        {
            mCurState = mInitState;
        }

        public void SetState(TState nextState, StateChangeReaction<TState> onStateChanged = null)
        {
            int cmp = mComparator(mCurState, nextState);
            if (cmp < 0)
            {
                if (onStateChanged == null || onStateChanged(mCurState, nextState))
                {
                    mCurState = nextState;
                }
            }
        }
    }
}