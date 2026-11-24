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

The binding **MUST** reject an unknown `type`, an unparsable field, an
incomplete loss-model parameter set, and a `delivery` value supplied for the
server role.

## 6. Lifecycle and teardown

The carrier owns the `Channel`, its two endpoint halves, and the registry entry
for the server `id`.

`Start` on the server registers `id` with `ChannelManager`; a duplicate `id`
fails `Start`. The server creates one session endpoint for each source route
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
is disposed when the client transport stops. Delivery configuration is accepted
only before the client transport starts.

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

This implementation declares no deviation from the abstract type. It adds no
authentication, confidentiality, or integrity protection; the process boundary is
the isolation boundary.

## 8. Implementation-defined choices

| Area | Choice |
| --- | --- |
| Carrier and wire encoding | In-process `Channel`; each message is one complete `UnionDataList`, with no byte serialization. |
| Addressing | The server is a `StringEndPoint` holding `id`; each client transport owns a unique `GuidEndPoint`, which the server uses as the source route. |
| Delivery virtualization | `PerfectDeliverySystem` by default; the `delivery` parameter optionally installs `NetemDeliverySystem` for both channel directions. |
| Callback scheduler | Each endpoint owns a serial action queue; each sender owns a bounded commit queue so a peer callback does not begin on the caller's thread. |
| Queue capacity and drain rate | `100` messages and `104857600` bytes; a full queue releases the message and may yield `BufferOverflow`. |
| Dispatch timing | A committed message reaches the channel after `Send` returns; a server notifies active handlers with the supplied stop reason before it stops the carrier, and `onStopped` is dispatched after endpoint teardown. |
| Failure detection | Channel disposal is the only disconnection signal; a missing server is detected during the first send. |
| Logging | Oversized inbound messages and delivery-system exceptions are logged; a message dropped for a missing server is not logged. |
| Protection | None beyond the process boundary. |

## 9. Implementer checklist

- [ ] Register the scheme `direct-raw-unreliable` for
      `TransportType.RawUnreliable`.
- [ ] Bind the server to the exact `id` and fail `Start` on a duplicate `id`.
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
- [ ] Dispose a channel's delivery systems with the channel, and dispose an
      uninstalled delivery system when the client transport stops.
- [ ] Support the `perfect` and `netem` delivery types and reject an invalid
      `delivery` value, including one supplied for the server role.
- [ ] Add no protection beyond the process boundary.
