# RawUnreliableServerTransport Layer Coverage

- **Layer:** RawUnreliableServerTransport
- **Subject:** `Pontifex.Raw.Unreliable.RawUnreliableServerTransport<TFactory>`
- **Directory:** `Tests/Pontifex.Tests/Transports/Raw/Unreliable/Server`
- **Inheritance parent:** `Pontifex.Raw.Unreliable.RawUnreliableTransport`
- **Contract:** [RawUnreliableServerTransport.cs](../../../../../../Pontifex/Implementations/Raw/Unreliable/RawUnreliableServerTransport.cs),
  [RawUnreliableServerEndpoint.cs](../../../../../../Pontifex/Implementations/Endpoints/Raw/Unreliable/RawUnreliableServerEndpoint.cs),
  [IRawUnreliableServerEndpointImp.cs](../../../../../../Pontifex/Implementations/Endpoints/Raw/Unreliable/IRawUnreliableServerEndpointImp.cs),
  [SendBudget.cs](../../../../../../Pontifex/Implementations/Endpoints/Raw/Unreliable/SendBudget.cs),
  [IRawUnreliableHandler.cs](../../../../../../Pontifex/Abstractions/Transports/Raw/Unreliable/IRawUnreliableHandler.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `SendBudgetLimits_ByDefault_DeriveFromMessageMaxByteSize` | The default shared-budget limits derive from the carrier message limit. | The default message cap is 100 and the default byte cap is `MessageMaxByteSize * 100`, for two different message sizes. | The limits are exposed as protected virtual properties so carriers can override them without changing the base. |
| `Start_AfterInitialization_CreatesSharedBudgetFromConfiguredCaps` | `TryStart` creates the shared budget from the overridable limits. | A budget configured with a 250-byte and 3-message cap accepts 180 bytes and rejects a further 90 bytes. | Proves the override is honoured, not just the default formula; the budget is null before the start. |
| `Start_CreatesSharedBudgetHonouringMessageCap` | The message limit of the shared budget comes from the hook. | A 2-message cap rejects the third reserved message. | — |
| `SharedSendBudget_IsPassedToEveryEndpointWithTheConfiguredLocalCap` | One budget is shared by every session of the transport. | Two route creations receive the same budget instance and the configured byte cap for their local budget. | The carrier is responsible for configuring the endpoint's local budget with `SendBudgetBytes`; the double records what the transport handed it and passes it on, mirroring what a carrier does. |
| `Start_BeforeInitialization_FailsWithoutStartingCarrierOrCreatingBudget` | `TryStart` requires `TryInitializeServer`. | The carrier is not started, no budget is created, and the transport is invalidated. | — |
| `Start_WhenCarrierReturnsFalse_FailsWithoutStoppingCarrier` | A rejected carrier start fails the start without a stop cascade. | `StopCarrier` is not called, the budget is dropped, and the transport is invalid. | The carrier must release its own partial resources; this is the documented carrier-start failure contract. |
| `Start_WhenCarrierThrows_LogsAndFailsWithoutStoppingCarrier` | A throwing carrier start is isolated. | The exception is logged, `StopCarrier` is not called, the budget is dropped, and the start returns `false`. | — |
| `TryInitializeServer_CalledTwice_ReturnsFalseAndKeepsFirstFactory` | Server initialization is one-shot. | The second call fails and a later route is built from the first factory's handler. | The second factory would return the other handler, so the delivered handler proves which one is kept. |
| `TryInitializeServer_AfterStartAttempt_ReturnsFalse` | `TryInitializeServer` is rejected after a start attempt. | Initialization fails and `HasStartBeenAttempted` is observable. | — |
| `OnCarrierInbound_WhenRouteIsNew_CreatesEndpointWithTriggerAndStartsIt` | The creation order is factory, endpoint construction with the trigger, route publication, then start. | The factory sees the source; the carrier receives the same trigger instance for the constructor; the route count becomes one; the endpoint does not raise `OnStarted` until it is started; the trigger is delivered first and after the start notification. | The trigger is handed to `CreateEndpoint`, which constructs the endpoint around it; the test pins the observable hand-off and delivery order, not the insertion mechanism. The endpoint is drained explicitly because its queue is manual. |
| `OnCarrierInbound_WhenRouteExists_DeliversToItWithoutAnotherFactoryCall` | Messages for an existing route bypass admission. | The factory and endpoint creation counts stay one and the second message reaches the existing endpoint. | — |
| `OnCarrierInbound_WhenFactoryReturnsNull_ReleasesTriggerAndPublishesNoRoute` | A null handler rejects the route. | The trigger is released, no endpoint is created, no route is published, and nothing is logged. | Dropped packets and rejected routes are silent by design. |
| `OnCarrierInbound_WhenNoFactoryIsBound_ReleasesTriggerAndPublishesNoRoute` | A null factory rejects every new route without failing the transport. | The factory is never invoked, the trigger is released, no route is published, and nothing is logged. | Null is accepted by `TryInitializeServer` precisely so this path is reachable. |
| `OnCarrierInbound_WhenFactoryThrows_LogsReleasesTriggerAndPublishesNoRoute` | A throwing factory is isolated. | The trigger is released exactly once, no route is published, and the exception is logged. | — |
| `OnCarrierInbound_WhenCreateEndpointThrows_LogsReleasesTriggerAndPublishesNoRoute` | A throwing endpoint constructor is isolated. | The trigger is released exactly once, no route is published, and the exception is logged. | The trigger was not adopted by an endpoint, so the admission path still owns it. |
| `OnCarrierInbound_WithNullArguments_Throws` | A source and a trigger are required. | Both null arguments throw `ArgumentNullException` and no route or admission side effect remains. | The trigger is released by the test because the call rejects it before ownership transfers. |
| `OnCarrierInbound_AfterStop_ReleasesTriggerWithoutCreatingRoute` | Admission is closed while the transport is stopping. | No factory or endpoint work runs, the trigger is released, and no route is published. | `IsStopping` is observable after the stop. |
| `OnCarrierInbound_ConcurrentTriggersForOneSource_InvokeFactoryOnce` | The admission lock serializes factory invocation and route publication. | Two contenders released together for one source produce exactly one factory call, one endpoint, and one route; both messages are delivered. | Categorised `Transports.Concurrency`. Both threads are released by a gate, joined with a bounded timeout, and the gates are always set in `finally`; the assertion is an invariant (one factory call), not a scheduling order. The received order is not asserted because which contender creates the route is not fixed. |
| `EndpointStopped_RemovesRouteAndAllowsRecreation` | The transport observes the session endpoint's terminal notification. | The route is removed, the handler receives the endpoint reason, and the next trigger for the same source creates a new route. | — |
| `EndpointStopped_RemovesOnlyItsOwnRoute` | Route removal is per entry. | Stopping one session leaves the other route live; a message for the live route does not invoke the factory, and a message for the stopped route does. | See the deferred stale-subscription note below. |
| `Stop_WhenRoutesExist_StopsEveryLiveEndpointWithTransportReason` | The stop cascade stops every live session with the transport reason. | Admission closes before the carrier stops, every route is removed, and each endpoint and handler observes the same reason instance. | `IsStoppingDuringStopCarrier` proves the flag is published under the admission lock before the carrier call. |
| `Stop_WhileEndpointIsBeingCreated_RemovesRouteAndStopsUnstartedEndpoint` | A stop that starts while an endpoint is being created wins over the new route. | The route is removed, the endpoint is never started (no `OnStarted`/`OnStopped` for its handler), and its trigger is released by the terminal stop of the never-started endpoint. | The double's factory stops the transport reentrantly on the admitting thread, which is deterministic and keeps the test single-threaded. The production contract forbids user code from calling the lifecycle synchronously; this exercises the defensive re-check after the lock. |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| — | — | No known missing tests for this layer. |

## Deferred by design

| Gap | Reason |
|---|---|
| A stale endpoint subscription attempting to remove a recreated route for the same source. | The transport never replaces a published route while its endpoint is alive, and the endpoint raises `Stopped` at most once and synchronously, so the stale invocation cannot be produced through the public contract without reflection. `EndpointStopped_RemovesOnlyItsOwnRoute` still pins that removal targets one entry only; the reference compare is the structural guard for the unreachable case. |
| Budget accept/reject through a live endpoint, and budget release on transmission. | Owned by the `RawUnreliableServerEndpoint` layer of the `Endpoints` suite; this layer asserts the transport creates and shares the budget and supplies the caps. |
| Conformance gates `BeforeHandlerFactoryGate`/`BeforeHandlerStartedGate`. | The conformance subsystem is out of scope for this suite. Gate placement is covered by the conformance suite in `Tests/Pontifex.Transports.Tests`. |
| Real carrier sockets, networks, and timing. | The layer is exercised through the `StartCarrier`/`StopCarrier`/`CreateEndpoint`/`InvokeFactory` seams. |

## Notes

- `TestRawUnreliableServerTransport` implements the carrier hooks and builds
  `TestRawUnreliableServerEndpoint` sessions from the endpoint suite. The session queue is manual, so every
  start, delivery, and stop transition is stepped explicitly; no test sleeps or uses real time.
- The concurrency test is the only test using real threads. It is additionally categorised
  `Transports.Concurrency` and is still deterministic: the two contenders are released together, the
  factory gate holds the admitting thread inside the admission lock, and both threads are joined.
- The factory runs under the admission lock, so a factory (or endpoint constructor) must not call the
  transport lifecycle synchronously in production. The stop-during-creation test relies on the reentrant
  call only to reach the re-check deterministically.
- `RecordingRawUnreliableHandler` clones and releases the buffers it receives, so trigger ownership is
  asserted through `UnionDataList.IsAlive` without retaining endpoint-owned buffers.
- `TearDown` calls `GC.Collect()` because the tests rent `UnionDataList` buffers from the shared pool.
