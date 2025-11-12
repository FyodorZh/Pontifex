using System;
using System.Collections.Generic;

namespace Shared.FSM
{
    public class FSM<TState, TStateValue>: IFSM<TState>, IFSM_Ctl<TState>
        where TStateValue : struct, IEquatable<TStateValue>
    {
        private class StateNode
        {
            public readonly TState State;
            public readonly TStateValue StateValue;
            public readonly List<StateNode> Transitions = new List<StateNode>();

            public StateNode(TState state, TStateValue stateValue)
            {
                State = state;
                StateValue = stateValue;
            }
        }

        private readonly Func<TState, TStateValue> mStateMapper;
        private readonly StateNode mFirstState;

        private readonly Dictionary<TStateValue, StateNode> mStates = new Dictionary<TStateValue, StateNode>();

        private StateNode mCurrentState;

        public FSM(TState firstState, Func<TState, TStateValue> stateMapper)
        {
            mStateMapper = stateMapper;

            var firstStateValue = stateMapper(firstState);

            mFirstState = new StateNode(firstState, firstStateValue);
            mStates.Add(firstStateValue, mFirstState);

            Reset();
        }

        public TState InitState
        {
            get { return mFirstState.State; }
        }

        public TState State
        {
            get { return mCurrentState.State; }
        }

        public bool AddTransition(TState fromState, TState toState)
        {
            TStateValue fromStateValue = mStateMapper(fromState);
            TStateValue toStateValue = mStateMapper(toState);
            StateNode fromNode;
            if (mStates.TryGetValue(fromStateValue, out fromNode))
            {
                for (int i = 0; i < fromNode.Transitions.Count; ++i)
                {
                    if (fromNode.Transitions[i].StateValue.Equals(toStateValue))
                    {
                        throw new InvalidOperationException(string.Format("Transition redefinition ({0} -> {1})", fromState, toState));
                    }
                }

                StateNode toNode;
                if (!mStates.TryGetValue(toStateValue, out toNode))
                {
                    toNode = new StateNode(toState, toStateValue);
                    mStates.Add(toStateValue, toNode);
                }

                fromNode.Transitions.Add(toNode);
                return true;
            }

            return false;
        }

        public bool AddTransitions(TState[] fromStates, TState toState)
        {
            bool res = true;
            foreach (var fromState in fromStates)
            {
                res = AddTransition(fromState, toState) && res;
            }

            return res;
        }

        public void Reset()
        {
            mCurrentState = mFirstState;
        }

        public void SetState(TState nextState, StateChangeReaction<TState> onStateChanged)
        {
            var nextStateValue = mStateMapper(nextState);

            var list = mCurrentState.Transitions;
            for (int i = 0; i < list.Count; ++i)
            {
                if (nextStateValue.Equals(list[i].StateValue))
                {
                    try
                    {
                        if (onStateChanged == null || onStateChanged(mCurrentState.State, nextState))
                        {
                            mCurrentState = list[i];
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }
                }
            }
        }
    }
}