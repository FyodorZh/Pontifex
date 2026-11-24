# RawUnreliableEndpoint Layer Coverage

- **Layer:** RawUnreliableEndpoint
- **Subject:** `Pontifex.Raw.Unreliable.RawUnreliableEndpoint`
- **Directory:** `Tests/Pontifex.Tests/Endpoints/RawUnreliable`
- **Inheritance parent:** `Pontifex.Raw.RawEndpoint`
- **Contract:** [RawUnreliableEndpoint.cs](../../../../Pontifex/Implementations/Endpoints/Raw/Unreliable/RawUnreliableEndpoint.cs),
  [IRawUnreliableEndpointImp.cs](../../../../Pontifex/Implementations/Endpoints/Raw/Unreliable/IRawUnreliableEndpointImp.cs),
  [IRawUnreliableEndpoint.cs](../../../../Pontifex/Abstractions/Transports/Raw/Unreliable/IRawUnreliableEndpoint.cs),
  [IRawUnreliableHandler.cs](../../../../Pontifex/Abstractions/Transports/Raw/Unreliable/IRawUnreliableHandler.cs),
  [RawUnreliableEndpointConformanceControl.cs](../../../../Pontifex/Implementations/Raw/Unreliable/RawUnreliableEndpointConformanceControl.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Constructor_WithNegativeInboxMessageCapacity_Throws` | The inbox message cap must not be negative. | A negative message cap is rejected before the endpoint exists. | The base limits are valid in this test; only the inbox guard is exercised. |
| `Constructor_WithNegativeInboxByteCapacity_Throws` | The inbox byte cap must not be negative. | A negative byte cap is rejected. | Validation runs after the base constructor, so the base endpoint is constructed first. |
| `Deliver_BeforeStart_BuffersWithoutDelivery` | `Deliver` buffers before the flush action runs. | Nothing reaches the handler, the start notification has not run, and the endpoint owns the buffer. | Ownership is shown by the buffer staying alive while the endpoint holds it. |
| `Deliver_BeforeStart_DeliversBufferedMessagesInFifoOrderAfterStart` | The flush raises the start notification before it drains the inbox in FIFO order. | Three buffered payloads reach the handler in delivery order, every delivery is observed after `OnStarted` completed, and no delivery action is pending inside `OnStarted`. | Delivery itself is scheduled by the base endpoint, so the drain must be followed by the queue drain. Two independent observables pin the ordering: the handler counts `OnReceived` before and after the start notification completed, and an `OnStarted` probe reads the endpoint's pending queue depth (0 when the notification runs first, 3 if the drain ran first). |
| `Deliver_AfterFlush_DeliversWithoutBuffering` | An open inbox schedules the delivery directly. | A message delivered after the start reaches the handler and is released. | — |
| `Deliver_WhenOnReceivedThrows_KeepsEndpointRunningAndDeliversLaterMessages` | An `OnReceived` exception abandons only that delivery (`Specification.md` §11). | The exception is isolated by the handler gate, the endpoint stays running, and a later message is still delivered. | Without the gate the inherited raw endpoint would treat the exception as an unhandled queue error and fail the endpoint hard. The throwing handler releases its buffer in `finally`, so ownership is not leaked. |
| `Deliver_BetweenFinishStartAndFlush_StaysBehindBufferedMessages` | `IsRunning` is not the dispatch key: an open inbox is the flush, not the start completion. | A message delivered after the finish-start action but before the flush stays behind the already buffered message. | The test steps the manual queue twice: the start action then the finish-start action. The endpoint already reports `IsRunning`, yet nothing has been delivered. |
| `Deliver_WhenMessageCapReached_DropsSilentlyAndReleases` | The inbox message cap bounds the buffered messages. | Excess messages are released, nothing is logged, and only the admitted messages are delivered. | Silence is asserted through a recording `ILogConsumer`; the endpoint must emit no log entry for a dropped message. |
| `Deliver_WhenByteCapReached_DropsSilentlyAndReleases` | The inbox byte cap bounds the buffered payload bytes. | The first message fills the byte budget, the second is released, and nothing is logged. | The cap is set to the exact size of one message, read from `GetDataSize` to avoid hard-coded sizes. |
| `Deliver_AfterStop_ReleasesWithoutDelivery` | A closed inbox releases every delivered message. | After a completed stop the handler receives nothing and the buffer is released. | The state check happens under the inbox lock, so a delivery racing the stop cannot slip past it. |
| `Start_RaisesOnStartedExactlyOnce` | The flush action raises `IRawUnreliableHandler.OnStarted` once. | The start notification count stays one across the start and a later stop. | The notification is raised by the endpoint, not by the transport. |
| `Start_WhenOnStartedThrows_FailsHardWithoutHandlerStopAndReleasesInbox` | A throwing start notification fails the endpoint hard and suppresses the handler stop notification. | `OnStopped` is not forwarded, the buffered message is released, and the terminal `Stopped` event fires once with `ExceptionFail`. | The exception escapes the flush action to the queue error path (`Log.wtf` + `ExceptionFail` + `FailHard`). The inbox is released by the `Stopped` subscription, not by the flush. |
| `Stop_BeforeStart_ReleasesBufferedMessagesWithoutDelivery` | Stopping a never-started endpoint is terminal and releases the inbox. | Two buffered messages are released, no lifecycle notification reaches the handler, and `Stopped` fires once. | The one-shot stop has no `IsRunning` guard, so this path is reachable by design. |
| `Stop_AfterStart_NotifiesHandlerOnceAndRaisesStoppedOnce` | A running stop forwards the handler stop notification exactly once. | The handler and the `Stopped` event observe the same reason instance once each, and the endpoint becomes invalid. | The handler gate is open because the start notification completed. |
| `Stop_WhileStarting_SuppressesOnStartedAndOnStopped` | A stop promoted while starting never raises the start notification, so no stop notification may follow. | Neither `OnStarted` nor `OnStopped` reaches the handler, `Stopped` fires once with the latch reason and no hard failure, the endpoint is terminal, and the handler-stopped gate is not hit. | Driven by one queue step: the start action ran, the stop was latched before the finish-start action, and the flush action then observed the invalidated endpoint. The handler-stopped gate now sits in the forwarding path of the handler notification, so a suppressed notification does not hit it; the test asserts the untouched quota. |
| `Stop_FirstCallReturnsTrue_SubsequentCallsReturnFalse` | `IRawUnreliableEndpoint.Stop` is a one-shot request. | The first call returns `true` and turns `IsValid` off immediately; later calls return `false`. | The call is made through the explicit interface implementation, so the base `Stop(StopReason, bool)` overload stays reachable. The transition may be executed inline by the queue. |
| `Stop_NullReason_MapsToUnknownFromTransportName` | A null stop reason is replaced before the base stop latches it. | The handler and the `Stopped` event observe an `Unknown` reason whose source is the transport name. | The mapping is mandatory: the base latches the reason as given and later delivers it unchanged. |
| `IsValid_TracksIsRunning` | `IsValid` is defined as `IsRunning`. | The two observables agree before start, while running, and after a stop request. | The flag turns over at the stop latch, before the transition completes. |
| `Send_WhenNotStarted_MapsNotConnectedToError` | The unreliable contract never reports a missing connection. | The base `NotConnected` rejection is surfaced as `Error`, and the buffer is released. | This is the only delta of the `CanSendNow` override. The inherited null, size, and byte-budget guards are covered by the root `RawEndpoint` layer. |
| `GetControls_ExposesEndpointConformanceControl` | `GetControls` publishes the endpoint conformance control. | Exactly one `RawUnreliableEndpointConformanceControl` is added and it implements the public endpoint control interface. | The base implementation contributes no controls. |
| `Start_WithBufferedMessage_HitsAfterReceivedGateExactlyOnce` | Every flushed message hits `AfterReceivedGate` once, immediately before it begins delivery. | One buffered pre-start message consumes exactly one gate hit, so the flush neither bypasses the gate nor hits it twice. | The gate is armed with a large quota, so the hit never blocks and the test stays single-threaded; the remaining quota is the exact hit count. The observation is fail-safe: a bypass leaves the quota untouched, a double hit consumes one more. |
| `Start_WithTwoBufferedMessages_HitsAfterReceivedGateOncePerMessage` | The gate is hit once per drained message, not once per flush. | Two buffered messages consume exactly two hits. | Same non-blocking quota technique; the count distinguishes per-message hitting from a single per-flush hit. |
| `GetControls_HonoursPredicate` | The predicate filters which controls are added. | A rejecting predicate yields nothing; a type predicate yields the conformance control. | The predicate is evaluated before the control is added. |
| `FailHard_AfterSuccessfulStart_HitsBeforeHandlerStoppedGateAndNotifiesOnce` | The handler-stop gate is hit immediately before a forwarded stop notification. | A hard failure after a completed start notifies the handler once and consumes exactly one gate hit. | The gate now sits in `HandlerGate.OnStopped`, so a hard-failure path that delivers the handler notification visits it exactly once. |
| `Start_WhenOnStartedThrows_DoesNotHitBeforeHandlerStoppedGate` | The gate is not hit for a handler whose start notification threw. | The suppressed stop notification leaves the gate quota untouched. | The gate is only reached after the start-completion flag is set. |
| `Start_WithStopFromOnStarted_DoesNotDeliverBufferedMessages` | A stop requested from `OnStarted` is latched before the inbox opens. | The buffered message is released, the handler receives no delivery, and the terminal notification is forwarded because the start notification returned. | The stop request is issued through the endpoint's public one-shot entry from the start probe. |
| `Stop_LatchedBeforeFlush_DoesNotDeliverBufferedMessages` | A stop latched between the start and flush actions suppresses the start notification and the drain. | The buffered message is released and no lifecycle callback runs. | The manual queue steps the start action, then the stop is latched before the finish/flush actions drain. |
| `Deliver_WhileOpen_ScheduledReceiveJob_ReleasesAfterStop` | A receive job scheduled before the stop latch must not deliver after `Stop` returns. | The scheduled job observes the latched stop, releases the message, and never invokes the handler. | The open inbox schedules the receive action; the stop latch is taken before the manual queue runs it. |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| Concrete client and server endpoints (`RawUnreliableDirectClientEndpoint`, `RawUnreliableUdpClientEndpoint`, the concrete server endpoints). | Carrier conformance suite | The concrete endpoints are internal to their carrier assemblies; their carrier behaviour is covered by `Tests/Pontifex.Transports.Tests/Raw/Unreliable`. |
| Send-commit conformance gates (`BeforeSendCommitGate`, `AfterSendCommitGate`) around the carrier call. | Carrier/conformance suite | The gates belong to the concrete `DoSendRaw`; the concrete unreliable carriers live outside this suite. |
| Abandoned-buffer release when the serial queue accepts but never runs an accepted action. | This layer | The manual queue models no release barrier, so the accepted-but-never-run path is not reachable deterministically. |
| Concurrent delivery ordering across threads. | Deferred suite layer | The default suite is deterministic; concurrency is covered by the transport conformance suite. |

## Deferred by design

| Gap | Reason |
|---|---|
| Dropped-message logging policy outside the inbox caps. | The caps are the only place where this layer drops silently; carrier drops belong to the transport layers. |
| Real carriers, sockets, networks, timers, and wall-clock time. | The endpoint contract is tested without a carrier; those facets belong to the transport and conformance suites. |
| The one-shot stop blocking at an armed conformance gate. | Requires a real blocking gate and a second thread; the latch/`IsValid` turnover is asserted synchronously instead. |

## Notes

- `TestRawUnreliableEndpoint` substitutes `ManualSerialActionQueue` for the production serial queue, so every
  start, finish-start, flush, delivery, and stop action is stepped explicitly. No test sleeps or uses real
  time.
- The start is driven entirely by the inherited implementation: `Start()` schedules the start action, which
  calls `FinishStart` and schedules the flush. `DrainOne` makes the window between the finish-start action and
  the flush action observable.
- The handler double records `OnStarted`, `OnReceived`, and `OnStopped`. `RecordingRawHandler` clones each
  delivered buffer and releases the original, so ownership is observed without retaining endpoint-owned
  buffers. Its `OnReceived` is virtual; the unreliable handler counts deliveries observed before and after
  `OnStarted` completed, and exposes an `OnStartedProbe` that reads `TestRawUnreliableEndpoint.PendingActions`.
  The probe is what pins "the start notification runs before the drain": buffered deliveries are only
  scheduled, so a counter of completed callbacks alone cannot see the difference on a non-inlining queue.
- Gate counts use `ICheckPointCtl.Arm(requiredHits: 100)`: the quota is far above the expected hits, so the
  gate never blocks and the drain stays on the test thread. The remaining free hits (`HitCount`) are the exact
  hit count; every arm is reset in a `finally`.
- The server trigger is drained by the same flush loop as a buffered message, so the per-message gate
  behaviour recorded here applies to the trigger too.
- `StoppedReasons` is recorded through a subscription installed after the endpoint's own subscription, so the
  inbox is always released before the test observes the terminal event.
- Buffer release is asserted through `IsAlive`. A double release is not observable through the contract and is
  not asserted; the endpoints release each buffered message once, on a single terminal transition.
