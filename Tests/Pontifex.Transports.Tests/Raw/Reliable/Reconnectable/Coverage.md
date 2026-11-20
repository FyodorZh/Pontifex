# RawReliableReconnectable Coverage

Layer: `RawReliableReconnectable`
Subject: `Pontifex.Raw.Reliable.Reconnectable.RawReliableReconnectableBinding`,
`Pontifex.Raw.Reliable.Reconnectable.RawReliableReconnectableClient`,
`Pontifex.Raw.Reliable.Reconnectable.RawReliableReconnectableServer`,
`Pontifex.Raw.Reliable.Reconnectable.ReconnectableClientLogic`, and
`Pontifex.Raw.Reliable.Reconnectable.ReconnectableServerLogic`
Directory: `Tests/Pontifex.Transports.Tests/Raw/Reliable/Reconnectable`

Contract references:
[RawReliableReconnectableBinding.cs](../../../../../Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/RawReliableReconnectableBinding.cs),
[RawReliableReconnectableClient.cs](../../../../../Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/RawReliableReconnectableClient.cs),
[RawReliableReconnectableServer.cs](../../../../../Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/RawReliableReconnectableServer.cs),
[ReconnectableClientLogic.cs](../../../../../Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/ReconnectableClientLogic.cs),
[ReconnectableServerLogic.cs](../../../../../Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/ReconnectableServerLogic.cs),
[ReconnectableBaseLogic.cs](../../../../../Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/Common/ReconnectableBaseLogic.cs),
[DeliverySystem.cs](../../../../../Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/Common/DeliverySystem.cs),
[ReconnectableInfo.cs](../../../../../Protocols/Pontifex.Protocol.Reconnectable/ReconnectableInfo.cs),
[RawReliableNegotiation.cs](../../../../../Pontifex/Implementations/Raw/Reliable/Negotiation/RawReliableNegotiation.cs),
[NegotiationSession.cs](../../../../../Pontifex/Implementations/Raw/Reliable/Negotiation/NegotiationSession.cs).

## Covered

| Test | Contract behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `FirstConnection_EstablishesSessionAndTransfersData` | A first connection establishes one logical session and transfers application data in both directions. | The user factory is called once, both application handlers connect once, and payloads travel in both directions through the delivery system. | The fake inner endpoint reports `MessageMaxByteSize = 4096`; the reconnectable proxy forwards that value. `TickAll()` runs both manual drivers twice so a payload queued on one side becomes visible to the other side's application handler without a real thread or timer. The negotiation request/response tails are not inspected by this test. |
| `ReconnectWithSessionIdAndSecret_ReattachesExistingSession` | A physical drop is not a logical disconnect; the next connection sends the live id and secret and reattaches the existing session. | After the drop the application handlers observe no disconnect; the second physical connection calls no user factory, raises no second `OnConnected`, reuses the same server handler, and delivers new data to the original session. | The server session map stays populated while the physical link is gone. `DropPhysicalConnection()` stops the fake physical session and its client attempt; `Reconnect()` advances the manual clock by the two-second reconnect period. |
| `WrongSecret_IsRejectedAndStopsTheInnerSessionExactlyOnce` | A reattach request with the right id but a wrong secret is rejected before the existing session is claimed. | The client stops with `HandshakeRejected`; no new user handler is created; the existing server handler is untouched; the rejected physical session is stopped exactly once after the two-second linger. | The harness rewrites the last element of the reattach request (the secret) on the outbound fake endpoint. The linger is asserted through the manual clock, so the stop-once counts are deterministic. |
| `VersionMismatch_IsRejectedWithoutOnConnectedOrFactory` | A request whose negotiation version differs from `1` is rejected. | The client observes only `OnStopped` with `HandshakeRejected`, never `OnConnected`; the server user factory is never called and no session handler exists. | The client is constructed with an internal negotiation-version seam; the production binding always uses version `1`. |
| `NameMismatch_IsRejectedWithoutOnConnectedOrFactory` | The negotiation name is validated before any reconnectable data is parsed. | A client negotiating a different layer name is rejected with `HandshakeRejected`; no `OnConnected` and no server factory call occur. | Name comparison is by UTF-8 content inside the shared negotiation session; the reconnectable logic adds no name check of its own. |
| `QueuedMessages_SurviveReattach` | The client send queue keeps undelivered payloads while the physical link is down and replays them after reattach. | A payload accepted before the drop is not delivered on the dead link; after reattach the server handler receives it exactly once. | The message is still in the client send queue because no tick ran before the drop; the `DeliverySystem` pending-buffer replay path is not exercised. The manual driver ticks after reattach; no real time or retry timer is involved. |
| `ReplayedMessageAfterReattach_IsDeliveredOnce` | A message that entered the delivery system but was not acknowledged before the drop is replayed on reattach and suppressed as a duplicate. | The server receives the reattach tail plus one replayed frame (`ReceivedCount == 2`), but its application handler observes the payload exactly once. | A server-side send first resets the report-flush window so the delivery report is not emitted in the same tick; the message therefore stays unconfirmed. A negative control that skips `ScheduledBuffers` drops `ReceivedCount` to 1, proving the second frame is the replay. |

## Planned

| Missing test | Owning layer |
| --- | --- |
| `handshake_max_byte_size` default/range and its binding formula exposure. | `RawReliableReconnectable` (the loose `FactoryBindingsTests` compiles the formula but does not set or assert the parameter). |
| Session-map capacity exhaustion and `disconnect_timeout` expiry removing the session and stopping the user handler. | `RawReliableReconnectable`. |
| Two simultaneous physical connections racing for the same session id, and a reattach that arrives while the old link is still attached. | `RawReliableReconnectable`. |
| Server response send failure during a reattach and its effect on the existing session's attachment state. | `RawReliableReconnectable`. |
| End-to-end `reconnectable(30, tcp(...))` over a real carrier. | Carrier integration suites (`Pontifex.Transport.Net.Tests`/`Pontifex.Transport.Direct.Tests`). |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Real timers, reconnect-period accuracy, and wall-clock behaviour | The suite drives the reconnect gate and the negotiation/linger deadlines through a manual clock and a hand-driven periodic driver. Real-time accuracy belongs to a categorised integration suite. |
| Real TCP/Direct carriers, sockets, and wire framing | Covered by the carrier integration suites; this suite drives fake reliable transports. |
| The shared `NegotiationSession` driver, admission gate, and rejection linger mechanics | Owned by the `RawReliableWrapper` suite and the shared session documentation. The reconnectable client/server do not use `RawReliableWrapperLogic`; their own admission and terminal paths are recorded above. |
| Exact secret bytes and randomness | The contract exposes the secret only as opaque byte content; tests assert its length and that a different value is rejected. |
| Buffer reference-count internals | Tests assert observable ownership outcomes (no duplicate delivery, existing session still usable) through the public contract, not private counters. |
| Security properties (authentication, confidentiality) | The secret is a resume token, not an authentication or encryption mechanism; security is out of scope. |
| Logging and diagnostics text | Requires an observable log sink; the `ILogger` is used only as a dependency. |
| Performance and allocation counts | Not part of this contract. |
