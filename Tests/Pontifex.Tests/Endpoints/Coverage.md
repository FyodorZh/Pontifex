# RawEndpoint Layer Coverage

- **Layer:** RawEndpoint (root layer)
- **Subject:** `Pontifex.Raw.RawEndpoint`
- **Directory:** `Tests/Pontifex.Tests/Endpoints`
- **Inheritance parent:** — (root)
- **Contract:** [RawEndpoint.cs](../../../Pontifex/Implementations/Raw/RawEndpoint.cs),
  [IRawEndpoint.cs](../../../Pontifex/Abstractions/Transports/Raw/IRawEndpoint.cs),
  [IBaseEndpoint.cs](../../../Pontifex/Abstractions/Transports/IBaseEndpoint.cs),
  [IRawHandler.cs](../../../Pontifex/Abstractions/Transports/Raw/IRawHandler.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Constructor_WithNonPositiveMessageMaxByteSize_Throws` | Constructor requires `messageMaxByteSize >= 1`. | Invalid message limit is rejected before the endpoint exists. | Buffer capacity is valid in this test; the message-limit guard is the one exercised. |
| `Constructor_WithNonPositiveBufferCapacity_Throws` | Constructor requires `bufferCapacityBytes >= 1`. | Invalid byte budget is rejected. | Buffer capacity is validated before the message limit. |
| `Constructor_CapturesRemoteEndPointAndMessageMaxByteSize` | Constructor captures the remote endpoint and message limit. | The two public observables expose the supplied values. | Identity assertion for the endpoint object; value assertion for the size. |
| `Constructor_InitiallyNotRunning` | A constructed endpoint is not running. | Lifecycle starts in the constructed stage. | — |
| `Start_WhenConstructed_AfterCompletionIsRunning` | `Start` reaches the started stage after `FinishStart`. | Happy-path start exposes `IsRunning == true` and performs one `BeginStart`. | Completion is explicit; the double only records `BeginStart`. |
| `Start_WhileStarting_IsNotRunningUntilCompleted` | `IsRunning` is false until the start transition completes. | The observable only flips after `FinishStart`. | — |
| `Start_CalledTwice_FailsHard` | A second `Start` is an invalid transition. | The queued action throws, the queue routes it to `onError`, and the endpoint fails hard with `ExceptionFail`. | `Start()` returns `true` because admission is queued; the failure is observed asynchronously on drain. |
| `Start_AfterStop_FailsHardWithoutSecondStopNotification` | A stopped endpoint cannot be restarted. | The invalid transition fails hard but does not raise a second handler stop notification. | `FailHard` notifies the handler only from the started stage. |
| `Start_StopWhileStarting_IsDeferredUntilFinishStart` | A stop requested while starting waits for `FinishStart`. | The stop is latched, then forwarded to `BeginStop` with `tryBeGentle: false` after the start completes. | The deferred path deliberately downgrades gentleness. |
| `FinishStart_WhenNotStarting_InvokesOnFailAndFailsHard` | `FinishStart` is legal only while starting. | The failure callback runs and the endpoint becomes terminal. | No handler stop notification because the endpoint never started. |
| `FinishStart_WhenAlreadyStarted_InvokesOnFailAndNotifiesStopped` | `FinishStart` after start is an invalid transition. | The failure callback runs and the started endpoint notifies `OnStopped(ExceptionFail)`. | Exercises the queue `onError` → `FailHard` path. |
| `Send_Null_ReturnsInvalidMessage` | `Send(null)` is rejected as `InvalidMessage`. | Null is rejected without affecting lifecycle. | Buffer ownership does not transfer for `InvalidMessage`. |
| `Send_WhenNotStarted_ReturnsNotConnectedAndReleasesBuffer` | `Send` rejects with `NotConnected` before start and consumes the buffer. | Rejection releases the buffer exactly once. | Ownership transfers for every non-`InvalidMessage` result. |
| `Send_TooBig_ReturnsMessageTooBigAndReleasesBuffer` | `Send` rejects payloads above `MessageMaxByteSize`. | The size guard rejects and releases the buffer. | Buffer size is verified to exceed the limit before the call. |
| `Send_WhenRunning_ReturnsOkAndTransmitsOnDrain` | A valid send is admitted and transmitted on the serial queue. | The admitted payload reaches `DoSendRaw` after drain. | The double captures a clone for inspection and releases the original. |
| `Send_MultipleBuffers_PreservesAdmissionOrder` | Concurrently admitted sends are transmitted in admission order. | Two accepted buffers reach `DoSendRaw` in call order. | Deterministic single-threaded queue; no real concurrency. |
| `Send_ExceedingCapacity_ReturnsBufferOverflow` | The outbound byte budget is bounded by `bufferCapacityBytes`. | An over-capacity send is rejected and its buffer released. | Budget accounting uses the serialized data size. |
| `Send_BudgetIsFreedAfterDrain` | Capacity is reclaimed after admitted work drains. | A rejected send succeeds after the first send is transmitted. | Proves the budget is released in `DoSendRaw`'s finally path. |
| `CanSendNow_WhenNotStarted_ReturnsNotConnected` | `CanSendNow` requires the started stage. | Before start it reports `NotConnected`. | — |
| `CanSendNow_WhenRunning_ReturnsOk` | A started endpoint with no stop request admits sends. | `CanSendNow` reports `Ok`. | — |
| `CanSendNow_AfterStopRequested_ReturnsNotConnected` | A pending stop closes the send gate. | `CanSendNow` reports `NotConnected` immediately after `Stop`. | The stop action may still be queued. |
| `ProcessInbound_WhenNotRunning_ReleasesWithoutCallback` | Inbound messages are dropped when not running. | No handler delivery and exactly one release. | — |
| `ProcessInbound_WhenRunning_DeliversToHandler` | A running endpoint delivers inbound messages. | The handler receives the payload content. | The recording handler clones and releases the original. |
| `ProcessInbound_IsDeliveredBeforeStopNotification` | Delivery is serialized before the stop notification. | The handler observes `received` before `stopped`. | Ordering is asserted through the handler event sequence. |
| `ProcessInbound_AfterStopped_IsDropped` | A stopped endpoint accepts no further inbound messages. | No delivery and the buffer is released. | — |
| `Stop_BeforeStart_TransitionsWithoutHandlerCallback` | `Stop` before start is terminal but does not notify the handler. | No `BeginStop` and no `OnStopped`; the endpoint is not running. | The public `Stopped` event is raised on this path but is not asserted here. |
| `Stop_WhenRunning_NotifiesHandlerWithReason` | A running stop calls `BeginStop` and notifies `OnStopped` with the same reason. | Reason identity and one-shot notification. | — |
| `Stop_ForwardsTryBeGentleToBeginStop` | `tryBeGentle` is forwarded to the transport hook. | The hook observes the caller's hint. | — |
| `Stop_CalledTwice_FirstReasonWins` | Repeated stops are idempotent. | Only the first reason and gentleness reach `BeginStop` and the handler. | — |
| `FinishStop_WhenNotStopping_FailsHard` | `FinishStop` is legal only while stopping. | An invalid completion fails hard with `ExceptionFail`. | Exercises the queue `onError` → `FailHard` path. |
| `FailHard_WhenRunning_NotifiesHandlerAndStops` | A fatal failure from the started stage notifies the handler and stops. | Reason identity and terminal `IsRunning == false`. | The direct protected hook is invoked; the queue error path is covered by the start/stop guard tests. |
| `GetControls_BaseImplementationAddsNothing` | The base control surface contributes no controls. | `GetControls` leaves the destination empty. | The conformance-control role is out of scope for this suite. |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| `Stopped` event raised once on every terminal path and subscriber exceptions isolated. | This layer | The public event is part of the endpoint contract but is not currently asserted. |
| `IRawEndpoint`/`IBaseEndpoint` assignability and `GetControls` predicate overload. | This layer | Interface exposure and the predicate path are not covered. |
| Inbound scheduling rejection releases the buffer. | This layer | The manual queue always accepts; a reject-next seam is needed. |
| `OnReceived` throwing fails hard with `ExceptionFail` while releasing ownership. | This layer | Handler exception isolation is a documented failure path. |
| `OnStopped` throwing and `Stopped` subscriber throwing are swallowed. | This layer | Callback exception isolation is a documented contract. |
| `DoSendRaw` throwing routes to `FailHard`. | This layer | Transport hook exception path is not covered. |

## Deferred by design

| Gap | Reason |
|---|---|
| Conformance controls (`GetControls` conformance role, gates, failure injection). | The conformance subsystem is out of scope and may be rewritten. |
| Thread-safety, concurrent sends, and stress behaviour. | The default suite is deterministic; concurrency belongs to a separate categorised layer or the conformance suite. |
| Real transports, sockets, networks, timers, and wall-clock time. | The endpoint contract is tested without a carrier; those facets belong to transport/integration suites. |
| Concrete endpoint subclasses. | Covered by the derived layers of this Abstract Test Suit. |

## Notes

- Every transition is serialized through `ManualSerialActionQueue`, which models the production
  queue: one action at a time, actions scheduled during a drain run on a later iteration, and
  queue-error routing to `FailHard`.
- `BeginStart`/`BeginStop` are recorded by the double and completed explicitly with
  `FinishStart`/`FinishStop`; tests never sleep or use real time.
- The recording handler clones each delivered buffer for inspection, releases the original, and
  releases the clones on dispose. Captured outbound buffers are cloned by the endpoint double.
- The public `Stopped` event is raised independently of the handler callback; it is currently a
  planned gap rather than covered behaviour.
