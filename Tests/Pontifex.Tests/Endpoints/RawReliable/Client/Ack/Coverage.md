# RawReliableAckClientEndpoint Layer Coverage

- **Layer:** RawReliableAckClientEndpoint
- **Subject:** `Pontifex.Raw.Reliable.Ack.RawReliableAckClientEndpoint`
- **Directory:** `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/Ack`
- **Inheritance parent:** `Pontifex.Raw.Reliable.RawReliableClientEndpoint`
- **Contract:** [RawReliableAckClientEndpoint.cs](../../../../../../Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckClientEndpoint.cs),
  [RawReliableAckInfo.cs](../../../../../../Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckInfo.cs),
  [IRawReliableAckClientHandler.cs](../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Client/IRawReliableAckClientHandler.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Connect_SendsAckRequestAndAwaitsResponse` | Connection completion starts the ack handshake by sending an ack request. | `FillAckData` runs once, one framed request is transmitted, and logical `IsConnected` stays false. | The low-level connection is already up; the logical flag waits for the response. |
| `AckResponse_CompletesHandshake` | An acknowledgment response completes the logical connection. | `OnConnected` receives the endpoint and response payload once, and `IsConnected` becomes true. | The handler copies the payload before releasing the response buffer. |
| `AckResponse_WhenHandshakeAlreadyComplete_FailsHard` | A second acknowledgment response is a protocol violation. | The queued failure raises `OnDisconnected` once and stops with `ExceptionFail`. | This path routes through the queue `onError` → `FailHard`. |
| `FillAckData_Throws_StopsWithExceptionFail` | A handler exception while filling ack data aborts the handshake. | No `OnConnected` occurs and the stop reason is `ExceptionFail`. | The handler exception is wrapped with the exception instance. |
| `AckRequest_WhenCannotBeAdmitted_StopsWithTextFail` | An ack request that cannot be admitted aborts the handshake. | No frame is captured and the endpoint stops with `TextFail`. | Capacity is set below the framed request size. |
| `Disconnect_WhenLogicallyConnected_NotifiesOnDisconnected` | A graceful or ungraceful stop of a logically connected endpoint notifies `OnDisconnected` once. | The reason reaches `OnDisconnected` and `OnStopped`, and `IsConnected` is false. | The ack double calls the base `BeginDisconnect` before recording. |
| `Disconnect_WhenHandshakePending_DoesNotNotifyOnDisconnected` | A stop before the handshake completes has no logical disconnect notification. | `OnDisconnected` is not raised; only `OnStopped` follows. | The stage is `AckRequestSent`, not `LogicallyConnected`. |
| `FailHard_WhenLogicallyConnected_NotifiesDisconnectedThenStopped` | A hard failure of a logically connected endpoint notifies disconnection before stop. | `OnDisconnected` and `OnStopped` both receive the failure reason. | `IsConnected` is already false when the notification runs. |
| `FailHard_WhenHandshakePending_DoesNotNotifyOnDisconnected` | A hard failure before the handshake completes has no logical disconnect notification. | `OnDisconnected` is not raised; `OnStopped` receives the reason. | — |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| `FillAckData` producing data above `RawReliableAckInfo.AckMaxByteSize` stops with `TextFail`. | This layer | The oversize guard is not covered. |
| An inbound ack response above `AckMaxByteSize` stops with `TextFail` and releases the frame. | This layer | The inbound oversize guard is not covered. |
| `OnConnected` handler throwing stops with `ExceptionFail`. | This layer | The handler callback exception path is not covered. |
| `Acknowledged` is raised exactly once and subscriber exceptions are isolated. | This layer | The public event is not asserted. |
| No ping is sent before the ack request; keep-alive starts only after the handshake. | This layer | The handshake/keep-alive ordering invariant is documented but not asserted. |
| An ack response received while logically disconnected is released silently. | This layer | The disconnected-stage branch is not covered. |

## Deferred by design

| Gap | Reason |
|---|---|
| The inherited keep-alive background loop. | Keep-alive is disabled in the ack client double; the loop wrapper uses real time and is out of scope. |
| Real transport handshake and peer interaction. | Covered by the transport/conformance suites. |
| Real timing and socket failures. | The suite uses deterministic seams. |

## Notes

- Handler exceptions during `FillAckData` are `ExceptionFail` (not `TextFail`); other internal
  failures in the ack-send path may be `TextFail`.
- `OnConnected` is the ack-aware hook: it sends the request and does not call the inherited
  keep-alive initialization until the response completes the handshake.
- The test double's `BeginDisconnect` calls the ack base implementation before recording, which
  is what raises the logical disconnect notification.
- The ack response payload is copied out before the handler releases the buffer.
