# RawReliableAckServerEndpoint Layer Coverage

- **Layer:** RawReliableAckServerEndpoint
- **Subject:** `Pontifex.Raw.Reliable.Ack.RawReliableAckServerEndpoint`
- **Directory:** `Tests/Pontifex.Tests/Endpoints/RawReliable/Server/Ack`
- **Inheritance parent:** `Pontifex.Raw.Reliable.RawReliableServerEndpoint`
- **Contract:** [RawReliableAckServerEndpoint.cs](../../../../../../Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckServerEndpoint.cs),
  [RawReliableAckInfo.cs](../../../../../../Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckInfo.cs),
  [IRawReliableAckServerHandler.cs](../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Server/IRawReliableAckServerHandler.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Connect_DoesNotSendAnythingUntilAckRequest` | The server does not begin the handshake on its own. | Connecting sends nothing, `FillAckResponse` is not called, and logical `IsConnected` stays false. | The low-level connection is already up. |
| `AckRequest_SendsAckResponseAndCompletesHandshake` | The first ack request triggers the response and logical connection. | `FillAckResponse` runs once, one framed response is transmitted, `OnConnected` runs once, and `IsConnected` becomes true. | The handler response payload appears after the frame type byte. |
| `AckRequest_WhenHandshakeAlreadyComplete_StopsWithExceptionFail` | A second ack request is a protocol violation. | The endpoint stops with `ExceptionFail`; the terminal stop follows `FinishDisconnect`. | This is a normal `Stop` with an `ExceptionFail` reason, not the `FailHard` hook. |
| `FillAckResponse_Throws_StopsWithExceptionFail` | A handler exception while filling the response aborts the handshake. | No `OnConnected` occurs and the stop reason is `ExceptionFail`. | The handler exception is wrapped with the exception instance. |
| `AckRequest_WhenAckResponseCannotBeAdmitted_StopsWithTextFail` | An ack response that cannot be admitted aborts the handshake. | No frame is captured, the endpoint stops with `TextFail`, and `OnConnected` is not called. | Capacity is set below the framed response size. |
| `Disconnect_WhenLogicallyConnected_NotifiesOnDisconnected` | A stop of a logically connected endpoint notifies `OnDisconnected` once. | The reason reaches `OnDisconnected` and `OnStopped`, and `IsConnected` is false. | The ack double calls the base `BeginDisconnect` before recording. |
| `Disconnect_WhenHandshakePending_DoesNotNotifyOnDisconnected` | A stop before the handshake completes has no logical disconnect notification. | `OnDisconnected` is not raised; only `OnStopped` follows. | The stage is `Constructed`, not `LogicallyConnected`. |
| `FailHard_WhenLogicallyConnected_NotifiesDisconnectedThenStopped` | A hard failure of a logically connected endpoint notifies disconnection before stop. | `OnDisconnected` and `OnStopped` both receive the failure reason. | `IsConnected` is already false when the notification runs. |
| `FailHard_WhenHandshakePending_DoesNotNotifyOnDisconnected` | A hard failure before the handshake completes has no logical disconnect notification. | `OnDisconnected` is not raised; `OnStopped` receives the reason. | — |
| `AckRequest_RefreshesWatchdog` | An ack request refreshes the idle-session watchdog. | A tick past the original deadline does nothing because the request reset the timestamp. | The refresh happens before the request is validated for size. |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| An inbound ack request above `RawReliableAckInfo.AckMaxByteSize` stops with `TextFail`. | This layer | The request oversize guard is not covered. |
| `FillAckResponse` producing data above `AckMaxByteSize` stops with `TextFail`. | This layer | The response oversize guard is not covered. |
| `OnConnected` handler throwing stops with `ExceptionFail`. | This layer | The handler callback exception path is not covered. |
| An ack request received while logically disconnected is released silently. | This layer | The disconnected-stage branch is not covered. |

## Deferred by design

| Gap | Reason |
|---|---|
| A real periodic driver, timer, or transport ticker. | `PeriodicTick` is driven directly; scheduling is out of scope. |
| Default `DateTime.Now` clock and real-time timeout behaviour. | Tests use the `Now` seam for determinism. |
| Real transport handshake and peer interaction. | Covered by the transport/conformance suites. |

## Notes

- Handler exceptions during `FillAckResponse` are `ExceptionFail` (not `TextFail`); other internal
  failures in the ack-response path may be `TextFail`.
- The second-request guard throws inside the scheduled handshake action; that exception is caught
  and converted to `Stop(ExceptionFail)`, so this path does not use `FailHard`.
- The ack request refreshes the inherited server watchdog before the size check, so even an
  oversized request counts as inbound activity.
- The test double's `BeginDisconnect` calls the ack base implementation before recording, which
  is what raises the logical disconnect notification.
