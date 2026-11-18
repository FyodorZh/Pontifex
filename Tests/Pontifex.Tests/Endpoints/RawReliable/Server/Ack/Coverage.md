# RawReliableAckServerEndpoint Layer Coverage

- **Layer:** RawReliableAckServerEndpoint
- **Subject:** `Pontifex.Raw.Reliable.Ack.RawReliableAckServerEndpoint`
- **Directory:** `Tests/Pontifex.Tests/Endpoints/RawReliable/Server/Ack`
- **Inheritance parent:** `Pontifex.Raw.Reliable.RawReliableServerEndpoint`
- **Contract:** [RawReliableAckServerEndpoint.cs](../../../../../../Pontifex/Implementations/Endpoints/Raw/Reliable/Ack/Server/RawReliableAckServerEndpoint.cs),
  [RawReliableAckInfo.cs](../../../../../../Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckInfo.cs),
  [IRawReliableAckServerHandler.cs](../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Server/IRawReliableAckServerHandler.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `StartWithoutFinishConnect_DoesNotSendOrNotifyHandler` | The pre-built response is sent only after the low-level connection is ready. | Starting without completing the connection sends nothing and notifies no handler; completing it sends exactly one framed response, calls `OnConnected` once, and releases the pending response. | The low-level connection stays down until `CompleteConnect`. The pending response is asserted dead before harness disposal. |
| `Connect_SendsPendingAckResponseBeforeOnConnected` | The response is admitted for outbound delivery before `OnConnected` runs. | One framed response carries the supplied payload, the handler is notified once, and the handler observes a pending outbound message inside the callback. | The pending-message probe reads the admission counter exposed through `OnPendingSendChanged`; captured sends are only filled after the drain. |
| `Connect_WhenResponseCannotBeAdmitted_StopsWithTextFail` | A response that cannot be admitted aborts the handshake. | No frame is captured, `OnConnected` is not called, and the endpoint stops with `TextFail`. | The capacity is set below the framed response size; the payload byte keeps the case explicitly about response payload size. |
| `Connect_WhenResponseExceedsAckMaxByteSize_StopsWithTextFail` | The framed ACK response must not exceed `RawReliableAckInfo.AckMaxByteSize`. | The oversize response is rejected before admission with `TextFail`, and the handler is stopped once. | The 4096-byte protocol limit is assumed independently of the harness `messageMaxByteSize` default. |
| `UnexpectedAckRequest_StopsWithTextFail` | The endpoint does not handle inbound ACK requests. | A stray request frame is an unsupported frame and stops the endpoint with `TextFail`, not `ExceptionFail`. | The frame is rejected by the inherited reliable-endpoint dispatch; this layer adds no request handling. |
| `OnConnected_Throws_StopsWithExceptionFailAndNoOnDisconnected` | A handler exception from `OnConnected` aborts the session. | The endpoint stops with `ExceptionFail`, delivers no `OnDisconnected`, and delivers exactly one `OnStopped`. | The handler already received `OnConnected`, so the terminal notification is deliberately not preceded by a logical disconnect. |
| `NoPendingAckResponse_StopsWithTextFail` | A missing pre-built response is a configuration failure. | The endpoint sends nothing, never calls `OnConnected`, and stops with `TextFail`. | Covers the endpoint's defensive stop instead of leaving the client to wait for its watchdog. |
| `StopBeforeConnect_ReleasesPendingAckResponse` | A terminal stop before the connection completes releases the unsent response. | The pending response is no longer alive after the stop is drained. | Stopping in the constructed stage raises the inherited `Stopped` event immediately; no start is attempted, so the handler is not notified. |
| `Disconnect_WhenLogicallyConnected_NotifiesOnDisconnected` | A stop of a logically connected endpoint notifies `OnDisconnected` once. | The reason reaches `OnDisconnected` and `OnStopped`, and `IsConnected` is false. | The ack double calls the base `BeginDisconnect` before recording. `ConnectAndComplete` already completes the handshake. |
| `FailHard_WhenLogicallyConnected_NotifiesDisconnectedThenStopped` | A hard failure of a logically connected endpoint notifies disconnection before stop. | `OnDisconnected` and `OnStopped` both receive the failure reason. | `IsConnected` is already false when the notification runs. |
| `FailHard_WhenHandshakePending_DoesNotNotifyOnDisconnected` | A hard failure before the response is delivered has no logical disconnect notification. | `OnDisconnected` is not raised, and `OnStopped` receives the reason exactly once. | The endpoint is started but not connected, so the logical stage is `Constructed`. |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| — | — | No known missing tests for this layer. |

## Deferred by design

| Gap | Reason |
|---|---|
| A real periodic driver, timer, or transport ticker. | `PeriodicTick` is driven directly; scheduling is out of scope. |
| Default `DateTime.Now` clock and real-time timeout behaviour. | Tests use the `Now` seam for determinism. |
| Real transport handshake and peer interaction. | Covered by the transport/conformance suites. |
| Releasing the response when a transport rejects a created endpoint. | Carrier ownership transfer is verified by the carrier suites, not by this layer. |

## Notes

- The endpoint never receives or releases the client's acknowledgement data. It is constructed with an
  already-built ACK response, owns that response, and releases it on every path, whether it was sent
  or not.
- A duplicate or stray ACK request is an unsupported frame, so the inherited reliable-endpoint dispatch
  stops the endpoint with `TextFail`. It is not a handshake-path `ExceptionFail`.
- A handler exception from `OnConnected` is still `ExceptionFail`; the response is admitted first and
  the handler is not told about a disconnect because it had already received `OnConnected`.
- The test double's `BeginDisconnect` calls the ack base implementation before recording, which is what
  raises the logical disconnect notification.
- The test double releases any still-pending response in `Dispose` as a safety net for endpoints that
  are never stopped; the dedicated stop test asserts the production release before disposal.
