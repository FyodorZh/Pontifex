using System;
using System.Collections.Generic;

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

        private readonly Func<TState, TStateValue> _stateMapper;
        private readonly StateNode _firstState;

        private readonly Dictionary<TStateValue, StateNode> _states = new Dictionary<TStateValue, StateNode>();

        private StateNode _currentState;

        public FSM(TState firstState, Func<TState, TStateValue> stateMapper)
        {
            _stateMapper = stateMapper;

            var firstStateValue = stateMapper(firstState);

            _firstState = new StateNode(firstState, firstStateValue);
            _states.Add(firstStateValue, _firstState);
            _currentState = _firstState;
        }

        public TState InitState => _firstState.State;

        public TState State => _currentState.State;

        public bool AddTransition(TState fromState, TState toState)
        {
            TStateValue fromStateValue = _stateMapper(fromState);
            TStateValue toStateValue = _stateMapper(toState);
            if (_states.TryGetValue(fromStateValue, out var fromNode))
            {
                foreach (var element in fromNode.Transitions)
                {
                    if (element.StateValue.Equals(toStateValue))
                    {
                        throw new InvalidOperationException($"Transition redefinition ({fromState} -> {toState})");
                    }
                }

                if (!_states.TryGetValue(toStateValue, out var toNode))
                {
                    toNode = new StateNode(toState, toStateValue);
                    _states.Add(toStateValue, toNode);
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
            _currentState = _firstState;
        }

        public void SetState(TState nextState, StateChangeReaction<TState>? onStateChanging, Action<TState>? onStateChanged)
        {
            var nextStateValue = _stateMapper(nextState);

            var list = _currentState.Transitions;
            foreach (var element in list)
            {
                if (nextStateValue.Equals(element.StateValue))
                {
                    if (onStateChanging?.Invoke(_currentState.State, nextState) ?? true)
                    {
                        _currentState = element;
                        onStateChanged?.Invoke(nextState);
                    }
                }
            }
        }
    }
}