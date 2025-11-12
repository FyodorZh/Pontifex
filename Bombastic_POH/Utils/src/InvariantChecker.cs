using System;

namespace Shared
{
    public abstract class InvariantChecker<TState>
        where TState : struct
    {
        private readonly Action<string> mOnFail;

        private int mState;

        protected abstract TState ToState(int state);
        protected abstract int FromState(TState state);

        protected InvariantChecker(TState state, Action<string> onFail)
        {
            mOnFail = onFail;
            // ReSharper disable once VirtualMemberCallInConstructor
            mState = FromState(state);
        }

        protected TState State
        {
            get { return ToState(mState); }
        }

        protected TState SetState(TState newState)
        {
            int oldState = System.Threading.Interlocked.Exchange(ref mState, FromState(newState));
            return ToState(oldState);
        }

        protected void CheckState(TState expected)
        {
            var curState = mState;
            if (curState != FromState(expected))
            {
                StateFail(expected, ToState(curState));
            }
        }

        protected void ChangeState(TState expected, TState newState)
        {
            int oldState = System.Threading.Interlocked.Exchange(ref mState, FromState(newState));
            if (oldState != FromState(expected))
            {
                StateFail(expected, ToState(oldState));
            }
        }

        protected void BeginCriticalSection(ref int sectionId)
        {
            int depth = System.Threading.Interlocked.Increment(ref sectionId);
            if (depth != 1)
            {
                Fail();
            }
        }

        protected void EndCriticalSection(ref int sectionId)
        {
            int depth = System.Threading.Interlocked.Decrement(ref sectionId);
            if (depth != 0)
            {
                Fail("wrong nesting");
            }
        }

        protected void StateFail(TState expected, TState received)
        {
            Fail(string.Format("required {0}, but found {1}", expected, received));
        }

        protected void Fail()
        {
            Fail("");
        }

        protected void Fail(string error)
        {
            if (mOnFail != null)
            {
                string failText = string.Format("{0}[{1}] invariants violation. msg = '{2}'", GetType(), ToString(), error);
                mOnFail(failText);
            }
        }
    }
}
