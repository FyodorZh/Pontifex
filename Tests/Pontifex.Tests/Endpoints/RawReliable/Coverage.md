# RawReliableEndpoint Layer Coverage

- **Layer:** RawReliableEndpoint
- **Subject:** `Pontifex.Raw.Reliable.RawReliableEndpoint`
- **Directory:** `Tests/Pontifex.Tests/Endpoints/RawReliable`
- **Inheritance parent:** `Pontifex.Raw.RawEndpoint`
- **Contract:** [RawReliableEndpoint.cs](../../../../Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableEndpoint.cs),
  [IRawReliableEndpoint.cs](../../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableEndpoint.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Start_ReachesConnectingAndCallsBeginConnect` | `BeginStart` moves the reliable stage to `Connecting` and calls `BeginConnect`. | Before completion the reliable observables are false and `BeginConnect` was called once. | `Start` is admitted but the raw start remains incomplete until `FinishConnect`. |
| `FinishConnect_WhenConnecting_CompletesConnection` | `FinishConnect` completes the raw start and the reliable connection. | `OnConnected` runs once and `IsLowLevelConnected`, `IsConnected`, `IsRunning` become true. | The double records `OnConnected`; alias identity is not relevant. |
| `FinishConnect_WithoutStart_FailsAndDoesNotConnect` | `FinishConnect` requires a start in progress. | The `ifFailed` callback runs, no connection is reported, and the endpoint remains unconnected. | The raw `FinishStart` failure is what makes `ifFailed` run. |
| `Stop_WhileConnecting_ReportsConnectionThenTearsDown` | A stop latched while connecting still reports the completed connection before teardown. | `OnConnected` runs once, then `BeginDisconnect` is called with the stop reason; the handler stop arrives after `FinishDisconnect`. | Ordering relies on the reliable `BeginStop` scheduling its work rather than running inline. |
| `Send_WhenConnected_FramesPayloadMessage` | A connected send prepends `PayloadMessage`. | The first element is the payload frame type and the original content follows. | The payload type byte is endpoint-owned framing, not application data. |
| `Send_PayloadAtExactMessageMaxByteSize_IsAccepted` | Transport framing is excluded from `MessageMaxByteSize`. | A payload exactly at the maximum is admitted and the framed buffer is larger than the payload. | The payload size is measured before `PutFirst`. |
| `Send_OneByteOverMessageMaxByteSize_ReturnsMessageTooBig` | The payload limit is inclusive. | One byte over the maximum is rejected and released. | Uses a three-byte limit. |
| `ProcessInbound_PayloadFrame_DeliversPayloadWithoutTypeByte` | A payload frame is delivered with its type byte stripped. | The handler receives only the application payload. | The frame builder puts the type byte first and the payload after it. |
| `ProcessInbound_DisconnectFrame_StopsWithGracefulRemoteIntention` | A remote disconnect frame initiates a graceful remote stop. | `BeginDisconnect` runs with `GracefulRemoteIntention` and the frame is not delivered. | The handler stop is observed only after `FinishDisconnect`. |
| `ProcessInbound_UnhandledFrameType_IsDroppedWithoutStopping` | Frame types not handled by this layer are logged and dropped. | Invalid, ping, pong, handshake-request, handshake-response, handshake-reject, and unknown frame types release the frame and leave the session connected. | Seven test cases share one contract row; derived layers override ping/pong/handshake handling. |
| `ProcessInbound_WhenNotConnected_IsDroppedWithoutStopping` | Inbound frames require a low-level connection. | The frame is released, not delivered, and the endpoint stays alive. | A later connect attempt is unaffected. |
| `ProcessInbound_EmptyFrame_IsDroppedWithoutStopping` | Empty inbound data has no frame type. | The frame is released and logged; the session stays connected. | The empty list is released by the dispatch path. |
| `ProcessInbound_PayloadBeforeOnConnectedCompleted_IsBufferedThenDeliveredInOrder` | A payload that arrives before `OnConnected` completes is buffered, not dropped. | Two payloads injected during the callback are delivered in order after it returns. | The harness disables the automatic latch and invokes the notification core itself. |
| `ProcessInbound_PayloadConcurrentWithLatchCompletion_IsDeliveredExactlyOnceInOrder` | A payload arriving from another thread while the latch completes is neither lost nor duplicated. | Two payloads are delivered once each in arrival order. | The second payload is injected from a task whose completion the callback waits for. |
| `ProcessInbound_WhenOnConnectedThrows_StillDrainsBufferedPayloads` | A throwing handler callback is isolated and does not discard accepted payloads. | The buffered payload is delivered after the callback throws and the endpoint stays running. | The exception is logged by `NotifyConnectedCore`. |
| `ProcessInbound_PreConnectBufferOverflow_ReleasesEverythingAndStops` | The pre-connect cap is enforced. | The buffered payload and the triggering payload are released and the session stops with `TextFail`. | The message cap is overridden to one. |
| `ProcessInbound_StopDuringOnConnected_ReleasesBufferedPayloadWithoutDelivery` | A stop during the callback suppresses buffered delivery. | The payload is released and the handler observes the supplied stop reason. | The stop request is issued from inside `OnConnected`. |
| `ProcessInbound_PreConnectPayloadWithMaxIntMessageLimit_IsAcceptedAndDelivered` | The pre-connect byte cap uses saturating arithmetic. | A payload is accepted and delivered when `MessageMaxByteSize` is `int.MaxValue`. | Proves the cap does not overflow to a tiny value. |
| `DisconnectGracefully_SendsDisconnectFrameAndStopsGently` | `DisconnectGracefully` frames a disconnect message and stops with `UserIntention`. | The disconnect frame is transmitted, `BeginDisconnect` runs, and the stop is gentle. | The reason source is the transport name. |
| `Disconnect_InterfaceInitiatesGentleStop` | `IRawReliableEndpoint.Disconnect` initiates a gentle stop with the caller's reason. | `BeginDisconnect` receives the same reason and `tryBeGentle: true`. | The interface returns `true`; the return value is not asserted here. |
| `DisconnectGracefully_WhenDisconnectFrameCannotBeAdmitted_StopsUngently` | A disconnect frame that cannot be admitted forces an ungentle stop. | No frame is captured, `BeginDisconnect` still runs, and `tryBeGentle` is false. | Capacity is set below the frame size to force `BufferOverflow`. |
| `DisconnectGracefully_UsesTransportNameAsReasonSource` | The graceful-disconnect reason is sourced from the transport name. | The reason's `Source` matches the configured transport name. | — |
| `Stop_WhileConnected_DisconnectsBeforeNotifyingStopped` | A connected stop enters `Disconnecting` before the handler stop. | The reliable observables are false and no handler stop occurs until `FinishDisconnect`. | `BeginDisconnect` completes the teardown explicitly. |
| `DisconnectCycle_DoesNotInvokeOnDisconnected` | This layer does not raise `OnDisconnected` itself. | A payload delivery followed by a stop produces only `OnReceived` and `OnStopped`. | Only the handshake subclasses add the logical disconnect notification. |
| `FinishDisconnect_WhenNotDisconnecting_FailsHard` | `FinishDisconnect` is legal only while disconnecting. | An invalid completion fails hard with `ExceptionFail`. | The reliable stage is moved to disconnected before the base failure path. |
| `IsConnected_TracksLowLevelConnection` | The default logical connection mirrors the low-level connection until the logical disconnect notification is delivered. | `IsConnected` is false while connecting, true when connected, and false once disconnecting begins. | The reliable handshake layer overrides `GetIsConnected` to add the handshake condition. |
| `FailHard_WhenConnected_StopsWithoutDisconnectedNotification` | A reliable failure does not raise `OnDisconnected` at this layer. | `FailHard` clears the low-level connection, notifies `OnStopped`, and leaves `DisconnectedCount` zero. | The reason instance is passed through unchanged. |
| `NotifyDisconnected_WhenConnected_NotifiesOnce` | `NotifyDisconnectedCore` delivers the handler's logical disconnect at most once, after the connected latch is armed. | Two consecutive calls produce one `OnDisconnected` with the same reason. | The latch is armed explicitly by `MarkConnectedNotified`. |
| `NotifyDisconnected_WhenNeverConnected_DoesNotNotify` | The logical disconnect requires that the handler observed the connection. | A call without an armed connected latch leaves `DisconnectedCount` zero. | This protects a session that fails before `OnConnected`. |
| `NotifyDisconnected_WhenHandlerThrows_DoesNotThrowOutOfTheEndpoint` | A handler exception during the logical disconnect is logged and swallowed. | The throwing handler is called once and no exception escapes the helper. | The throwing double is fixture-local; the layer recorders cannot throw. |
| `IsConnected_AfterDisconnectNotification_IsFalse` | The default predicate consults the disconnect latch, so `OnDisconnected` observes a disconnected endpoint. | While the low-level connection is still up, `IsConnected` is false inside the callback and after it. | Uses a fixture-local probe handler that captures `IsConnected` inside `OnDisconnected`. |
| `NotifyConnectedCore_WhenCallbackThrows_LogsAndKeepsEndpointUsable` | A throwing connection callback is isolated. | The helper returns true, logs the exception, keeps the endpoint connected and running, and later deliveries still reach the handler. | The connected latch stays armed, so no disconnect or stop notification follows. |
| `Send_WhenStoppedWithNullBuffer_ReturnsNotConnected` | Section 9 precedence: endpoint state before message validity. | A stopped endpoint reports `NotConnected` for a null buffer instead of `InvalidMessage`. | The state check does not dereference the buffer. |
| `Send_WhenStoppedWithOversizedBuffer_ReturnsNotConnectedAndReleases` | Section 9 precedence: endpoint state before message size. | A stopped endpoint reports `NotConnected` for an oversized buffer, and the live buffer is released exactly once. | The frame byte is not prepended on the state-rejection path. |
| `Send_WhenStoppedWithFullBudget_ReturnsNotConnectedAndReleases` | Section 9 precedence: endpoint state before outbound capacity. | A stopped endpoint reports `NotConnected` even when the local byte budget could not admit the buffer, and releases the buffer. | Capacity is smaller than the serialized payload. |
| `Send_WhenConnectedWithNullBuffer_ReturnsInvalidMessage` | A null outbound buffer is invalid while the endpoint is running. | A connected endpoint reports `InvalidMessage`. | Ownership stays with the caller. |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| `CanSendNow` override: connecting → `NotConnected`, connected → `Ok`, stop-requested → `NotConnected`. | This layer | The override adds the reliable-stage gate; the root tests cover only the base override. |
| `FinishConnect` `ifFailed` path while the reliable stage is disconnecting or disconnected. | This layer | Only the not-started failure path is covered. |
| `Send` rejection still prepends the payload frame byte. | This layer | The `Send` override has a documented side effect even when admission fails. |
| `IRawReliableEndpoint.Disconnect` returns `true`. | This layer | The call is asserted, but its return value is not. |

## Deferred by design

| Gap | Reason |
|---|---|
| Concrete client/server branches. | Covered by the child layers of this Abstract Test Suit. |
| Concurrency, real transports, sockets, and wall-clock timing. | The default suite is deterministic; these facets belong to a separate categorised layer or the transport/conformance suites. |
| Transport handshake and logical disconnect notifications. | Introduced by the handshake child layers. |

## Notes

- The reliable stage machine is driven by the inherited raw lifecycle. `FinishConnect` calls the
  raw `FinishStart`; the raw start is therefore not complete until the connection is reported.
- A stop latched while connecting is deferred. Because the reliable `BeginStop` schedules its
  transition, a pending `FinishConnect` can still run first and report `OnConnected` before the
  teardown begins. This ordering is deliberate and is asserted by
  `Stop_WhileConnecting_ReportsConnectionThenTearsDown`.
- The manual queue is required for the deferred-stop ordering; tests never sleep.
- `Send` selects the result state-first: endpoint state, then buffer validity, then size, then
  capacity. A handler exception in `NotifyConnectedCore` is logged and isolated instead of stopping the
  endpoint.
- The layer intentionally omits inherited reject paths already owned by the root layer, except
  where this layer overrides the behaviour (for example `Send` framing).
