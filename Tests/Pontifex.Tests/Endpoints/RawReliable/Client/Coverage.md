# RawReliableClientEndpoint Layer Coverage

- **Layer:** RawReliableClientEndpoint
- **Subject:** `Pontifex.Raw.Reliable.RawReliableClientEndpoint`
- **Directory:** `Tests/Pontifex.Tests/Endpoints/RawReliable/Client`
- **Inheritance parent:** `Pontifex.Raw.Reliable.RawReliableEndpoint`
- **Contract:** [RawReliableClientEndpoint.cs](../../../../../Pontifex/Implementations/Raw/Reliable/RawReliableClientEndpoint.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Constructor_WithNonPositivePingPeriod_Throws` | Supplied keep-alive options require a positive `PingPeriod`. | A zero ping period is rejected with `InvalidOperationException`. | The disconnect timeout is valid in this test. |
| `Constructor_WithDisconnectTimeoutLessThanTwicePingPeriod_Throws` | Supplied keep-alive options require `DisconnectTimeout >= 2 * PingPeriod`. | A too-short timeout is rejected with `InvalidOperationException`. | The boundary is strict: twice the period is valid. |
| `Constructor_WithValidPingOptions_DoesNotThrow` | A valid keep-alive configuration is accepted. | Valid options construct without throwing. | The endpoint is not connected or started. |
| `ProcessInbound_PongFrame_IsHandledAndDoesNotStop` | A pong frame is consumed by the keep-alive override. | The frame is released, not delivered to the handler, and does not stop the connection. | Proves the override against the base `TextFail` behaviour. |
| `KeepAliveTick_WhenPingPeriodElapsed_SendsPingAndContinues` | The tick sends a ping once the ping period elapses. | One `PingMessage` frame is admitted and the loop continues. | The first tick sends immediately because the last-sent-ping time starts at the default value. |
| `KeepAliveTick_WithinPingPeriod_DoesNotSendAgain` | A tick inside the ping period sends nothing. | The second tick keeps running without a new frame. | Time is advanced through the `Now` seam, not by sleeping. |
| `KeepAliveTick_WhenPongTimeoutElapsed_StopsWithTimeOut` | The pong timeout is checked before sending a ping. | The loop stops with `TimeOut`, sends nothing, and begins a disconnect. | The handler stop is observed after `FinishDisconnect`. |
| `KeepAliveTick_WhenPingCannotBeAdmitted_StopsWithTextFail` | A ping that cannot be admitted is a text failure. | No frame is captured and the endpoint stops with `TextFail`. | Capacity is set below the framing size to force the failure. |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| A pong refreshes the disconnect deadline, so a later tick past the original deadline still does not stop. | This layer | Pong delivery and timeout are tested separately, but the timestamp refresh effect is not. |
| `KeepAliveTick` with keep-alive disabled returns `true` and sends nothing. | This layer | The disabled configuration path is not asserted directly. |

## Deferred by design

| Gap | Reason |
|---|---|
| The background `Task.Run`/`Task.Delay` keep-alive loop wrapper. | The loop body is exercised synchronously through `KeepAliveTick`; the scheduling wrapper uses real time and is out of scope. |
| Default `DateTime.Now` clock. | Tests use the `Now` seam for determinism. |
| Real transport connect/disconnect and a real network. | Covered by transport/integration suites. |

## Notes

- The test double overrides `StartKeepAlive` to a no-op, so no background task is created; tests
  drive `KeepAliveTick` directly.
- `Now` is replaced by a controllable clock; the first tick is immediately due because the
  last-ping timestamp is not initialized until a ping is sent.
- `OnConnected` must call the base implementation for the pong timestamp to be initialized; the
  client double does so.
- The timeout check runs before the ping check, which is why an expired timeout sends nothing.
