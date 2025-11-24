using System;

namespace Pontifex
{
    public abstract class InvariantChecker<TState>
        where TState : struct
    {
        private readonly Action<string> _onFail;

        private int _state;

        protected abstract TState ToState(int state);
        protected abstract int FromState(TState state);

        protected InvariantChecker(TState state, Action<string> onFail)
        {
            _onFail = onFail;
            // ReSharper disable once VirtualMemberCallInConstructor
            _state = FromState(state);
        }

        protected TState State => ToState(_state);

        protected TState SetState(TState newState)
        {
            int oldState = System.Threading.Interlocked.Exchange(ref _state, FromState(newState));
            return ToState(oldState);
        }

        protected void CheckState(TState expected)
        {
            var curState = _state;
            if (curState != FromState(expected))
            {
                StateFail(expected, ToState(curState));
            }
        }

        protected void ChangeState(TState expected, TState newState)
        {
            int oldState = System.Threading.Interlocked.Exchange(ref _state, FromState(newState));
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
            Fail($"required {expected}, but found {received}");
        }

        protected void Fail(string error = "")
        {
            string failText = $"{GetType()}[{ToString()}] invariants violation. msg = '{error}'";
            _onFail(failText);
        }
    }
}
