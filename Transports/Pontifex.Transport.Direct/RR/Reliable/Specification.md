# Direct RRReliable Specification

## 1. Scope

The Direct carrier implements
[`RRReliable`](../../../../Pontifex/Abstractions/Transports/RR/Reliable/Specification.md)
over the in-process `Channel`.

This implementation adds the lazy channel connection, the reusable reliable core's serialized
dispatch queue, the per-session duplicate window, and the process-wide `id` registry. It does not
change the request model, the ordered response stream, the response-handle states, the TTL effects,
or the buffer ownership of the abstract type.

### 1.1 References

- [`RRReliable`](../../../../Pontifex/Abstractions/Transports/RR/Reliable/Specification.md)
  — the contract implemented here.
- [`RR` common specification](../../../../Pontifex/Abstractions/Transports/RR/Specification.md)
  — the inherited lifecycle, correlation, TTL, and ownership rules.

## 2. Normative language

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD NOT**,
and **MAY** in this document are to be interpreted as described in
[RFC 2119](https://datatracker.ietf.org/doc/html/rfc2119).

## 3. Contract

The carrier is registered for `TransportType.RRReliable` with the scheme
`direct-rr-reliable`. The binding is `RRReliableDirectBinding`; the concrete transports are
`RRReliableDirectClient` and `RRReliableDirectServer`. Both are `internal`; the binding is public.

The client is constructed with a server `IEndPoint`, a request TTL, a logger, a memory rental, and
an optional reliable timer factory. The server is constructed with a server id, a response TTL, a
logger, a memory rental, and an optional reliable timer factory. Neither transport accepts a
delivery configuration. The binding exposes `id` as the required positional parameter.

The concrete transports derive from the reusable `RRReliableClientTransport` and
`RRReliableServerTransport` and supply only carrier operations. Request tracking, ordered response
delivery, completion, cancellation, TTLs, duplicate suppression, and handler serialization stay in
the core.

## 4. Scheme and negotiation

- Scheme: `direct-rr-reliable`
- Negotiation name: none
- Negotiation version: none

The client acquires its channel on the first request and keeps that channel for its lifetime. A
channel loss after a live session fails the transport; it does not reconnect. There is no
admission exchange, session handshake, or peer lifecycle callback.

A request frame carries the client-local request id. Each response frame carries that id and a
`uint` ordinal starting at one. A finish frame carries the id and a completion byte. A cancel frame
carries the id only. The server-global request id is local bookkeeping and is not sent on the wire.

## 5. Parameters

| Parameter | Type and unit | Required | Default | Meaning |
| --- | --- | --- | --- | --- |
| `id` | string | yes | none | Server name; the server binds it and the client connects to it. |
| `request_ttl` | integer, seconds | no | `3` | Client pending-window lifetime; valid only for a client transport. |
| `response_ttl` | integer, seconds | no | `3` | Server response-handle lifetime; valid only for a server transport. |

Every parameter is implementation configuration, not part of the abstract contract. The binding
**MUST** reject `request_ttl` on a server transport and `response_ttl` on a client transport.

The binding registers no `delivery` parameter. The formula compiler rejects `delivery` for this
scheme before the binding runs; the carrier always uses the channel's default perfect delivery
systems.

## 6. Lifecycle and teardown

The client owns one fixed `GuidEndPoint` for its lifetime and the `DirectRrChannelClient` it lazily
connects. `StartCarrier` always succeeds and creates the carrier without touching the channel
registry. A request with no registered server makes the carrier release the frame and return
`Error`; the request delivers no callbacks, and the transport stays valid.

The server owns the process-wide registry entry for `id`, its `DirectRrChannelServer`, and the
reliable core's serialized `OnRequested` dispatch queue. `StartCarrier` calls `StartDispatch`
before `DirectRrChannelServer.TryStart`, so a request cannot arrive before the queue exists. A
failed registration calls `StopDispatch` and fails `Start`. `StopCarrier` stops the carrier and
calls `StopDispatch`.

A request that arrives before `OnStarted` is held in the core's pre-start pending list, bounded by
the dispatch capacity. A full list closes the handle, records the client-local id terminal, and
releases the request, so the client fails at its request TTL. Stop releases every held request and
disposes the queue without waiting for a running `OnRequested`.

An inbound request goes to `OnFrameReceived`; an inbound response or terminal frame goes to the
client's `OnFrameReceived`. A disposed server channel calls `OnSessionClosed(source)`, which closes
that session's handles and leaves other sessions usable.

The client's `Stop` unsubscribes the carrier's disposal notification, leaves the registry, and
disposes the channel. A normal stop therefore leaves `IsValid` true. A channel disposal before the
first successful send is a no-session loss: the carrier clears the channel, the transport stays
valid, and a later request retries. A channel disposal after a live session fails the transport
with `TextFail` unless the transport is already stopping. Before failing, the client marks every
accepted pending request terminal and schedules its one `Finish(Failed)`.

The carrier enforces these limits:

| Limit | Scope | Value |
| --- | --- | --- |
| Message maximum | Both directions | `1048576` bytes, excluding the reliable header and channel framing |
| Client-local request ids | One client | `16` bits, incremented with wraparound |
| Dispatch queue | Server | Configured core capacity, default `1024` items |
| Pre-start pending list | Server | Same capacity as the dispatch queue |
| Duplicate window | One server session | `65536` client-local ids |
| TTL defaults | Both directions | `3` seconds |

## 7. Failure, deviations, and protection

A disposed channel ends the current client session. After a live session it is a transport failure:
the transport becomes invalid, `IsStarted` is false, the owner's stop callback receives a
`TextFail`, and pending accepted requests each receive exactly one scheduled `Finish(Failed)`.
Before any successful send the disposal is not a failure: the request returns `Error`, no callback
is scheduled, and the transport remains valid.

A disposed server channel closes that client's session. Retained response handles return `Error`;
later calls from other sessions keep working. The client's own stop supplies its terminal
notification.

A malformed or oversized inbound frame is released and logged without stopping the transport. A
throwing delivery factory, install, or send is caught and logged; the frame is released once and
the affected operation returns `Error`. A request whose response TTL expired while it waited in
the dispatch queue is not presented: the worker releases it and records the client-local id
terminal.

### 6.1 Response-handle states

A response handle is `Undecided`, `Accepted`, `Rejected`, `Finished`, or `Closed`. `Accepted` and a
live transport are not terminal: later `Response` and `Finish` calls proceed, while `Reject` throws
`InvalidOperationException`.

| Call | Transport stopping or stopped | Rejected | Finished | TTL/session/disposal close | Otherwise |
| --- | --- | --- | --- | --- | --- |
| `Response`/`Finish` | `Error` | throw | `Error` | `Error` | validate and send |
| `Reject` | `Error` | `Ok` | throw | `Error` | send |

`Finish` becomes `Finished` only when the carrier send returns `Ok`; a non-`Ok` send leaves the
handle retryable. `Reject` becomes `Rejected` only on a successful send and can be retried after a
failed one. A completed `Finish` followed by `Reject` while the transport is live throws; once the
transport stops, every call returns `Error`. The response TTL sends `Finish(Failed)`; a session
close and a disposal close send nothing.

This implementation declares three bounded deviations from the abstract type.

1. **Callback after stop.** Shared §7.4 orders terminal notifications before `onStopped`. Dispatch
   disposal is non-blocking, so an `OnRequested` already running at stop may complete after
   `onStopped` begins. Its response handle is closed, and no other server callback overlaps it. The
   handler **MUST** tolerate one late request and release its payload.
2. **Callback during start.** Shared §7.2 says no handler callback begins before `Start` returns.
   `OnStarted` opens the gate during the final phase of `Start`, so a queued request may be
   presented after `OnStarted` but before the caller observes the return. The handler **MUST**
   tolerate a callback before it has observed `Start` returning.
3. **Per-session request cap.** Reliable §10 requires duplicate suppression within the documented
   persistence scope. The server records every terminal client-local id in a fixed 65,536-bit
   per-session bitmap. After 65,536 terminal requests the window is full; the server rejects new
   requests with `Reject`, and the client receives `Finish(Rejected)`. Re-establishing the session
   requires a new client transport. The application **MUST** re-establish the session before the
   cap; handlers **MUST** tolerate a rejection at the cap.

The process boundary is the isolation boundary. The carrier adds no authentication,
confidentiality, or integrity protection.

## 8. Implementation-defined choices

| Area | Choice |
| --- | --- |
| Wire correlation | The client-local `ushort` request id is the first frame element. A response adds a `uint` ordinal starting at one; a finish adds a completion byte; a cancel carries only the id. The server-global id stays local. |
| Duplicate-suppression scope | Per session, keyed by the client-local id, with a full 65,536-bit bitmap. The window is not shared between clients and does not survive a session. |
| Completion transport | A `Finish` frame carries the id and an `RRReliableCompletionType` byte; `Reject` sends `Rejected`, `Finish` sends `Normal`, the response TTL sends `Failed`, a session close and a disposal close send nothing, and the client's own stop or failure supplies `Failed`. |
| Timer factory | `Pontifex.Utils.ITimerFactory`; the concrete transports accept an optional factory and default to `SystemTimerFactory.Instance`. The binding exposes no timer parameter. |
| Capacity | The server dispatch queue and its pre-start pending list default to `1024` items; a full list releases the request and closes its handle. |
| Persistence | None. Pending requests, sessions, and the duplicate window do not survive a restart. |
| Payload limit | `1048576` bytes, excluding the reliable header and channel framing. |
| Server id uniqueness | `id` is unique within the `direct-rr-reliable` scheme; a duplicate registration in that scheme fails `Start`. Another Direct scheme may use the same `id`. |
| Callback scheduler | One server-side serialized dispatch queue owns `OnRequested`; the client callback pump owns `Response` and `Finish`. |
| Reconnect policy | The first request connects lazily. After a live session a channel disposal fails the transport; there is no reconnect or background retry. |
| Failure detection | Channel disposal is the only session-loss signal. A missing server is detected during `ChannelManager.Connect`. |
| Logging | Malformed, oversized, foreign-route, delivery-system, and handler failures are logged. A request dropped for a missing peer is not logged. |

## 9. Implementer checklist

- [ ] Register the scheme `direct-rr-reliable` for `TransportType.RRReliable`.
- [ ] Reject `request_ttl` on a server and `response_ttl` on a client.
- [ ] Default both TTLs to three seconds.
- [ ] Register no `delivery` parameter for this scheme.
- [ ] Create the dispatch queue before carrier registration and dispose it on failure.
- [ ] Hold pre-start requests in the bounded pending list and release them on stop.
- [ ] Serialize `OnRequested` through the one dispatch queue.
- [ ] Start the response TTL at frame receipt.
- [ ] Close the session and its handles when its channel is disposed.
- [ ] Treat a channel disposal after a live session as `TextFail` and fail the transport.
- [ ] Treat a channel disposal before any successful send as a valid retry, not a failure.
- [ ] Schedule `Finish(Failed)` for every accepted pending request before `onStopped`.
- [ ] Leave `IsValid` true after a normal stop and after a later channel disposal.
- [ ] Release the frame on every failing connect, install, send, and dispatch path.
- [ ] Fail `Start` on a duplicate `id` within the `direct-rr-reliable` scheme
      and keep the first server usable.
- [ ] Record every terminal client-local id in the per-session duplicate window.
- [ ] Reject new requests when the 65,536-request window is full.
- [ ] Keep the three declared deviations within their stated bounds.
- [ ] Add no authentication or integrity protection.
