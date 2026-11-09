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
| `Init_WhenConnected_FailsAndNotifiesDisconnectThenStop` | `Init` in the connected state stops the connection. | `OnDisconnected` then `OnStopped` run with one shared reason before destruction. | Order is asserted through the shared event log. |
| `Init_AfterCompletedStop_FailsWithoutAdditionalNotification` | `Init` after a completed stop cannot revive the transport. | No additional handler notification or destruction occurs. | The second handler is never stored. |
| `Start_WithoutInit_FailsWithoutTransportHooksOrHandlerNotifications` | `Start` requires successful initialization. | `BeginConnect`/`OnReadyToConnect` do not run and no handler callback occurs. | The base start callback still observes the failure. |
| `Start_WhenInitialized_BeginsConnectThenReady` | A valid `Start` runs `BeginConnect` then `OnReadyToConnect`. | Hook order is correct and the transport stays valid but not connected. | — |
| `Start_WhenBeginConnectReturnsFalse_DestroysAndNotifiesStopped` | A rejected `BeginConnect` fails and cleans up. | `OnStopped` runs once, destruction runs, and no ready callback occurs. | — |
| `Start_WhenBeginConnectReturnsFalseAndOnStoppedThrows_StillDestroys` | A throwing `OnStopped` is isolated during start failure. | Destruction still runs and the start callback receives the stop reason. | The handler records its thrown exception for assertion. |
| `Start_WhenBeginConnectReturnsFalseAndDestroyThrows_StillDetaches` | A throwing `DestroyTransport` is isolated. | The attached endpoint is detached, `Start` returns false, and no exception escapes. | Detachment is owned by the base class. |
| `Start_WhenBeginConnectStopsTransport_ReturnsFalseWithoutOnReady` | A synchronous stop during `BeginConnect` beats the start path. | `OnReadyToConnect` is skipped and the handler sees the supplied stop reason. | — |
| `ConnectionFinished_WhenConnecting_InvokesActionWithHandlerAndConnects` | `ConnectionFinished` invokes the supplied action with the stored handler. | The action runs before `IsConnected` flips to true. | — |
| `ConnectionFinished_WhenAlreadyConnected_StopsWithTextFailWithoutInvokingAction` | `ConnectionFinished` outside the connecting state is a contract violation. | The action is skipped and the connection is torn down with a `TextFail`. | The reason text names the illegal states. |
| `ConnectionFinished_WhenInitialized_IsIgnoredAndStartStillWorks` | `ConnectionFinished` before start is ignored. | No cleanup occurs and a later `Start` still works. | — |
| `ConnectionFinished_WhenActionStopsTransport_DoesNotResurrectConnected` | A stop from inside the finish action wins over `Connected`. | The transport ends stopped, not connected, with one handler notification. | — |
| `ConnectionFinished_WhenActionFailsTransport_DoesNotResurrectConnected` | A failure from inside the finish action wins over `Connected`. | The transport ends invalid, not connected, with the failure reason propagated. | — |
| `ConnectionFinished_WhenActionThrows_FailsTransportWithoutConnecting` | A throwing finish action fails the transport. | The stop reason is an `ExceptionFail` carrying the same exception instance. | — |
| `ConnectionFinished_WhenActionAttachesConnectedEndpoint_IsConnectedDuringAction` | Connectivity follows an attached endpoint inside the finish action. | `IsConnected` is true during the action and after completion. | The endpoint double is shared with the `Endpoints` suite. |
| `ConnectionFinished_WhenConstructed_IsIgnoredAndInitStillWorks` | `ConnectionFinished` in the constructed state is ignored. | No cleanup occurs and `Init`/`Start` still work. | — |
| `ConnectionFailed_WhenConstructed_IsIgnoredAndInitStillWorks` | `ConnectionFailed` in the constructed state is ignored. | No cleanup occurs and `Init`/`Start` still work. | — |
| `ConnectionFailed_WhenConnecting_FailsAndNotifiesStopped` | A connection failure from connecting raises `OnStopped` once. | `OnDisconnected` is skipped and the reason is `TextFail("Connection failed")`. | — |
| `ConnectionFailed_WhenConnected_StopsWithTextFailAndNotifiesDisconnect` | A failure outside connecting is a contract violation. | The connected endpoint receives `OnDisconnected` then `OnStopped` with one reason. | The transport stays valid after the graceful stop. |
| `ConnectionFailed_WhenInitialized_IsIgnoredAndStartStillWorks` | `ConnectionFailed` before start is ignored. | The transport remains startable. | — |
| `IsConnected_TracksTransportStateWithoutEndpoint` | Without an attached endpoint, connectivity mirrors the internal state. | `IsConnected` is false until completion and false again after stop. | — |
| `IsConnected_WithAttachedEndpoint_DelegatesToEndpoint` | With an attached endpoint, connectivity delegates to the endpoint. | `IsConnected` follows the endpoint's own connection state. | — |
| `IsConnected_IsFalseDuringOnDisconnected` | `OnDisconnected` observes `IsConnected == false`. | Connectivity is cleared before the callback runs. | — |
| `IsConnected_IsFalseDuringOnStopped` | `OnStopped` observes `IsConnected == false`. | Connectivity is cleared before the callback runs. | — |
| `IsConnected_IsFalseInsideDestroyTransport` | `DestroyTransport` observes `IsConnected == false` when no endpoint is attached. | The transport state is already cleared at destruction. | With an attached endpoint the endpoint remains the connectivity source; see the next test. |
| `AttachEndpoint_WithNull_Throws` | An endpoint attachment requires a non-null endpoint. | `ArgumentNullException` is thrown and nothing is attached. | — |
| `AttachEndpoint_WhenAlreadyAttached_ThrowsAndKeepsFirst` | Only one endpoint may be attached. | The second attachment throws and the first endpoint remains attached. | — |
| `AttachEndpoint_EndpointStop_StopsTransportWithSameReason` | An attached endpoint's terminal stop stops the transport. | The same reason reaches destruction and the start callback. | The transport handler is skipped because the endpoint owns its own notifications. |
| `Stop_WhileConnected_DetachesEndpointAfterDestroy` | The base owns endpoint detachment. | The endpoint stays attached during destruction and is null afterwards. | — |
| `DestroyTransport_SeesAttachedEndpointBeforeBaseDetaches` | Detachment happens after `DestroyTransport` returns. | The double sees the endpoint and endpoint-derived connectivity during destruction. | — |
| `Stop_AfterInitBeforeStart_KeepsInitializedStateAndAllowsStart` | A stop after `Init` but before `Start` is a no-op. | No destruction or notification occurs and `Start` still works. | — |
| `Stop_WhileConnecting_NotifiesStoppedThenDestroys` | Stopping while connecting notifies `OnStopped` then destroys. | No `OnDisconnected` occurs and the start callback receives the same reason. | — |
| `Stop_WhileConnected_NotifiesDisconnectedThenStoppedThenDestroys` | Stopping while connected notifies `OnDisconnected`, then `OnStopped`, then destroys. | Both callbacks and the start callback share the supplied reason instance. | — |
| `Stop_WhileConnected_WhenOnDisconnectedThrows_StillCallsOnStoppedAndDestroys` | A throwing `OnDisconnected` is isolated. | `OnStopped` still runs and destruction still completes. | The handler records the thrown exception. |
| `Stop_WhileConnected_WhenOnStoppedThrows_StillDestroys` | A throwing `OnStopped` is isolated. | `OnDisconnected` still runs and destruction still completes. | The handler records the thrown exception. |
| `Stop_WhileConnected_WhenDestroyThrows_StillDetachesAndNotifiesStartCallback` | A throwing `DestroyTransport` is isolated during stop. | The endpoint is detached and the start callback still receives the reason. | — |
| `Stop_WhileConnecting_WhenOnStoppedThrows_StillDestroys` | A throwing `OnStopped` while connecting is isolated. | Destruction still runs and the start callback still fires. | — |
| `Stop_WhileConnectedWithAttachedEndpoint_SkipsHandlerNotifications` | An attached endpoint owns the handler notifications. | The transport handler receives neither `OnDisconnected` nor `OnStopped`. | The endpoint's own handler is covered by the `Endpoints` suite. |

## Planned

| Missing layer or test | Owner | Reason |
|---|---|---|
| — | — | No known missing tests for this layer. |

## Deferred by design

| Gap | Reason |
|---|---|
| Conformance controls (`GetControls`, gates, failure injection). | The conformance subsystem is out of scope and planned for a rewrite. |
| Real carrier connection establishment, sockets, and network timing. | The layer is exercised through `BeginConnect`/`ConnectionFinished`/`ConnectionFailed` seams. |
| Thread-safety and scheduling of callbacks. | The default suite is deterministic; carrier scheduling belongs to the carrier/conformance suites. |

## Notes

- `TestRawReliableClientTransport` supplies the abstract hooks and exposes the protected state
  machine transitions; `RecordingRawReliableClientHandler` records callbacks, reasons, and
  configurable exceptions.
- `RecordingRawReliableClientHandler` and the transport share an event log so callback ordering
  can be asserted without sleeping.
- The layer reuses `TestRawReliableClientEndpoint` from
  `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/Harness` for endpoint-attachment tests.
  Both suites live in the same test assembly; the endpoint double is covered by the `Endpoints`
  suite.
