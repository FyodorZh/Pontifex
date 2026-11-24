# RawReliableHandshakeServerEndpoint Layer Coverage

- **Layer:** RawReliableHandshakeServerEndpoint
- **Subject:** `Pontifex.Raw.Reliable.RawReliableHandshakeServerEndpoint`
- **Directory:** `Tests/Pontifex.Tests/Endpoints/RawReliable/Server/RawReliableHandshakeServerEndpoint`
- **Inheritance parent:** `Pontifex.Raw.Reliable.RawReliableServerEndpoint`
- **Contract:** [RawReliableHandshakeServerEndpoint.cs](../../../../../../Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableHandshakeServerEndpoint.cs),
  [IRawReliableServerHandler.cs](../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableServerHandler.cs),
  [Specification.md](../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Connect_NotifiesHandler` | The logical connection is reported to the session handler once. | `OnConnected` receives the endpoint and `IsConnected` becomes true. | The harness sets the pending response before completing the connect. |
| `Connect_SendsPendingHandshakeResponseBeforeOnConnected` | The prebuilt handshake response is the first outbound frame. | The captured send is a `HandshakeResponseMessage` and the handler connection follows it. | The frame type is read through a symbol derived from the protected `MessageType` enum, not a hard-coded number. |
| `Connect_WithoutPendingResponse_StopsWithoutOnConnected` | A session whose handshake response cannot be sent is not admitted. | The handler receives no connection notification and the endpoint stops with `TextFail`. | Covers the missing-response branch required by the accept contract. |
| `PeriodicTick_WhenIdle_NotifiesDisconnectedThenStopped` | An idle session times out and delivers the documented callback sequence. | The `TimeOut` reason reaches `OnDisconnected` and `OnStopped` once the disconnect completes. | The ancestor layer already proves the timeout decision; this test covers the reliable notification delta. |
| `Stop_WhenConnected_NotifiesDisconnectedThenStopped` | A stop of a connected session delivers the documented callback sequence. | The reason reaches `OnDisconnected` and `OnStopped`, and the observed order is connected, disconnected, stopped. | The double calls the base `BeginDisconnect` before recording. |
| `Stop_WhileConnecting_DeliversStoppedWithoutConnectedNotification` | A stop latched while connecting prevents the handshake response from being admitted. | No connection or disconnect notification is raised, and the completed teardown delivers one stop. | The inherited response send fails with `NotConnected`, so the session is not admitted. |
| `FailHard_WhenConnected_NotifiesDisconnectedThenStopped` | A hard failure of a connected session notifies disconnection before stop. | `OnDisconnected` and `OnStopped` both receive the failure reason and `IsConnected` is false. | The `FailHard` override notifies before the base terminal stop notification. |
| `FailHard_WhenConnecting_NotifiesStoppedOnly` | A hard failure before the connection completes has no logical disconnect notification. | `OnDisconnected` is not raised; `OnStopped` receives the reason. | The connection notification latch is still clear. |
| `Disconnect_WhenConnected_MakesIsConnectedFalseDuringOnDisconnected` | `IsConnected` is false during the handler's disconnect notification. | The probe reads false from inside `OnDisconnected`. | The disconnect latch is set before the callback; the inherited low-level stage is already disconnecting. |
| `OnConnected_WhenHandlerThrows_LogsAndKeepsEndpointConnected` | A handler exception during the connection notification is isolated. | `OnConnected` was observed once, no disconnect or stop follows, and the endpoint stays connected. | The connection latch is set before the handler call; the throwing callback no longer fails the endpoint or skips the watchdog. |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| A `FailHard` before the start attempt notifies no reliable callback. | This layer | The constructed-stage hard failure is owned by the root layer; the reliable-specific latch state is not asserted. |

## Deferred by design

| Gap | Reason |
|---|---|
| A real periodic driver, timer, or transport ticker. | `PeriodicTick` is driven directly; scheduling is out of scope. |
| Default `DateTime.Now` clock and real-time timeout behaviour. | Tests use the `Now` seam for determinism. |
| Real transport session admission and teardown. | Covered by the transport suites and the carrier integration tests. |
| `RawReliable` conformance controls and suite. | Conformance controls and the conformance suite are deferred. |

## Notes

- The test double's `BeginDisconnect` calls the reliable base implementation before recording, which is what raises the logical disconnect notification.
- `SetHandshakeResponse` builds the pending response from the harness message maximum; it must be called before `CompleteConnect`, because the endpoint sends it from `OnConnected` and stops without admission when it is missing or unsendable.
- The recording handler re-implements `IRawReliableHandler.OnDisconnected` explicitly because the inherited recording member is not virtual; the explicit implementation evaluates the `IsConnected` probe before recording the callback.
- The disconnect latch and the once-only notification helper live on `RawReliableEndpoint` (`NotifyDisconnectedCore`); this layer's `FailHard` and `BeginDisconnect` hooks only delegate to it.
