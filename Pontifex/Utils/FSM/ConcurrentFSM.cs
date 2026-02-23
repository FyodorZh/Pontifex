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
            private readonly ConcurrentFSM<TState> mOwner;
            private readonly bool mReset;
            private readonly TState mStateToSet;
            private readonly StateChangeReaction<TState>? mOnStateChanged;

            public ActionRec(ConcurrentFSM<TState> owner, bool reset, TState state, StateChangeReaction<TState>? onStateChanged)
            {
                mOwner = owner;
                mReset = reset;
                mStateToSet = state;
                mOnStateChanged = onStateChanged;
            }

            public void Invoke()
            {
                try
                {
                    if (mReset)
                    {
                        mOwner.mCore.Reset();
                    }
                    else
                    {
                        mOwner.mCore.SetState(mStateToSet, mOnStateChanged);
                    }
                }
                finally
                {
                    mOwner.mCurState.Value = mOwner.mCore.State;    
                }
            }

            public void Fail()
            {
                // DO NOTHING
            }
        }

        private readonly IFSM<TState> mCore;
        private readonly TState mInitState;

        private readonly AtomicBox<TState> mCurState = new AtomicBox<TState>();

        private readonly ActionQueue<ActionRec> mTicker = new ActionQueue<ActionRec>(new TinyConcurrentQueue<ActionRec>());

        public ConcurrentFSM(IFSM<TState> core)
        {
            mCore = core;
            mInitState = core.InitState;
            mCurState.Value = core.State;
        }

        public TState InitState
        {
            get { return mInitState; }
        }

        public TState State => mCurState.Value;

        public void Reset()
        {
            mTicker.Put(new ActionRec(this, true, default, null));
        }

        public void SetState(TState nextState, StateChangeReaction<TState>? onStateChanged)
        {
            mTicker.Put(new ActionRec(this, false, nextState, onStateChanged));
        }

        public void Release()
        {
            mTicker.Release();
        }
    }
}