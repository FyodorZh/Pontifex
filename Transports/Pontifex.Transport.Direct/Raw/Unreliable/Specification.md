# Direct RawUnreliable Specification

## 1. Scope

The Direct carrier implements
[`RawUnreliable`](../../../../Pontifex/Abstractions/Transports/Raw/Unreliable/Specification.md)
over an in-process channel.

This implementation adds the process-wide channel registry, the lazy client
connection, per-source session routing, the bounded send budgets, and the
optional virtual delivery systems. It does not change the delivery guarantee,
the lifecycle, the send results, or the buffer ownership of the abstract type.

### 1.1 References

- [`RawUnreliable`](../../../../Pontifex/Abstractions/Transports/Raw/Unreliable/Specification.md)
  — the contract implemented here.

## 2. Normative language

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD NOT**,
and **MAY** in this document are to be interpreted as described in
[RFC 2119](https://datatracker.ietf.org/doc/html/rfc2119).

## 3. Contract

The carrier is registered for `TransportType.RawUnreliable` with the scheme
`direct-raw-unreliable`. The binding is `RawUnreliableDirectBinding`, and the
concrete transports are `RawUnreliableDirectClientTransport` and
`RawUnreliableDirectServerTransport`. Each transport is constructed with a server `id`, a
logger, and a memory rental; the binding exposes `id` as the required positional
parameter.

The carrier produces an `IRawUnreliableClient` or an `IRawUnreliableServer` of
the abstract type.

## 4. Scheme and negotiation

- Scheme: `direct-raw-unreliable`
- Handshake name: none
- Handshake version: none

The carrier is connectionless at the contract level. It has no admission
exchange, session, or peer lifecycle callback. A message crosses the channel as
one complete `UnionDataList`; no envelope and no byte serialization exist.

The client acquires its channel on the first send. A send with no registered
server is accepted locally and then released: the carrier reports no
`NotConnected` and no synchronous delivery failure.

## 5. Parameters

| Parameter | Type and unit | Required | Default | Meaning |
| --- | --- | --- | --- | --- |
| `id` | string | yes | none | Server name; the server binds it and the client connects to it. |
| `delivery` | struct | no | `{ type = "perfect" }` | Virtual delivery configuration for the client channel directions. |

Every parameter is implementation configuration, not part of the abstract
contract.

### 5.1 `delivery`

The `delivery` value selects the delivery system installed for both directions
of the client channel. The value is a struct with a required `type` field.

| Field | Type and unit | Required | Default | Meaning |
| --- | --- | --- | --- | --- |
| `type` | string | yes | none | Selects `perfect` or `netem`. |
| `latencyns` | integer, nanoseconds | no | `0` | Base added delay. |
| `jitterns` | integer, nanoseconds | no | `0` | Uniform jitter range around the base delay. |
| `lossprobability` | integer, scaled to `uint` | no | `0` | Random loss probability. |
| `duplicateprobability` | integer, scaled to `uint` | no | `0` | Duplication probability. |
| `reorderprobability` | integer, scaled to `uint` | no | `0` | Reorder probability for gap candidates. |
| `gap` | integer, packets | no | `0` | Every Nth packet is a reorder candidate; `0` disables reordering. |
| `ratebytespersec` | integer, bytes per second | no | `0` | Rate limit; `0` is unlimited. |
| `packetoverhead` | integer, bytes | no | `0` | Extra bytes per packet for the rate calculation. |
| `cellsize` | integer, bytes | no | `0` | ATM-style cell size for the rate calculation; `0` disables it. |
| `celloverhead` | integer, bytes | no | `0` | Extra bytes per cell. |
| `queuelimit` | integer, messages | no | `2147483647` | Maximum queued messages before a drop. |
| `lossmodel` | string | no | `random` | Selects `random`, `fourState`, or `gilbertElliot`. |
| `correlation` | struct | no | all fields `0` | Correlation coefficients for delay, loss, duplication, and reordering. |
| `slot` | struct | no | disabled | Periodic transmission windows. |
| `delaydistribution` | struct | no | `none` | Empirical delay distribution table. |
| `slotdistribution` | struct | no | `none` | Distribution table for slot spacing. |
| `fourstate` | struct | no | `none` | Parameters for the four-state loss model. |
| `gilbertelliot` | struct | no | `none` | Parameters for the Gilbert-Elliot loss model. |

The `perfect` type installs `PerfectDeliverySystem` for both directions and
preserves order, content, and delivery. The `netem` type installs a
`NetemDeliverySystem` per direction with the same configuration; it may drop,
duplicate, reorder, delay, and rate-limit messages according to the fields above.

The binding stores delivery factories, not delivery systems. No delivery system
is constructed during transport construction, binding creation, or before the
client start; the client endpoint materializes one system per direction when it
is created. A configured `perfect` or `netem` value is therefore inert until the
first start materializes it, and a pre-start stop constructs nothing.

The binding **MUST** reject an unknown `type`, an unparsable field, an
incomplete loss-model parameter set, and a `delivery` value supplied for the
server role.

## 6. Lifecycle and teardown

The carrier owns the `Channel`, its two endpoint halves, and the registry entry
for the server `id`.

`Start` on the server registers `id` with `ChannelManager` under the
`direct-raw-unreliable` scheme; a duplicate `id` within that scheme fails
`Start`. The same `id` may exist under another Direct scheme, and a client sees
only its own scheme. The server creates one session endpoint for each source route
through the abstract routing rules. The client creates one endpoint at start and
acquires its channel on the first send.

`Stop` on the server unregisters `id` and disposes every channel that is still
registered. Before it stops the carrier, it stops every active session endpoint
with the supplied `StopReason` instance, so each active handler observes exactly
that instance. The later channel-disposal notification is a one-shot no-op. `Stop`
on the client disposes its channel and removes it from the server registry. A
disposed channel stops both endpoint halves.

The delivery systems installed on a channel are owned by that channel and are
disposed with it. A delivery system that the client never installed on a channel
is released exactly once by the client endpoint or the client transport.
Delivery configuration is accepted only before the client transport starts, and
reliable debug mode must be selected before any explicit configuration.

Ownership is exact per direction:

- A configured explicit pair is owned by the transport until the endpoint
  materializes it. A pre-start stop, a start failure, or reliable debug mode
  releases it exactly once and replaces the direction with a fresh perfect
  factory.
- If endpoint construction throws, both materialized systems are released once
  and both directions are reset to fresh perfect factories, so a retry cannot
  reuse a released instance.
- If the channel takes a system, the channel releases it. The client endpoint
  releases only the direction the channel never took. A channel is removed from
  the server registry before it is disposed on a failed connect.
- A manager callback exception or a delivery-system install exception is logged
  and isolated: the endpoint stays alive, the outbound buffer is released once,
  both directions are reset to fresh perfect factories, and the next send
  retries.
- A successful start followed by `Stop` releases each direction exactly once.

The client endpoint's commit queue is bounded by `100` messages. Reliable debug
mode is client-only: the Direct server has no delivery systems, so
`TryMakeReliableForDebug()` returns `false`. In client debug mode the client
endpoint uses perfect systems and an unbounded commit queue, so the client
direction cannot drop an accepted message on queue saturation. Server replies
remain on the bounded commit queue; client debug mode does not make
server-to-client FIFO unconditional under queue saturation.

The carrier enforces these limits:

| Limit | Scope | Value |
| --- | --- | --- |
| Message maximum | Both directions | `1048576` bytes |
| Queue capacity | Client endpoint and server transport | `100` messages |
| Send budget | Client endpoint | `104857600` bytes |
| Send budget | Server transport, shared by all sessions | `104857600` bytes |

The client endpoint and each server session endpoint admit at most `100`
accepted messages whose commit has not completed. A send that would exceed the
window is rejected synchronously with `BufferOverflow` and its message is
released; an empty message counts toward the same bound. The commit worker
releases its slot when it commits or discards the message, including after a
throwing commit or a queue disposal.

A commit worker defers the peer callback until the sending `Send` call has
returned. A handler that sends synchronously from a callback is reentrant on its
own endpoint and cannot deadlock against that barrier.

An inbound message above the message maximum is released and logged before any
route lookup; it does not invoke the handler factory, reach a handler, or stop
the endpoint or the transport.

A factory-created handler whose endpoint cannot be created receives exactly one
`OnStopped` outside the admission lock, as required by the abstract
specification. The triggering message is released and no route is published.

## 7. Failure, deviations, and protection

A disposed channel terminates the sessions that own it and stops the owning
client transport. A send with no registered server releases the message without
logging and without a transport failure. An exception thrown by a delivery
system is caught, logged, and releases that message; it does not stop the
endpoint or the transport.

The endpoint control hits `BeforeHandlerStoppedGate` immediately before a
forwarded handler `OnStopped` call. It is not hit for a handler whose start
notification threw, and it is not hit on the raw endpoint teardown itself.

### 7.1 Residual limitations

- Reliable debug mode is client-only. `RawUnreliableDirectServerTransport`
  reports it as unsupported because the server owns no delivery systems and
  cannot make its reply queue reliable. Conformance reliable-mode tests run
  through the client capability.
- Under client debug mode, server replies stay on the bounded commit queue. A
  saturated reply queue can still drop a queued reply, so mandated FIFO is not
  unconditional for server-to-client traffic.

Apart from these two documented limitations, this implementation declares no
deviation from the abstract type. It adds no authentication, confidentiality, or
integrity protection; the process boundary is the isolation boundary.

## 8. Implementation-defined choices

| Area | Choice |
| --- | --- |
| Carrier and wire encoding | In-process `Channel`; each message is one complete `UnionDataList`, with no byte serialization. |
| Addressing | The server is a `StringEndPoint` holding `id`; each client transport owns a unique `GuidEndPoint`, which the server uses as the source route. |
| Delivery virtualization | `PerfectDeliverySystem` by default; the `delivery` parameter configures a per-direction factory that constructs `PerfectDeliverySystem` or `NetemDeliverySystem`. Nothing is constructed before the client start materializes the endpoint. |
| Callback scheduler | Each endpoint owns a serial action queue; each sender owns a commit queue so a peer callback does not begin on the caller's thread. The client debug commit queue is unbounded; the normal and server reply queues are bounded. |
| Queue capacity and drain rate | `100` messages and `104857600` bytes; a full queue releases the message and may yield `BufferOverflow`. |
| Dispatch timing | A commit worker defers the peer callback until `Send` returns; a server notifies active handlers with the supplied stop reason before it stops the carrier, and `onStopped` is dispatched after endpoint teardown. |
| Failure detection | Channel disposal is the only disconnection signal; a missing server is detected during the first send. A connect or install failure is isolated and retried with fresh perfect systems. |
| Logging | Oversized inbound messages and delivery-system exceptions are logged; a message dropped for a missing server is not logged. |
| Protection | None beyond the process boundary. |

## 9. Implementer checklist

- [ ] Register the scheme `direct-raw-unreliable` for
      `TransportType.RawUnreliable`.
- [ ] Bind the server to the exact `id` and fail `Start` on a duplicate `id`
      within the `direct-raw-unreliable` scheme.
- [ ] Connect the client lazily on the first send and release the message when
      no server is registered.
- [ ] Key every server route by source-endpoint equality and hash code.
- [ ] Preserve complete message boundaries and content on every delivery.
- [ ] Enforce the `1048576`-byte message maximum and log oversized inbound
      messages without stopping the endpoint.
- [ ] Enforce the `100`-message and `104857600`-byte budgets.
- [ ] Give each client endpoint its own budget and share one budget across all
      server sessions.
- [ ] Commit each send through a queue so no peer callback begins on the
      caller's thread.
- [ ] Serialize handler callbacks per endpoint and prevent reentrancy.
- [ ] Remove a channel from the server registry, and dispose it, when the client
      endpoint stops or the channel is otherwise disposed.
- [ ] Dispose every registered channel when the server stops.
- [ ] Dispose a channel's delivery systems with the channel; release an
      uninstalled system exactly once; construct no system before start.
- [ ] Isolate connect and install exceptions and retry with fresh systems.
- [ ] Support the `perfect` and `netem` delivery types and reject an invalid
      `delivery` value, including one supplied for the server role.
- [ ] Report reliable debug mode as client-only and keep server replies on the
      bounded commit queue.
- [ ] Add no protection beyond the process boundary.
