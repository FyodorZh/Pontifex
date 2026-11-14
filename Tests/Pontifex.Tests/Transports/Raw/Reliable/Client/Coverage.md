# RawReliableClientTransport Layer Coverage

- **Layer:** RawReliableClientTransport
- **Subject:** `Pontifex.Raw.Reliable.RawReliableClientTransport<TClientHandler>`
- **Directory:** `Tests/Pontifex.Tests/Transports/Raw/Reliable/Client`
- **Inheritance parent:** `Pontifex.Raw.Reliable.RawReliableTransport`
- **Contract:** [RawReliableClientTransport.cs](../../../../../../Pontifex/Implementations/Raw/Reliable/RawReliableClientTransport.cs),
  [IRawReliableClientTransport.cs](../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableClientTransport.cs),
  [IRawReliableClientHandler.cs](../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableClientHandler.cs),
  [IRawReliableEndpoint.cs](../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableEndpoint.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Init_WhenConstructed_StoresHandlerAndInitializes` | `Init` runs `SetupHandler` and stores the returned handler from the constructed state. | The handler is installed and the transport stays valid, not started. | `SetupHandler` is recorded by the double. |
| `Init_WithNullHandler_FailsAndInvalidates` | A null handler is an invalid `Init`. | `Init` returns false, the transport becomes invalid, and no handler is stored. | `SetupHandler` is still invoked with the null argument. |
| `Init_WhenSetupHandlerReturnsNull_FailsAndInvalidates` | A `SetupHandler` that returns null rejects initialization. | No handler is stored and the transport is invalid. | — |
| `Init_WhenSetupHandlerTransforms_StoresTransformedHandler` | The handler stored is the one returned by `SetupHandler`. | The transformed instance wins over the original argument. | — |
| `Init_CalledTwice_FailsAndKeepsFirstHandler` | `Init` is one-shot. | The second call fails, keeps the first handler, and invalidates the transport. | The second argument still reaches `SetupHandler`. |
| `Init_AfterStartWithoutInit_FailsWithoutCallingSetupHandler` | `Init` is illegal after a failed start attempt. | `SetupHandler` is not called and no handler is installed. | — |
| `Init_WhenConnecting_FailsAndStopsWithHandlerNotification` | `Init` in the connecting state fails through the stop path. | `OnStopped` runs once, `DestroyTransport` runs, and the start callback receives the same reason. | Reason is a `TextFail` sourced from the transport name. |
| `Init_AfterCompletedStop_FailsWithoutAdditionalNotification` | `Init` after a completed stop cannot revive the transport. | No additional handler notification or destruction occurs. | The second handler is never stored. |
| `Start_WithoutInit_FailsWithoutTransportHooksOrHandlerNotifications` | `Start` requires successful initialization. | `BeginConnect`/`OnReadyToConnect` do not run and no handler callback occurs. | The base start callback still observes the failure. |
| `Start_WhenInitialized_BeginsConnectThenReady` | A valid `Start` runs `BeginConnect` then `OnReadyToConnect`. | Hook order is correct and the transport stays valid but not connected. | No endpoint is attached by the double. |
| `Start_WhenBeginConnectReturnsFalse_DestroysAndNotifiesStopped` | A rejected `BeginConnect` fails and cleans up. | `OnStopped` runs once, destruction runs, and no ready callback occurs. | No endpoint is attached, so the transport owns the handler notification. |
| `Start_WhenBeginConnectAttachesEndpointAndReturnsFalse_NotifiesHandlerAndDetaches` | A rejected `BeginConnect` may already have attached its endpoint. | The transport still notifies `OnStopped`, destroys with the endpoint attached, and detaches it. | A failed `BeginConnect` never hands lifecycle ownership to the endpoint, so the transport supplies the terminal notification. |
| `Start_WhenBeginConnectReturnsFalseAndOnStoppedThrows_StillDestroys` | A throwing `OnStopped` is isolated during start failure. | Destruction still runs and the start callback receives the stop reason. | The handler records its thrown exception for assertion. |
| `Start_WhenBeginConnectReturnsFalseAndDestroyThrows_StillDetaches` | A throwing `DestroyTransport` is isolated. | The attached endpoint is detached, `Start` returns false, and no exception escapes. | Detachment is owned by the base class. |
| `Start_WhenBeginConnectStopsTransport_ReturnsFalseWithoutOnReady` | A synchronous stop during `BeginConnect` beats the start path. | `OnReadyToConnect` is skipped and the handler sees the supplied stop reason. | — |
| `IsConnected_WithAttachedEndpoint_DelegatesToEndpoint` | Connectivity is owned by the attached endpoint. | `IsConnected` follows the endpoint's own connection state and ignores the transport lifecycle. | The endpoint double is shared with the `Endpoints` suite. |
| `IsConnected_IsFalseDuringOnStopped_WhenStartFails` | The failed-start stop path observes `IsConnected == false`. | No endpoint is attached, so connectivity is false during the handler callback. | — |
| `IsConnected_IsFalseInsideDestroyTransport` | `DestroyTransport` observes the attached endpoint, not transport-derived state. | An endpoint that never connected reports `IsConnected == false` during destruction. | A connected endpoint reports `true` inside destruction; see `DestroyTransport_SeesAttachedEndpointBeforeBaseDetaches`. |
| `AttachEndpoint_WithNull_Throws` | An endpoint attachment requires a non-null endpoint. | `ArgumentNullException` is thrown and nothing is attached. | — |
| `AttachEndpoint_WhenAlreadyAttached_ThrowsAndKeepsFirst` | Only one endpoint may be attached. | The second attachment throws and the first endpoint remains attached. | — |
| `AttachEndpoint_EndpointStop_StopsTransportWithSameReason` | An attached endpoint's terminal stop stops the transport. | The same reason reaches destruction and the start callback. | The transport handler is skipped because the endpoint owns its own notifications. |
| `Stop_WhileEndpointConnected_DetachesEndpointAfterDestroy` | The base owns endpoint detachment. | The endpoint stays attached during destruction and is null afterwards. | The endpoint double is connected before the transport stops. |
| `DestroyTransport_SeesAttachedEndpointBeforeBaseDetaches` | Detachment happens after `DestroyTransport` returns. | The double sees the endpoint and endpoint-derived connectivity during destruction. | — |
| `Stop_AfterInitBeforeStart_KeepsInitializedStateAndAllowsStart` | A stop after `Init` but before `Start` is a no-op. | No destruction or notification occurs and `Start` still works. | — |
| `Stop_WhileConnecting_NotifiesStoppedThenDestroys` | Stopping while connecting with no endpoint notifies `OnStopped` then destroys. | The start callback receives the same reason. | The transport never calls `OnDisconnected`. |
| `Stop_WhileConnectingWithAttachedEndpoint_SkipsHandlerNotification` | Reaching `Connecting` hands the lifecycle to the attached endpoint even before it starts. | The transport handler is skipped and the endpoint is detached after destruction. | Mirrors a carrier whose endpoint is still connecting; the endpoint owns its terminal notification. |
| `Stop_WhileEndpointConnected_WhenDestroyThrows_StillDetachesAndNotifiesStartCallback` | A throwing `DestroyTransport` is isolated during stop. | The endpoint is detached and the start callback still receives the reason. | — |
| `Stop_WhileConnecting_WhenOnStoppedThrows_StillDestroys` | A throwing `OnStopped` while connecting is isolated. | Destruction still runs and the start callback still fires. | — |
| `Stop_WhileEndpointConnected_SkipsHandlerNotifications` | An attached endpoint owns the handler notifications. | The transport handler receives neither `OnDisconnected` nor `OnStopped`. | The endpoint's own handler is covered by the `Endpoints` suite. |

## Planned

| Missing layer or test | Owner | Reason |
|---|---|---|
| — | — | No known missing tests for this layer. |

## Deferred by design

| Gap | Reason |
|---|---|
| Conformance controls (`GetControls`, gates, failure injection). | The conformance subsystem is out of scope and planned for a rewrite. |
| Real carrier connection establishment, sockets, and network timing. | The layer is exercised through the `BeginConnect`/`AttachEndpoint` seams. |
| Connection completion and failure reporting (`OnConnected`, `OnDisconnected`, `OnStopped` for an attached endpoint). | The attached endpoint owns these notifications; the `Endpoints` suite covers them. |
| Thread-safety and scheduling of callbacks. | The default suite is deterministic; carrier scheduling belongs to the carrier/conformance suites. |

## Notes

- `TestRawReliableClientTransport` supplies the abstract hooks and exposes the protected surface;
  `RecordingRawReliableClientHandler` records callbacks, reasons, and configurable exceptions.
- `RecordingRawReliableClientHandler` and the transport share an event log so callback ordering
  can be asserted without sleeping.
- The layer reuses `TestRawReliableClientEndpoint` from
  `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/Harness` for endpoint-attachment tests.
  Both suites live in the same test assembly; the endpoint double is covered by the `Endpoints`
  suite.
