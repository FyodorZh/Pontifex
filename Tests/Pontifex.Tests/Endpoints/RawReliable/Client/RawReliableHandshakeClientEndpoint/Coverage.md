# RawReliableHandshakeClientEndpoint Layer Coverage

- **Layer:** RawReliableHandshakeClientEndpoint
- **Subject:** `Pontifex.Raw.Reliable.RawReliableHandshakeClientEndpoint`
- **Directory:** `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/RawReliableHandshakeClientEndpoint`
- **Inheritance parent:** `Pontifex.Raw.Reliable.RawReliableClientEndpoint`
- **Contract:** [RawReliableHandshakeClientEndpoint.cs](../../../../../../Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableHandshakeClientEndpoint.cs),
  [IRawReliableClientHandler.cs](../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Client/IRawReliableClientHandler.cs),
  [Specification.md](../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Connect_NotifiesHandlerAndSetsIsConnected` | The logical connection is reported to the handler once. | `OnConnected` receives the endpoint and `IsConnected` becomes true. | The handshake response must be supplied before the connection is reported; the harness feeds it explicitly. |
| `Connect_SendsHandshakeRequestBeforeOnConnected` | The first outbound frame is the transport handshake request. | The captured send is a `HandshakeRequestMessage` and the handler connection follows the response. | The frame type is read through a symbol derived from the protected `MessageType` enum, not a hard-coded number. |
| `Stop_WhenConnected_NotifiesDisconnectedThenStopped` | A stop of a connected endpoint delivers the documented callback sequence. | The reason reaches `OnDisconnected` and `OnStopped`, and the observed order is connected, disconnected, stopped. | The double calls the base `BeginDisconnect` before recording. |
| `Stop_WhileConnecting_DeliversStoppedWithoutConnectedNotification` | A stop latched while connecting prevents the handshake from admitting the session. | No connection or disconnect notification is raised, and the completed teardown delivers one stop. | The pending handshake response is never accepted, so the connected latch stays clear. |
| `FailHard_WhenConnected_NotifiesDisconnectedThenStopped` | A hard failure of a connected endpoint notifies disconnection before stop. | `OnDisconnected` and `OnStopped` both receive the failure reason, in order, and `IsConnected` is false. | The `FailHard` override notifies before the base terminal stop notification. |
| `FailHard_WhenConnecting_NotifiesStoppedOnly` | A hard failure before the connection completes has no logical disconnect notification. | `OnDisconnected` is not raised; `OnStopped` receives the reason. | The connection notification latch is still clear. |
| `Disconnect_WhenConnected_MakesIsConnectedFalseDuringOnDisconnected` | `IsConnected` is false during the handler's disconnect notification. | The probe reads false from inside `OnDisconnected`. | The disconnect latch is set before the callback; the inherited low-level stage is already disconnecting. |
| `OnConnected_WhenHandlerThrows_StopsWithExceptionFail` | A handler exception during the connection notification stops the endpoint. | `OnConnected` was observed once, then `OnDisconnected` and `OnStopped` receive `ExceptionFail`, and keep-alive is not started. | The connection latch is set before the handler call, so the throwing path still delivers the disconnect notification. |
| `HandshakeTimeout_WithoutResponse_StopsWithTimeOutAndNeverConnects` | The handshake deadline stops an endpoint whose response never arrives. | The manual timer is fired, the endpoint stops with `TimeOut`, and no connection or disconnect notification is raised. | The deadline is driven by the manual timer seam, so the test is deterministic. |
| `HandshakeResponse_StopsTimer_AndConnectionStaysHealthy` | A successful response cancels the deadline. | The timer is disposed, the endpoint stays connected, and a late timer fire does nothing. | Proves the timer is released on success rather than only on teardown. |
| `HandshakeResponse_WithVersionMismatch_StopsWithProtocolMismatch` | A response from a different protocol version is a protocol mismatch. | The endpoint stops with `ProtocolMismatch` and never connects. | — |
| `HandshakeResponse_WithNameMismatch_StopsWithProtocolMismatch` | A response from a different handshake name is a protocol mismatch. | The endpoint stops with `ProtocolMismatch` and never connects. | — |
| `HandshakeResponse_WithNonPositiveMaximum_StopsWithProtocolMismatch` | A negotiated maximum below 1 is malformed. | The endpoint stops with `ProtocolMismatch` and never connects. | The parser accepts the shape; the endpoint applies the semantic `serverMax < 1` guard. |
| `HandshakeResponse_Malformed_StopsWithProtocolMismatch` | A response that does not parse is a protocol mismatch. | The endpoint stops with `ProtocolMismatch` and never connects. | The frame type is valid; the payload is truncated. |
| `HandshakeResponse_Oversized_StopsWithTextFail` | A response above `HandshakeMaxByteSize` is rejected before parsing. | The endpoint stops with `TextFail` and never connects. | — |
| `HandshakeReject_MapsStatusToStopReason` | The reject status table maps to typed stop reasons. | Version/name/unknown map to `ProtocolMismatch`, rejected/capacity to `HandshakeRejected`, and timeout to `TimeOut`. | One parameterized row covers the six statuses. |
| `HandshakeReject_MapsNameMismatchToProtocolMismatch` | A protocol mismatch reject maps to the typed stop reason. | A `NameMismatch` reject stops the endpoint with `ProtocolMismatch` and no connection notification. | The reject status mapping is owned by this layer. |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| A `FailHard` before the start attempt notifies no reliable callback. | This layer | The constructed-stage hard failure is owned by the root layer; the reliable-specific latch state is not asserted. |

## Deferred by design

| Gap | Reason |
|---|---|
| The inherited keep-alive background loop. | Keep-alive is disabled in the reliable client double; the loop wrapper uses real time and is out of scope. |
| Real transport connect and teardown. | Covered by the transport suites and the carrier integration tests. |
| Real timing and socket failures. | The suite uses deterministic seams. |
| `RawReliable` conformance controls and suite. | Conformance controls and the conformance suite are deferred. |

## Notes

- The test double's `BeginDisconnect` calls the reliable base implementation before recording, which is what raises the logical disconnect notification.
- The test double injects a `ManualHandshakeTimer` through `CreateHandshakeTimer`; the timeout test fires it explicitly and the success test proves it was disposed.
- `CompleteHandshake` builds a response with the harness handshake name and the configured message maximum, prepends the symbolic response frame type, and feeds it through `ExposedProcessInbound`.
- The recording handler re-implements `IRawReliableHandler.OnDisconnected` explicitly because the inherited recording member is not virtual; the explicit implementation evaluates the `IsConnected` probe before recording the callback.
- The recording handler records the connection before honouring `ThrowOnConnected`, so the throwing path is observable as one connection followed by disconnect and stop.
- The disconnect latch and the once-only notification helper live on `RawReliableEndpoint` (`NotifyDisconnectedCore`); this layer's `FailHard` and `BeginDisconnect` hooks only delegate to it.
