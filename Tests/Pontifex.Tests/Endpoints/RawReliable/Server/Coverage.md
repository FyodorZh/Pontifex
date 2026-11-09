# RawReliableServerEndpoint Layer Coverage

- **Layer:** RawReliableServerEndpoint
- **Subject:** `Pontifex.Raw.Reliable.RawReliableServerEndpoint`
- **Directory:** `Tests/Pontifex.Tests/Endpoints/RawReliable/Server`
- **Inheritance parent:** `Pontifex.Raw.Reliable.RawReliableEndpoint`
- **Contract:** [RawReliableServerEndpoint.cs](../../../../../Pontifex/Implementations/Raw/Reliable/RawReliableServerEndpoint.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Constructor_WithNonPositiveDisconnectTimeout_Throws` | The idle-session timeout must be positive. | A zero timeout is rejected with `ArgumentException`. | The guard is `<= TimeSpan.Zero`. |
| `ProcessInbound_PingFrame_AnswersWithPongFrame` | A ping frame is answered with a pong frame. | One pong is admitted, the ping is not delivered to the handler, and the connection stays up. | The ping frame is released by the override. |
| `ProcessInbound_PingFrame_WhenPongCannotBeAdmitted_StopsWithTextFail` | A pong that cannot be admitted is a text failure. | No frame is captured and the endpoint stops with `TextFail`. | Capacity is set below the framing size to force the failure. |
| `PeriodicTick_WhenNotConnected_DoesNothing` | The watchdog is inactive before a connection. | No disconnect and no handler stop occur. | `IsLowLevelConnected` gates the tick. |
| `PeriodicTick_WithinDisconnectTimeout_DoesNothing` | The watchdog tolerates silence inside the configured timeout. | No stop occurs one second before the deadline. | Time is advanced through the `Now` seam. |
| `PeriodicTick_AfterDisconnectTimeout_StopsWithTimeOut` | Silence beyond the timeout stops the session. | The endpoint begins a disconnect with `TimeOut`; the handler stop arrives after `FinishDisconnect`. | The comparison is strict `> timeout`. |
| `PeriodicTick_AfterInboundWithinTimeout_DoesNothing` | Every inbound frame refreshes the idle deadline. | A tick past the original deadline does nothing because the frame reset the timestamp. | The payload frame also exercises normal delivery. |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| A ping buffer is released when the pong cannot be admitted. | This layer | The stop is asserted, but the ping's ownership release is not. |
| `PeriodicTick` before the deferred timestamp initialization. | This layer | Tests always drain the scheduled `OnConnected` initialization first. |

## Deferred by design

| Gap | Reason |
|---|---|
| A real periodic driver, timer, or transport ticker. | The `PeriodicTick` body is driven directly; scheduling is out of scope. |
| Default `DateTime.Now` clock and real-time timeout behaviour. | Tests use the `Now` seam for determinism. |
| Real transport sessions and server admission. | Covered by the transport suite. |

## Notes

- The test double overrides `Now` with a controllable clock; advancing time is explicit and no
  timer or thread is involved.
- `OnConnected` schedules the watchdog timestamp initialization on the serial queue. Every test
  that relies on the watchdog drains that action first.
- `RefreshLastInboundMessage` is called for every inbound frame, including frames that are later
  rejected; the refresh test uses a normal payload frame.
