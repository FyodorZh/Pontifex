# RawUnreliableClientTransport Layer Coverage

- **Layer:** RawUnreliableClientTransport
- **Subject:** `Pontifex.Raw.Unreliable.RawUnreliableClientTransport`
- **Directory:** `Tests/Pontifex.Tests/Transports/Raw/Unreliable/Client`
- **Inheritance parent:** `Pontifex.Raw.Unreliable.RawUnreliableTransport`
- **Contract:** [RawUnreliableClientTransport.cs](../../../../../../Pontifex/Implementations/Raw/Unreliable/RawUnreliableClientTransport.cs),
  [IRawUnreliableClient.cs](../../../../../../Pontifex/Abstractions/Transports/Raw/Unreliable/IRawUnreliableClient.cs),
  [IRawUnreliableHandler.cs](../../../../../../Pontifex/Abstractions/Transports/Raw/Unreliable/IRawUnreliableHandler.cs),
  [IRawUnreliableEndpoint.cs](../../../../../../Pontifex/Abstractions/Transports/Raw/Unreliable/IRawUnreliableEndpoint.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Init_WhenConstructed_BindsHandlerAndLeavesTransportUnstarted` | `Init` binds the handler from the constructed state. | The handler is stored, the transport stays valid and unstarted, and no carrier hook runs. | The transport does not wrap or transform the handler. |
| `Init_CalledTwice_ReturnsFalseAndKeepsFirstHandler` | `Init` is one-shot. | The second call returns `false`, the first handler stays bound, and the transport is not invalidated. | Rejection without invalidation keeps the first binding usable. |
| `Init_WithNullHandler_ThrowsWithoutChangingTheTransport` | A null handler is rejected. | `ArgumentNullException` is thrown, nothing is bound, and the transport is still startable (the start fails only for the missing initialization). | The handler contract (`IRawUnreliableClient.Init`) documents the throw. |
| `Init_AfterStartAttempt_ReturnsFalseWithoutBindingHandler` | `Init` after a start attempt is rejected. | The handler stays unbound and neither carrier nor endpoint work runs. | The start attempt is terminal for `Init` even when it failed. |
| `Init_AfterStart_ReturnsFalse` | `Init` after a successful start is rejected. | The first handler stays bound. | — |
| `Start_WithoutInit_FailsWithoutStartingCarrierOrCreatingEndpoint` | `TryStart` requires a bound handler. | The transport fails, starts no carrier, creates no endpoint, and delivers no start callback. | The base transport owns the failure notification on this path. |
| `Start_WhenInitialized_CreatesAttachesAndStartsOneEndpoint` | The start sequence is carrier, endpoint creation, attachment, endpoint start. | One endpoint is created for the bound handler, attached, and started; the start notification is raised by the endpoint's flush. | The endpoint double uses a manual queue, so the start is asserted by draining it explicitly. |
| `Start_CalledTwice_CreatesEndpointOnlyOnce` | `Start` is one-shot. | The second start fails and does not create another endpoint or start the carrier again. | — |
| `Start_WhenCarrierFails_StopsCarrierAndNotifiesHandlerWithoutEndpoint` | A rejected carrier start fails the start and releases the carrier. | `StopCarrier` runs once, the handler receives a `TextFail` because no endpoint exists, and the user start callback is not invoked. | `StopCarrier` must tolerate a carrier that never started; the hook contract states this. A failed initial start is user-callback silent. |
| `Start_WhenCarrierThrows_LogsAndReturnsFalse` | A throwing carrier start is isolated. | The exception is logged, the start returns `false`, and no endpoint is created. | Logging is asserted through a recording `ILogConsumer`. |
| `Start_WhenCreateEndpointThrows_LogsAndReturnsFalse` | A throwing endpoint factory fails the start. | The factory exception is logged, the transport attaches nothing, and the carrier is stopped. | — |
| `Start_WhenNoEndpointWasCreated_NotifiesHandlerStoppedDirectly` | The transport owns the handler stop notification until an endpoint exists. | The handler receives one `ExceptionFail` carrying the factory exception, the carrier stop receives the same instance, and the user start callback is not invoked. | This is the reachable "no endpoint created" fallback path; the endpoint would otherwise own the notification. A failed initial start is user-callback silent. |
| `Start_WhenEndpointStartIsRejected_FailsWithoutHandlerNotification` | A rejected endpoint `Start` fails the transport start while the attached endpoint owns the handler notification. | `Start` returns false, the carrier is stopped once with a `TextFail`, the transport is invalid and detached, and the never-started endpoint delivers no `OnStopped` because its handler gate suppresses it. | Reaches the branch with a one-shot queue rejection armed by the endpoint factory. The endpoint is still stopped through the one-shot entry and its terminal transition is drained explicitly. |
| `Start_WhenEndpointStopsDuringStart_ReplaysStopAfterStart` | An endpoint stop observed while the start is in progress is latched, not forwarded. | The start returns `true`, the endpoint's reason is replayed after `OnStarted`, and the transport ends stopped with that reason; the handler sees `OnStarted` once and `OnStopped` once. | The endpoint double runs on the production inline queue and stops itself in an action scheduled after its flush, so the terminal notification arrives inside `endpoint.Start()` while the transport's start latch is set. |
| `Start_WhenTransportStopsDuringEndpointCreation_StopsUnstartedEndpointAndReturnsFalse` | A stop that starts while the endpoint is being created wins over the new endpoint. | The start returns `false`, the handler already received the fallback notification for the stop reason, the carrier is stopped once, the user callback is not invoked, and the created endpoint is stopped unstarted with a `TextFail` without raising `OnStarted` or a second `OnStopped`. | The factory stops the transport reentrantly on the starting thread, which is deterministic and single-threaded; it exercises the post-creation state re-check that prevents an endpoint from being started by a transport that already stopped. |
| `EndpointStopped_AfterStart_StopsTransportWithSameReason` | The transport observes the attached endpoint's terminal notification. | The transport stops with the same reason instance, stops the carrier once, detaches the endpoint, and delivers the handler notification only through the endpoint. | — |
| `Stop_WhenStarted_StopsEndpointBeforeCarrier` | The stop order is endpoint before carrier. | Inside `StopCarrier` a second one-shot endpoint stop returns `false`, proving the transport had already latched the endpoint stop; the handler is notified once by the endpoint. | The probe uses only the public one-shot `IRawUnreliableEndpoint.Stop` result; it does not inspect private state. |
| `Stop_WhenStarted_DoesNotNotifyHandlerWhenAnEndpointOwnsTheNotification` | The transport does not duplicate the handler notification. | Right after the transport stop the handler has no notification, because the endpoint owns it and has not run its transition yet. | Draining the endpoint after the assertions produces the single notification. |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| — | — | No known missing tests for this layer. |

## Deferred by design

| Gap | Reason |
|---|---|
| Re-testing inherited `AnyTransport`/`RawTransport`/`RawUnreliableTransport` behaviour. | Ancestor layers own that coverage. |
| Real carrier start/stop, sockets, and network timing. | The layer is exercised through the `StartCarrier`/`StopCarrier`/`CreateEndpoint` seams. |
| The endpoint's lazy connect and its serial send queue. | Owned by the endpoint-owned architecture and covered by the `Endpoints` suite. |

## Notes

- The layer reuses `TestRawUnreliableEndpoint`, `RecordingRawUnreliableHandler`, `RecordingLogConsumer`,
  and `TestBuffers` from `Tests/Pontifex.Tests/Endpoints`. Both suites live in the same test assembly; the
  endpoint double is covered by the `Endpoints` suite and is not copied here.
- `StopDuringStartEndpoint` is the only client-specific double. It exists because the start-latch path
  requires the endpoint terminal notification to arrive synchronously inside `endpoint.Start()`; the
  shared double's manual queue cannot produce that window.
- The shared endpoint double exposes `RejectNextSchedule()` so the rejected-`Start` path is reachable
  without a second client-specific double. The rejection is armed by the endpoint factory before the
  transport calls `endpoint.Start()`.
- The client transport keeps a `Constructed`/`Initialized`/`Started`/`Stopped` lifecycle and does not
  track connectivity. `IsStarted`/`IsValid` are the inherited observables used by the tests.
- `UnionDataList` is not allocated by this layer's tests; the `TearDown` `GC.Collect()` is kept for
  consistency with the suite convention.
