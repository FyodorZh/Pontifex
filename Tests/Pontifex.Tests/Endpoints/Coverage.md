# RawEndpoint Layer Coverage

- **Layer:** RawEndpoint (root layer)
- **Subject:** `Pontifex.Raw.RawEndpoint`
- **Directory:** `Tests/Pontifex.Tests/Endpoints`
- **Inheritance parent:** — (root)
- **Contract:** [RawEndpoint.cs](../../../Pontifex/Implementations/Endpoints/Raw/RawEndpoint.cs),
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
| `Start_AfterStop_FailsHardWithoutSecondStopNotification` | A stopped endpoint cannot be restarted. | The invalid transition fails hard but does not raise a second handler stop notification. | The once-only handler latch makes `FailHard` notify from the starting, started, or stopping stage only; the stopped stage is silent. |
| `Start_StopWhileStarting_IsDeferredUntilFinishStart` | A stop requested while starting waits for `FinishStart`. | The stop is latched, then forwarded to `BeginStop` with `tryBeGentle: false` after the start completes. | The deferred path deliberately downgrades gentleness. |
| `FinishStart_WhenNotStarting_InvokesOnFailAndFailsHard` | `FinishStart` is legal only while starting. | The failure callback runs and the endpoint becomes terminal. | No handler stop notification because the endpoint never started. |
| `FinishStart_WhenAlreadyStarted_InvokesOnFailAndNotifiesStopped` | `FinishStart` after start is an invalid transition. | The failure callback runs and the started endpoint notifies `OnStopped(ExceptionFail)`. | Exercises the queue `onError` → `FailHard` path. |
| `Send_Null_ReturnsInvalidMessage` | `Send(null)` on a running endpoint is rejected as `InvalidMessage`. | Null is rejected without affecting lifecycle. | The endpoint is started first, because a stopped endpoint reports the state result. Buffer ownership does not transfer for `InvalidMessage`. |
| `Send_ReleasedBuffer_ReturnsInvalidMessageAndKeepsCallerOwnership` | `Send` rejects a non-null buffer that is no longer alive as `InvalidMessage`. | A released buffer is rejected and is not released a second time. | The caller retains ownership on `InvalidMessage`; the buffer is already dead, so a double release would corrupt the pool. |
| `Send_WhenNotStarted_ReturnsNotConnectedAndReleasesBuffer` | `Send` rejects with `NotConnected` before start and consumes the buffer. | Rejection releases the buffer exactly once. | Ownership transfers for every non-`InvalidMessage` result. |
| `Send_TooBig_ReturnsMessageTooBigAndReleasesBuffer` | `Send` rejects payloads above `MessageMaxByteSize` while running. | The size guard rejects and releases the buffer. | The endpoint is started first, because a stopped endpoint reports the state result. Buffer size is verified to exceed the limit before the call. |
| `Send_WhenStoppedWithNullBuffer_ReturnsNotConnected` | Result precedence: endpoint state before message validity. | A stopped endpoint reports `NotConnected` for a null buffer, not `InvalidMessage`. | The state check does not dereference the buffer. |
| `Send_WhenStoppedWithOversizedBuffer_ReturnsNotConnectedAndReleasesBuffer` | Result precedence: endpoint state before message size. | A stopped endpoint reports `NotConnected` for an oversized buffer, which is released exactly once. | `RefCount` reaches zero after the release. |
| `Send_WhenStoppedWithPendingBudget_ReturnsNotConnectedAndReleasesBuffer` | Result precedence: endpoint state before outbound capacity. | A stopped endpoint reports `NotConnected` while a previous admitted send still holds the whole byte budget, and the rejected buffer is released once. | The admitted action is drained afterwards to release its reservation. |
| `Send_WhenRunning_ReturnsOkAndTransmitsOnDrain` | A valid send is admitted and transmitted on the serial queue. | The admitted payload reaches `DoSendRaw` after drain. | The double captures a clone for inspection and releases the original. |
| `Send_MultipleBuffers_PreservesAdmissionOrder` | Concurrently admitted sends are transmitted in admission order. | Two accepted buffers reach `DoSendRaw` in call order. | Deterministic single-threaded queue; no real concurrency. |
| `Send_ExceedingCapacity_ReturnsBufferOverflow` | The outbound byte budget is bounded by `bufferCapacityBytes`. | An over-capacity send is rejected and its buffer released. | Budget accounting uses the serialized data size. |
| `Send_BudgetIsFreedAfterDrain` | Capacity is reclaimed after admitted work drains. | A rejected send succeeds after the first send is transmitted. | Proves the budget is released in `DoSendRaw`'s finally path. |
| `Send_WhenAdmitted_ReportsPositiveReservationDelta` | `OnPendingSendChanged` reports a reservation when bytes are reserved for a buffer. | An admitted send reports `+size` bytes and `+1` message before the buffer enters the queue. | The double keeps the signed deltas and their sums, so the sum is the endpoint's pending reservation; nothing is drained yet. |
| `Send_AfterDrain_ReportsNegativeDeltaAndNetsToZero` | Transmission releases the reservation with a symmetric negative delta. | The delta sequence is exactly `(+size, +1)` then `(-size, -1)`, and the sums are zero after the drain. | Proves the `_sendAction` `finally` releases the reservation on the transmit path. |
| `Send_WhenStopCompletesBeforeQueuedSend_DropsBufferAndReportsNegativeDelta` | A stop that lands before a queued send runs drops the buffer and releases its reservation. | The buffer is not transmitted, it is released, and the deltas net to zero. | The manual queue is FIFO, so the race is produced through the double's `AdmitWhileStopping` admission: the send passes `CanSendNow` as if before the stop latch, and its action lands behind the stop action. |
| `Send_WhenDoSendRawThrows_ReportsNegativeDeltaAndFailsHard` | A throwing `DoSendRaw` still releases the reservation and fails the endpoint hard. | Deltas net to zero, `OnStopped(ExceptionFail)` fires once, and the endpoint is not running. | The base contract leaves the buffer to the throwing implementation; the test asserts it is still alive and releases it. |
| `Send_WhenLocalBudgetOverflows_ReportsMatchingPositiveAndNegativeDeltas` | The local byte-budget rollback is symmetric. | A `BufferOverflow` rejection reports `(+size, +1)` then `(-size, -1)` and nets to zero. | The reservation is emitted before the capacity check, so a rejected admission is visible as a matching pair. |
| `Send_WhenQueueRejectsAdmission_ReportsMatchingDeltasAndReleasesBuffer` | A queue that rejects the admission rolls the reservation back. | `Send` returns `Error`, the deltas net to zero, and the buffer is released. | Uses the manual queue's opt-in reject-next seam; a rejection invokes neither the action nor `onFailToRun`, so the endpoint performs the rollback itself. |
| `Send_WhenPendingSendChangedOverrideThrows_IsSwallowedLoggedAndNetsToZero` | A throwing override cannot break buffer accounting. | The exception is logged, the endpoint keeps working, the buffer is transmitted, and the deltas net to zero. | Asserted through a recording `ILogConsumer`; the double throws after recording the delta, so the sums still prove the pairing. |
| `CanSendNow_WhenNotStarted_ReturnsNotConnected` | `CanSendNow` requires the started stage. | Before start it reports `NotConnected`. | — |
| `CanSendNow_WhenRunning_ReturnsOk` | A started endpoint with no stop request admits sends. | `CanSendNow` reports `Ok`. | — |
| `CanSendNow_AfterStopRequested_ReturnsNotConnected` | A pending stop closes the send gate. | `CanSendNow` reports `NotConnected` immediately after `Stop`. | The stop action may still be queued. |
| `ProcessInbound_WhenNotRunning_ReleasesWithoutCallback` | Inbound messages are dropped when not running. | No handler delivery and exactly one release. | — |
| `ProcessInbound_WhenRunning_DeliversToHandler` | A running endpoint delivers inbound messages. | The handler receives the payload content. | The recording handler clones and releases the original. |
| `ProcessInbound_ScheduledBeforeStopLatch_IsReleasedInsteadOfDelivered` | A receive job scheduled before the stop latch is released, not delivered, once the stop is requested. | The handler observes no `received`, the buffer is released, and only `stopped` follows. | The latch is checked again when the receive action runs; a receive that has not begun when `Stop` returns is dropped. |
| `ProcessInbound_AfterStopped_IsDropped` | A stopped endpoint accepts no further inbound messages. | No delivery and the buffer is released. | — |
| `ProcessInbound_WhenHandlerThrows_LogsAndLeavesEndpointUsable` | A throwing `OnReceived` is isolated. | The exception is logged, the endpoint keeps running, and later deliveries still reach the handler. | The handler records and releases the delivery before throwing, so ownership is respected. |
| `Stop_BeforeStart_TransitionsWithoutHandlerCallback` | `Stop` before start is terminal but does not notify the handler. | No `BeginStop` and no `OnStopped`; the endpoint is not running. | The public `Stopped` event is raised on this path but is not asserted here. |
| `Stop_WhenRunning_NotifiesHandlerWithReason` | A running stop calls `BeginStop` and notifies `OnStopped` with the same reason. | Reason identity and one-shot notification. | — |
| `Stop_ForwardsTryBeGentleToBeginStop` | `tryBeGentle` is forwarded to the transport hook. | The hook observes the caller's hint. | — |
| `Stop_CalledTwice_FirstReasonWins` | Repeated stops are idempotent. | Only the first reason and gentleness reach `BeginStop` and the handler. | — |
| `Stop_WhenFirstStoppedSubscriberThrows_StillInvokesLaterSubscribers` | `Stopped` subscriber failures are isolated per subscriber. | The throwing first subscriber is logged and the second subscriber still runs once. | The handler stop notification is unaffected. |
| `Stop_LatchedWhileStartingAndPromotedByFinishStart_QueuedStopActionIsNoOp` | The queued stop action is a no-op when teardown has already started. | `FinishStart` promotes a stop latched while starting to `BeginStop`; the queued stop action then does nothing, so there is no hard failure and exactly one `OnStopped`. | Needs the single-step drain so the start-completion action runs before the queued stop action. `FinishStart`/`FinishStop` out of order still fail hard (covered below). |
| `FinishStop_WhenNotStopping_FailsHard` | `FinishStop` is legal only while stopping. | An invalid completion fails hard with `ExceptionFail`. | Exercises the queue `onError` → `FailHard` path. |
| `FailHard_WhenRunning_NotifiesHandlerAndStops` | A fatal failure from the started stage notifies the handler and stops. | Reason identity and terminal `IsRunning == false`. | The direct protected hook is invoked; the queue error path is covered by the start/stop guard tests. |
| `FailHard_WhileStarting_NotifiesStoppedOnce` | A start-attempted endpoint notifies the handler when it fails hard while starting. | The handler receives exactly one `OnStopped` with the failure reason, and the endpoint is not running. | The stage is entered through the queue (`Start` then `DrainOne`) before the direct `FailHard` call. |
| `FailHard_WhileConstructed_DoesNotNotifyHandler` | A constructed endpoint has no start attempt, so a hard failure is silent. | No handler notification; the endpoint is not running. | `FailHard` still sets the terminal stage and raises the public `Stopped` event. |
| `FailHard_AfterFinishStop_DoesNotNotifyAgain` | The terminal notification is once-only across stop completion and hard failure. | After a completed stop, a subsequent `FailHard` adds no notification and the original stop reason is retained. | `CompleteStop` is not called directly from the started stage; `FinishStop` requires the stopping stage, so the test uses `StopAndComplete`. |
| `FailHard_WhileStopping_NotifiesStoppedOnce` | A hard failure during the stopping stage notifies the handler once. | One `OnStopped` with the fail reason. | `BeginStop` is recorded but not completed, so the endpoint is still stopping. |
| `FailHard_WhileStopped_DoesNotNotifyAgain` | A stop before start is terminal and later silent. | The handler is not notified by the stop or by the following `FailHard`. | Reuses the stop-before-start setup of `Stop_BeforeStart_TransitionsWithoutHandlerCallback`. |
| `GetControls_BaseImplementationAddsNothing` | The base control surface contributes no controls. | `GetControls` leaves the destination empty. | The conformance-control role is out of scope for this suite. |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| `Stopped` event raised once on every terminal path. | This layer | The public event is part of the endpoint contract; its subscriber-failure isolation is covered, but the one-shot raising on every terminal path is not asserted. |
| `IRawEndpoint`/`IBaseEndpoint` assignability. | This layer | Interface exposure is not covered. The `GetControls` predicate overload is covered by the `RawUnreliable` layer. |
| Inbound scheduling rejection releases the buffer. | This layer | The manual queue exposes an opt-in reject-next seam; the test is not written yet. |
| `OnStopped` throwing is swallowed while the terminal transition completes. | This layer | The handler-notification isolation path is not asserted for the endpoint's own handler. |
| `onFailToRun` accounting rollback when an accepted send is never executed. | This layer | Accepted-but-never-run actions are only produced by a released queue; the manual queue models no release barrier. |

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
  queue-error routing to `FailHard`. The queue can also be stepped one action at a time
  (`DrainOne`) and can reject the next submission on demand, so ordering- and admission-sensitive
  paths are deterministic.
- `TestRawEndpoint` records every `OnPendingSendChanged` delta together with its running sums, so a
  test can compare the delta sum with the endpoint's current pending outbound reservation.
- `BeginStart`/`BeginStop` are recorded by the double and completed explicitly with
  `FinishStart`/`FinishStop`; tests never sleep or use real time.
- The recording handler clones each delivered buffer for inspection, releases the original, and
  releases the clones on dispose. Captured outbound buffers are cloned by the endpoint double.
- The public `Stopped` event is raised independently of the handler callback; subscriber-failure
  isolation is covered, while the one-shot raising of the event on every terminal path remains a
  planned assertion.
- `ScheduleToSend` selects the result state-first: endpoint state, then buffer validity, then size,
  then capacity. A state rejection releases a live buffer and leaves a dead or null buffer to the
  caller.
- `FinishStop` and `FailHard` share one once-only handler latch, so the handler's stop notification
  is delivered at most once per endpoint. A stop completion that arrives after a hard failure is a
  no-op instead of an invalid-stage exception.
