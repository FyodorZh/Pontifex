# FSM Coverage

| Field | Value |
| --- | --- |
| Layer | Root |
| Subject | State-machine family around `IFSMBase<TState>` |
| Directory | `Tests/Pontifex.Tests/FSM/` |
| Inheritance parent | none |
| Dependency | All subjects are public production types; the suite needs no test doubles or internal access. |
| Contract | [IFSM.cs](../../../Pontifex/Utils/FSM/IFSM.cs), [FSM.cs](../../../Pontifex/Utils/FSM/Implementations/FSM.cs) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `InitialState` (`FSM`) | `IFSMBase.InitState`/`State` | Both start at the constructor state | No transition is applied at construction. |
| `ValidTransition_ChangesState` | `IFSM.SetState` | A declared transition moves `State` to the target | The graph is built with `AddTransition`. |
| `InvalidTransition_IsIgnored` | Transition graph | An undeclared transition leaves the state unchanged | `SetState` reports failure and no callback runs. |
| `SetState_ChainsMultipleTransitions` | Transition graph | Declared transitions apply one after another | Each call sees the state left by the previous call. |
| `Reset_ReturnsToInitState` | `IFSMBase.Reset` | `Reset` restores the constructor state | `InitState` is unchanged by the reset. |
| `InitState_DoesNotChange` | `IFSMBase.InitState` | `InitState` stays the constructor state after transitions | The default state is immutable for the instance lifetime. |
| `SetState_onStateChanging_AllowsTransition` | Change predicate | A predicate returning true lets the transition run | The predicate runs before the state changes. |
| `SetState_onStateChanging_VetoesTransition` | Change predicate | A predicate returning false aborts the transition | The state is left unchanged. |
| `SetState_onStateChanging_ReceivesCorrectStates` | Change predicate | The predicate receives the current and target states | Ordering of the arguments is part of the contract. |
| `Constructor_onStateChanged_CalledAfterTransition` | State-changed reaction | The constructor reaction runs after an applied transition | It observes the new state. |
| `Constructor_onStateChanged_NotCalled_WhenVetoed` | State-changed reaction | A veto suppresses the reaction entirely | No partial notification is emitted. |
| `SetState_InvalidTransition_DoesNotCallCallbacks` | Callback isolation | Neither predicate nor reaction runs for an undeclared transition | Invalid input is rejected before callbacks. |
| `AddTransition_FromUnknownState_ReturnsFalse` | Graph construction | A transition from an undeclared state is refused | `AddTransition` reports failure instead of throwing. |
| `AddTransition_Duplicate_ThrowsInvalidOperationException` | Graph construction | Adding the same edge twice throws | Duplicate edges are a programming error, not a silent no-op. |
| `AddTransitions_AllSucceed` | Bulk graph construction | A batch of valid edges is added | Bulk add reports success only when every edge is accepted. |
| `AddTransitions_PartialFailure_ReturnsFalse` | Bulk graph construction | A batch containing an invalid edge reports failure | Callers treat any failure as an incomplete graph. |
| `SelfTransition_WhenAllowed_ChangesToSelf` | Self edges | A declared self edge is a valid transition | The reaction runs for the self move. |
| `StateMapper_NonUniqueValue_TreatedAsSharedNode` | State mapping | Two states mapping to one value share a graph node | The mapper, not state equality, identifies nodes. |
| `SetState_NullCallbacks_DoesNotThrow` | Optional callbacks | A transition without a predicate or reaction succeeds | Callback parameters are optional. |
| `InitialState` (`AsyncFSM`) | `IFSMBase` passthrough | The wrapper reports the inner `InitState`/`State` | The async wrapper delegates reads to the inner FSM. |
| `SetState_ValidTransition_EventuallyChangesState` | `IConcurrentFSM.SetState` | A queued transition is applied on the worker | Assertions spin with a bounded timeout; no fixed sleep. |
| `SetState_MultipleCalls_ProcessedInOrder` | Queue ordering | Queued transitions apply in call order | Ordering is the wrapper's core guarantee. |
| `Reset_EventuallyReturnsToInitState` | `IFSMBase.Reset` | A queued reset restores the inner state | Reset travels through the same queue. |
| `InvalidTransition_IsIgnored` (`AsyncFSM`) | Queue ordering | An invalid queued transition leaves the state unchanged | Later transitions still run. |
| `SetState_Calls_onStateChanging` | Change predicate | The wrapper forwards the predicate and honours a veto | Veto semantics are preserved through the queue. |
| `SetState_Calls_onStateChanged` | State-changed reaction | The wrapper forwards the reaction | The reaction runs on the worker thread. |
| `SetState_Veto_QueuedCorrectly` | Queue ordering | A vetoed transition does not block later queued entries | Each entry is evaluated independently. |
| `Release_DoesNotThrow` | `IReleasableResource` | Releasing the wrapper is safe | Release is idempotent. |
| `Release_StopsProcessing` | `IReleasableResource` | After release no further queued transition is applied | The wrapper is terminal after release. |
| `WrapsInnerFsm_Correctly` (`AsyncFSM`) | Passthrough | Reads and transitions reflect the inner FSM | The wrapper owns the queue, not the graph. |
| `WrapsInnerFsm_Correctly` (`SerializedFSM`) | Passthrough | Reads and transitions reflect the inner FSM | The wrapper serializes callbacks around the inner FSM. |
| `InitState_IsForwarded` | Passthrough | `InitState` comes from the inner FSM | No separate default state exists. |
| `Reset_IsForwarded` | `IFSMBase.Reset` | Reset reaches the inner FSM | Wrapping does not swallow reset. |
| `SetState_onStateChanging_IsForwarded` | Change predicate | The predicate reaches the inner FSM | Wrapping preserves veto behavior. |
| `ConcurrentSetState_IsSerialized` | Callback serialization | Concurrent calls never overlap inside the inner FSM | The wrapper's lock is the serialization point. |
| `InitialState` (`RatchetFSM`) | `IFSMBase` | The ratchet starts at its constructor state | The comparator does not move the state at construction. |
| `ForwardMove_ChangesState` | Direction rule | A move in the allowed direction is applied | Direction is defined by the comparator. |
| `BackwardMove_IsIgnored` | Direction rule | A move against the comparator is refused | The state stays at the higher value. |
| `SameState_IsIgnored` | Direction rule | A move to the current state is refused | The ratchet moves strictly forward. |
| `Reset_ReturnsToInitState` | `IFSMBase.Reset` | Reset returns to the original state | Reset is the only backward path. |
| `ChainForwardIncrements` | Direction rule | Successive forward moves apply in sequence | Each move compares against the current state. |
| `BackwardThenForward_BackwardIgnoredForwardWorks` | Direction rule | A refused move does not poison later moves | The ratchet state is unchanged by the refusal. |
| `SetState_onStateChanging_AllowsForwardMove` | Change predicate | A predicate can veto or allow a forward move | The predicate runs only for candidate moves. |
| `SetState_onStateChanging_VetoesForwardMove` | Change predicate | A veto stops an otherwise valid forward move | The state stays unchanged. |
| `SetState_onStateChanging_NotCalledOnBackwardMove` | Direction rule | The predicate does not run for a refused direction | Direction is checked before the predicate. |
| `Constructor_onStateChanged_CalledOnForwardMove` | State-changed reaction | The reaction runs for an applied forward move | The reaction sees the new state. |
| `Constructor_onStateChanged_NotCalledOnBackwardMove` | State-changed reaction | A refused move emits no reaction | Refusal is silent. |
| `Constructor_onStateChanged_NotCalledWhenVetoed` | State-changed reaction | A vetoed move emits no reaction | Predicate failure precedes notification. |
| `SetState_NegativeComparator_ReversesDirection` | Comparator contract | A reversed comparator makes the ratchet move downward | Direction is entirely comparator-defined. |
| `SetState_NullCallbacks_DoesNotThrow` | Optional callbacks | A ratchet move without callbacks succeeds | Callback parameters are optional. |

## Planned

| Missing test | Owner layer |
| --- | --- |
| Direct `IFSM_Ctl<TState>` contract usage outside `FSM` | Root — deferred until another implementation exposes the control side. |
| Api state-machine composition (`ApiRoot`, pipe systems) | The Api suites, which exercise the FSM only as an implementation detail. |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Concurrency stress beyond serialized callbacks | The wrapper contract is callback serialization, not throughput; the asserted invariant is no overlap. |
| Real-time latency of `AsyncFSM` delivery | The wrapper asserts ordering and eventual application, not timing accuracy. |
| Allocation and performance characteristics | Out of scope for a contract suite. |
| Interface-only tests | `IFSMBase`, `IFSM`, and `IConcurrentFSM` have no behavior without an implementation; each concrete type proves the shared contract. |
