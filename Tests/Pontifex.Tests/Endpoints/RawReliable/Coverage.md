# RawReliableEndpoint Layer Coverage

- **Layer:** RawReliableEndpoint
- **Subject:** `Pontifex.Raw.Reliable.RawReliableEndpoint`
- **Directory:** `Tests/Pontifex.Tests/Endpoints/RawReliable`
- **Inheritance parent:** `Pontifex.Raw.RawEndpoint`
- **Contract:** [RawReliableEndpoint.cs](../../../../Pontifex/Implementations/Raw/Reliable/RawReliableEndpoint.cs),
  [IRawReliableEndpoint.cs](../../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableEndpoint.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Start_ReachesConnectingAndCallsBeginConnect` | `BeginStart` moves the reliable stage to `Connecting` and calls `BeginConnect`. | Before completion the reliable observables are false and `BeginConnect` was called once. | `Start` is admitted but the raw start remains incomplete until `FinishConnect`. |
| `FinishConnect_WhenConnecting_CompletesConnection` | `FinishConnect` completes the raw start and the reliable connection. | `OnConnected` runs once and `IsLowLevelConnected`, `IsConnected`, `IsRunning` become true. | The double records `OnConnected`; alias identity is not relevant. |
| `FinishConnect_WithoutStart_FailsAndDoesNotConnect` | `FinishConnect` requires a start in progress. | The `ifFailed` callback runs, no connection is reported, and the endpoint remains unconnected. | The raw `FinishStart` failure is what makes `ifFailed` run. |
| `Stop_WhileConnecting_ReportsConnectionThenTearsDown` | A stop latched while connecting still reports the completed connection before teardown. | `OnConnected` runs once, then `BeginDisconnect` is called with the stop reason; the handler stop arrives after `FinishDisconnect`. | Ordering relies on the reliable `BeginStop` scheduling its work rather than running inline. |
| `Send_WhenConnected_FramesPayloadMessage` | A connected send prepends `PayloadMessage`. | The first element is the payload frame type and the original content follows. | The payload type byte is endpoint-owned framing, not application data. |
| `Send_FramingCountsTowardMessageMaxByteSize` | The framed size, not the raw payload size, is checked against `MessageMaxByteSize`. | A payload that fits exactly is rejected once the frame byte is added. | The buffer is mutated before admission, as documented. |
| `ProcessInbound_PayloadFrame_DeliversPayloadWithoutTypeByte` | A payload frame is delivered with its type byte stripped. | The handler receives only the application payload. | The frame builder puts the type byte first and the payload after it. |
| `ProcessInbound_DisconnectFrame_StopsWithGracefulRemoteIntention` | A remote disconnect frame initiates a graceful remote stop. | `BeginDisconnect` runs with `GracefulRemoteIntention` and the frame is not delivered. | The handler stop is observed only after `FinishDisconnect`. |
| `ProcessInbound_UnhandledFrameType_StopsWithTextFail` | Frame types not handled by this layer are protocol failures. | Invalid, ping, pong, ack-request, ack-response, and unknown frame types all stop with `TextFail` and release the frame. | Six test cases share one contract row; derived layers override ping/pong/ack handling. |
| `ProcessInbound_WhenNotConnected_ReleasesAndStopsWithTextFail` | Inbound frames require a low-level connection. | The frame is released, not delivered, and the endpoint stops with `TextFail`. | The stop is latched while connecting and completed after connect/disconnect. |
| `ProcessInbound_EmptyFrame_StopsWithTextFail` | Empty inbound data has no frame type. | The endpoint treats it as a parse failure and stops with `TextFail`. | The empty list is released by the dispatch path. |
| `DisconnectGracefully_SendsDisconnectFrameAndStopsGently` | `DisconnectGracefully` frames a disconnect message and stops with `UserIntention`. | The disconnect frame is transmitted, `BeginDisconnect` runs, and the stop is gentle. | The reason source is the transport name. |
| `Disconnect_InterfaceInitiatesGentleStop` | `IRawReliableEndpoint.Disconnect` initiates a gentle stop with the caller's reason. | `BeginDisconnect` receives the same reason and `tryBeGentle: true`. | The interface returns `true`; the return value is not asserted here. |
| `DisconnectGracefully_WhenDisconnectFrameCannotBeAdmitted_StopsUngently` | A disconnect frame that cannot be admitted forces an ungentle stop. | No frame is captured, `BeginDisconnect` still runs, and `tryBeGentle` is false. | Capacity is set below the frame size to force `BufferOverflow`. |
| `DisconnectGracefully_UsesTransportNameAsReasonSource` | The graceful-disconnect reason is sourced from the transport name. | The reason's `Source` matches the configured transport name. | — |
| `Stop_WhileConnected_DisconnectsBeforeNotifyingStopped` | A connected stop enters `Disconnecting` before the handler stop. | The reliable observables are false and no handler stop occurs until `FinishDisconnect`. | `BeginDisconnect` completes the teardown explicitly. |
| `DisconnectCycle_DoesNotInvokeOnDisconnected` | This layer does not raise `OnDisconnected` itself. | A payload delivery followed by a stop produces only `OnReceived` and `OnStopped`. | Only the ack subclasses add the logical disconnect notification. |
| `FinishDisconnect_WhenNotDisconnecting_FailsHard` | `FinishDisconnect` is legal only while disconnecting. | An invalid completion fails hard with `ExceptionFail`. | The reliable stage is moved to disconnected before the base failure path. |
| `IsConnected_TracksLowLevelConnection` | The default logical connection mirrors the low-level connection. | `IsConnected` is false while connecting, true when connected, and false once disconnecting begins. | Ack layers override `GetIsConnected` to add the handshake condition. |
| `FailHard_WhenConnected_StopsWithoutDisconnectedNotification` | A reliable failure does not raise `OnDisconnected` at this layer. | `FailHard` clears the low-level connection, notifies `OnStopped`, and leaves `DisconnectedCount` zero. | The reason instance is passed through unchanged. |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| `CanSendNow` override: connecting → `NotConnected`, connected → `Ok`, stop-requested → `NotConnected`. | This layer | The override adds the reliable-stage gate; the root tests cover only the base override. |
| `FinishConnect` `ifFailed` path while the reliable stage is disconnecting or disconnected. | This layer | Only the not-started failure path is covered. |
| `Send` rejection still prepends the payload frame byte. | This layer | The `Send` override has a documented side effect even when admission fails. |
| `IRawReliableEndpoint.Disconnect` returns `true`. | This layer | The call is asserted, but its return value is not. |
| An override of `OnConnected` that throws routes to `FailHard`. | This layer | The virtual connection hook's exception path is not covered. |

## Deferred by design

| Gap | Reason |
|---|---|
| Concrete client/server branches. | Covered by the child layers of this Abstract Test Suit. |
| Concurrency, real transports, sockets, and wall-clock timing. | The default suite is deterministic; these facets belong to a separate categorised layer or the transport/conformance suites. |
| Ack handshake and logical disconnect notifications. | Introduced by the ack child layers. |

## Notes

- The reliable stage machine is driven by the inherited raw lifecycle. `FinishConnect` calls the
  raw `FinishStart`; the raw start is therefore not complete until the connection is reported.
- A stop latched while connecting is deferred. Because the reliable `BeginStop` schedules its
  transition, a pending `FinishConnect` can still run first and report `OnConnected` before the
  teardown begins. This ordering is deliberate and is asserted by
  `Stop_WhileConnecting_ReportsConnectionThenTearsDown`.
- The manual queue is required for the deferred-stop ordering; tests never sleep.
- The layer intentionally omits inherited reject paths already owned by the root layer, except
  where this layer overrides the behaviour (for example `Send` framing).
