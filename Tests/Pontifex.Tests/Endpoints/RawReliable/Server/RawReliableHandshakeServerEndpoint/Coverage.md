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
| `Connect_NotifiesHandler` | The logical connection is reported to the session handler once. | `OnConnected` receives the endpoint and `IsConnected` becomes true. | The inherited watchdog start is not re-tested here; the ancestor layer owns it. |
| `PeriodicTick_WhenIdle_NotifiesDisconnectedThenStopped` | An idle session times out and delivers the documented callback sequence. | The `TimeOut` reason reaches `OnDisconnected` and `OnStopped` once the disconnect completes. | The ancestor layer already proves the timeout decision; this test covers the reliable notification delta. |
| `Stop_WhenConnected_NotifiesDisconnectedThenStopped` | A stop of a connected session delivers the documented callback sequence. | The reason reaches `OnDisconnected` and `OnStopped`, and the observed order is connected, disconnected, stopped. | The double calls the base `BeginDisconnect` before recording. |
| `Stop_WhileConnecting_ReportsConnectionThenTearsDown` | A stop latched while connecting still reports the connection first. | The observed order is connected then disconnected before the disconnect completes, and stopped afterwards. | The inherited reliable stage reports the connection before teardown; the harness exposes a public `CompleteConnect`. |
| `FailHard_WhenConnected_NotifiesDisconnectedThenStopped` | A hard failure of a connected session notifies disconnection before stop. | `OnDisconnected` and `OnStopped` both receive the failure reason and `IsConnected` is false. | The `FailHard` override notifies before the base terminal stop notification. |
| `FailHard_WhenConnecting_NotifiesStoppedOnly` | A hard failure before the connection completes has no logical disconnect notification. | `OnDisconnected` is not raised; `OnStopped` receives the reason. | The connection notification latch is still clear. |
| `Disconnect_WhenConnected_MakesIsConnectedFalseDuringOnDisconnected` | `IsConnected` is false during the handler's disconnect notification. | The probe reads false from inside `OnDisconnected`. | The disconnect latch is set before the callback; the inherited low-level stage is already disconnecting. |
| `OnConnected_WhenHandlerThrows_StopsWithExceptionFail` | A handler exception during the connection notification stops the endpoint. | `OnConnected` was observed once, then `OnDisconnected` and `OnStopped` receive `ExceptionFail`, and the watchdog is not started. | The connection latch is set before the handler call, so the throwing path still delivers the disconnect notification. |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| An `OnDisconnected` handler exception is logged and swallowed, so the stop still completes. | This layer | The exception isolation in the disconnect notification is not covered. |
| A `FailHard` before the start attempt notifies no reliable callback. | This layer | The constructed-stage hard failure is owned by the root layer; the reliable-specific latch state is not asserted. |

## Deferred by design

| Gap | Reason |
|---|---|
| A real periodic driver, timer, or transport ticker. | `PeriodicTick` is driven directly; scheduling is out of scope. |
| Default `DateTime.Now` clock and real-time timeout behaviour. | Tests use the `Now` seam for determinism. |
| Real transport session admission and teardown. | Covered by the transport suites and the carrier integration tests. |
| A Direct `RawReliable` carrier. | Only the TCP `tcp-noack` carrier exists in this stage. |
| `RawReliable` conformance controls and suite. | Conformance controls and the conformance suite are deferred. |

## Notes

- The test double's `BeginDisconnect` calls the reliable base implementation before recording, which is what raises the logical disconnect notification.
- `TestRawReliableServerEndpoint.CompleteConnect` is private in the ancestor harness, so this layer's double exposes its own public `CompleteConnect`.
- The recording handler re-implements `IRawReliableHandler.OnDisconnected` explicitly because the inherited recording member is not virtual; the explicit implementation evaluates the `IsConnected` probe before recording the callback.
- The disconnect latch and the once-only notification helper live on `RawReliableEndpoint` (`NotifyDisconnectedCore`); this layer's `FailHard` and `BeginDisconnect` hooks only delegate to it.
