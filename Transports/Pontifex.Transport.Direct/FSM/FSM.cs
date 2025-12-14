using System;
using System.Collections.Generic;
using Scriba;

namespace Pontifex.Utils.FSM
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
            mCurrentState = mFirstState;
        }

        public TState InitState => mFirstState.State;

        public TState State => mCurrentState.State;

        public bool AddTransition(TState fromState, TState toState)
        {
            TStateValue fromStateValue = mStateMapper(fromState);
            TStateValue toStateValue = mStateMapper(toState);
            if (mStates.TryGetValue(fromStateValue, out var fromNode))
            {
                foreach (var element in fromNode.Transitions)
                {
                    if (element.StateValue.Equals(toStateValue))
                    {
                        throw new InvalidOperationException($"Transition redefinition ({fromState} -> {toState})");
                    }
                }

                if (!mStates.TryGetValue(toStateValue, out var toNode))
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

        public void SetState(TState nextState, StateChangeReaction<TState>? onStateChanged)
        {
            var nextStateValue = mStateMapper(nextState);

            var list = mCurrentState.Transitions;
            foreach (var element in list)
            {
                if (nextStateValue.Equals(element.StateValue))
                {
                    try
                    {
                        if (onStateChanged == null || onStateChanged(mCurrentState.State, nextState))
                        {
                            mCurrentState = element;
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