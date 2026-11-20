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
| `Connect_NotifiesHandlerAndSetsIsConnected` | The logical connection is reported to the handler once. | `OnConnected` receives the endpoint and `IsConnected` becomes true. | The reliable transport layer owns the logical connection; this layer reports it. |
| `Stop_WhenConnected_NotifiesDisconnectedThenStopped` | A stop of a connected endpoint delivers the documented callback sequence. | The reason reaches `OnDisconnected` and `OnStopped`, and the observed order is connected, disconnected, stopped. | The double calls the base `BeginDisconnect` before recording. |
| `Stop_WhileConnecting_ReportsConnectionThenTearsDown` | A stop latched while connecting still reports the connection first. | `OnConnected` and `OnDisconnected` run once, and `OnStopped` follows the completed disconnect. | The inherited reliable stage reports the connection before teardown. |
| `FailHard_WhenConnected_NotifiesDisconnectedThenStopped` | A hard failure of a connected endpoint notifies disconnection before stop. | `OnDisconnected` and `OnStopped` both receive the failure reason, in order, and `IsConnected` is false. | The `FailHard` override notifies before the base terminal stop notification. |
| `FailHard_WhenConnecting_NotifiesStoppedOnly` | A hard failure before the connection completes has no logical disconnect notification. | `OnDisconnected` is not raised; `OnStopped` receives the reason. | The connection notification latch is still clear. |
| `Disconnect_WhenConnected_MakesIsConnectedFalseDuringOnDisconnected` | `IsConnected` is false during the handler's disconnect notification. | The probe reads false from inside `OnDisconnected`. | The disconnect latch is set before the callback; the inherited low-level stage is already disconnecting. |
| `OnConnected_WhenHandlerThrows_StopsWithExceptionFail` | A handler exception during the connection notification stops the endpoint. | `OnConnected` was observed once, then `OnDisconnected` and `OnStopped` receive `ExceptionFail`, and keep-alive is not started. | The connection latch is set before the handler call, so the throwing path still delivers the disconnect notification. |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| An `OnDisconnected` handler exception is logged and swallowed, so the stop still completes. | This layer | The exception isolation in the disconnect notification is not covered. |
| A `FailHard` before the start attempt notifies no reliable callback. | This layer | The constructed-stage hard failure is owned by the root layer; the reliable-specific latch state is not asserted. |

## Deferred by design

| Gap | Reason |
|---|---|
| The inherited keep-alive background loop. | Keep-alive is disabled in the reliable client double; the loop wrapper uses real time and is out of scope. |
| Real transport connect and teardown. | Covered by the transport suites and the carrier integration tests. |
| Real timing and socket failures. | The suite uses deterministic seams. |
| A Direct `RawReliable` carrier. | Only the TCP `tcp-noack` carrier exists in this stage. |
| `RawReliable` conformance controls and suite. | Conformance controls and the conformance suite are deferred. |

## Notes

- The test double's `BeginDisconnect` calls the reliable base implementation before recording, which is what raises the logical disconnect notification.
- The recording handler re-implements `IRawReliableHandler.OnDisconnected` explicitly because the inherited recording member is not virtual; the explicit implementation evaluates the `IsConnected` probe before recording the callback.
- The recording handler records the connection before honouring `ThrowOnConnected`, so the throwing path is observable as one connection followed by disconnect and stop.
- The disconnect latch and the once-only notification helper live on `RawReliableEndpoint` (`NotifyDisconnectedCore`); this layer's `FailHard` and `BeginDisconnect` hooks only delegate to it.
