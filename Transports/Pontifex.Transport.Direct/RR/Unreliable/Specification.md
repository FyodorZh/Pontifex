# Direct RRUnreliable Specification

## 1. Scope

The Direct carrier implements
[`RRUnreliable`](../../../../Pontifex/Abstractions/Transports/RR/Unreliable/Specification.md)
over the in-process `Channel`.

This implementation adds the lazy channel connection, the bounded server dispatch queue, the
optional virtual delivery configuration, and the process-wide `id` registry. It does not change the
pending window, the single-use response handle, the correlation rules, or the buffer ownership of
the abstract type.

### 1.1 References

- [`RRUnreliable`](../../../../Pontifex/Abstractions/Transports/RR/Unreliable/Specification.md)
  — the contract implemented here.
- [`RR` common specification](../../../../Pontifex/Abstractions/Transports/RR/Specification.md)
  — the inherited lifecycle, correlation, TTL, and ownership rules.

## 2. Normative language

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD NOT**,
and **MAY** in this document are to be interpreted as described in
[RFC 2119](https://datatracker.ietf.org/doc/html/rfc2119).

## 3. Contract

The carrier is registered for `TransportType.RRUnreliable` with the scheme
`direct-rr-unreliable`. The binding is `RRUnreliableDirectBinding`; the concrete transports are
`RRUnreliableDirectClient` and `RRUnreliableDirectServer`. Both are `internal`; the binding is
public.

The client is constructed with a server `IEndPoint`, a request TTL, a logger, a memory rental, and
optional per-connect delivery factories. The server is constructed with a server id, a response
TTL, a logger, a memory rental, an optional clock, and a dispatch capacity. The binding exposes
`id` as the required positional parameter.

## 4. Scheme and negotiation

- Scheme: `direct-rr-unreliable`
- Negotiation name: none
- Negotiation version: none

The client acquires its channel on the first request and reconnects with a new channel after a
loss. There is no admission exchange, session, or peer lifecycle callback. A request frame carries
the client-local request id; a response frame carries the id the server received.

## 5. Parameters

| Parameter | Type and unit | Required | Default | Meaning |
| --- | --- | --- | --- | --- |
| `id` | string | yes | none | Server name; the server binds it and the client connects to it. |
| `request_ttl` | integer, seconds | no | `3` | Client pending-window lifetime; valid only for a client transport. |
| `response_ttl` | integer, seconds | no | `3` | Server response-handle lifetime; valid only for a server transport. |
| `delivery` | struct | no | `{ type = "perfect" }` | Virtual delivery configuration for both client channel directions; valid only for a client transport. |

Every parameter is implementation configuration, not part of the abstract contract. The binding
**MUST** reject `request_ttl` on a server transport, `response_ttl` on a client transport, and
`delivery` on a server transport.

The `delivery` struct is the struct of the Raw Direct unreliable binding: a required `type` field
selecting `perfect` or `netem`, followed by the optional impairment fields. The binding **MUST**
validate `delivery` during `Create` without constructing a threaded netem system, and **MUST**
reject an unknown type and an incomplete loss-model parameter set, including `fourState` or
`gilbertElliot` without its parameter struct.

The client builds one fresh delivery system per direction for every connect from the same
`delivery` value. An absent `delivery` leaves the channel's default perfect systems, which are also
fresh for every connect.

## 6. Lifecycle and teardown

The server owns the process-wide registry entry for `id`, its channel dictionary, and one
`SerializedCallbackQueue<InboundFrame>`. The queue is created before the carrier registers, so a
request cannot arrive before the queue exists. A failed registration disposes the queue. The client
owns one fixed `GuidEndPoint` for its lifetime and the channel it creates lazily.

The server dispatch queue is opened by `OnStarted`. A frame that arrives before the gate opens is
held in a pending list bounded by `100` frames; the `101`st frame is released. Stop releases every
held frame and disposes the queue without waiting for a running `OnRequested`. A frame delivered
after the stop snapshot finds no queue and is released by the inbound path, because no other owner
remains for it.

The client's `Stop` and a channel disposal unsubscribe the disposal notification, remove the
channel from the server registry, and dispose the channel. A disposed channel is a loss, not a
transport failure: the next request reconnects. The carrier translates a defensive
`SendResult.NotConnected` into the no-peer result after clearing the channel under its lock, so a
later connect is not refused by a stale registry entry.

The carrier enforces these limits:

| Limit | Scope | Value |
| --- | --- | --- |
| Message maximum | Both directions | `1048576` bytes, excluding the request-id header |
| Pending pre-start frames | Server | `100` frames |
| Dispatch queue | Server | Configured capacity, default `100` items |

## 7. Failure, deviations, and protection

A disposed channel ends the current client session and removes the registry entry. The next request
reconnects with fresh delivery systems. A throwing delivery factory or install releases the frame,
the created systems, and the new channel, and returns `Error` without invoking the owner callback
under the carrier lock. A channel disposed during connect or install cannot own the systems installed
afterwards — its disposal already released the previous pair — so the carrier releases every created
system itself and releases the frame once. A delivery-system exception is caught, logs, releases that
frame, and returns `Error`. An inbound frame that is malformed, has a foreign source, or exceeds
`MessageMaxByteSize` is released and logged without stopping the transport.

This implementation declares four bounded deviations from the abstract type.

1. **Already-expired request presented.** Shared §6.5 says a request whose response TTL expired
   before presentation **SHOULD NOT** reach the application. The dispatch queue is opened only on
   start, so a frame can wait behind `OnStarted`. The request is presented with an already-closed
   handle, and `Response` returns `Error`; no response frame is sent. The handler **MUST** tolerate
   an already-closed handle and release the request.
2. **Callback after stop.** Shared §7.4 orders terminal notifications before `onStopped`. Queue
   disposal is non-blocking, so an `OnRequested` already running at stop may complete after
   `onStopped` begins. Its response handle is closed, and no other server callback overlaps it. The
   handler **MUST** tolerate one late request and release its payload.
3. **Callback during start.** Shared §7.2 says no handler callback begins before `Start` returns.
   `OnStarted` opens the gate during the final phase of `Start`, so a queued request may be
   presented after `OnStarted` but before the caller observes the return. The handler **MUST**
   tolerate a callback before it has observed `Start` returning.
4. **Bounded netem teardown.** Shared §7.3 asks for prompt carrier teardown. Stopping a client with
   `delivery = netem` may wait up to five seconds while `NetemDeliverySystem` joins its dequeue
   thread. The terminal-notification rule does not promise prompt carrier teardown under netem; the
   caller **MUST** tolerate a bounded stop delay.

The process boundary is the isolation boundary. The carrier adds no authentication,
confidentiality, or integrity protection.

## 8. Implementation-defined choices

| Area | Choice |
| --- | --- |
| Wire correlation | The client-local `ushort` request id is the first frame element; the response carries the received id. The server-global id stays local. |
| TTL values | `request_ttl` and `response_ttl` default to three seconds and are configured in whole seconds. |
| Pending storage | The client uses `RRUnreliableRequestTable`; the server holds no pending request table, only the dispatch queue. |
| Reconnect policy | The first request connects lazily. A channel disposal is a loss; the next request reconnects with fresh delivery systems. There is no background retry. |
| Capacity | The server dispatch queue is bounded by the configured capacity, default `100`; a full queue releases the frame. The pre-start pending list is bounded by `100`. |
| Payload limit | `1048576` bytes, excluding the request-id header and channel framing. |
| Server id uniqueness | `id` is unique across all Direct transports in the process; a duplicate server registration fails `Start`. |
| Callback scheduler | One server dispatch queue serializes `OnRequested`; client responses are delivered on the channel's callback path. |
| Failure detection | Channel disposal is the only session-loss signal; a missing server is detected during `ChannelManager.Connect`. |
| Logging | Malformed, oversized, foreign-route, and delivery-system failures are logged. A request dropped for a missing peer is not logged. |

## 9. Implementer checklist

- [ ] Register the scheme `direct-rr-unreliable` for `TransportType.RRUnreliable`.
- [ ] Reject `request_ttl` on a server, `response_ttl` on a client, and `delivery` on a server.
- [ ] Default both TTLs to three seconds.
- [ ] Validate `delivery` eagerly, including incomplete loss-model parameters.
- [ ] Build fresh delivery systems per direction for every connect.
- [ ] Create the dispatch queue before carrier registration and dispose it on failure.
- [ ] Hold frames received before `OnStarted` in a `100`-frame pending list and release the rest.
- [ ] Open the dispatch gate in `OnStarted` while holding the list lock.
- [ ] Start the response TTL at frame receipt.
- [ ] Serialize `OnRequested` through one dispatch queue.
- [ ] Release the frame on every failing connect, install, send, and dispatch path.
- [ ] Clear the channel under the lock before disconnecting on a loss.
- [ ] Remove the disposed channel from the registry and reconnect with fresh systems.
- [ ] Fail `Start` on a duplicate `id` and keep the first server usable.
- [ ] Release every held frame and the queue on stop.
- [ ] Send nothing and fail nothing when no server is registered.
- [ ] Keep the four declared deviations within their stated bounds.
- [ ] Add no authentication or integrity protection.
