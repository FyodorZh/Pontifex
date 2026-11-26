# Direct RawReliable Specification

## 1. Scope

The Direct carrier implements
[`RawReliable`](../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md)
over an in-process channel.

This implementation adds the channel and its process-wide registry, the
synchronous handshake path, the in-memory endpoint wiring, and the channel
teardown that the session endpoint owns. It does not change the delivery
guarantee, the lifecycle, the send results, or the buffer ownership of the
abstract type.

### 1.1 References

- [`RawReliable`](../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md)
  — the contract implemented here.

## 2. Normative language

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD NOT**,
and **MAY** in this document are to be interpreted as described in
[RFC 2119](https://datatracker.ietf.org/doc/html/rfc2119).

## 3. Contract

The carrier is registered for `TransportType.RawReliable` with the scheme
`direct`. The binding is `RawReliableDirectBinding`, and the concrete transports
are `RawReliableDirectClientTransport` and `RawReliableDirectServerTransport`.
Each transport is constructed with a channel `id`, a logger, and a memory
rental; the binding exposes `id` as the required positional parameter.

The carrier produces an `IRawReliableClient` or an `IRawReliableServer` of the
abstract type.

## 4. Scheme and negotiation

- Scheme: `direct`
- Handshake name: `direct`
- Handshake version: `1`

The carrier sends the abstraction's flat handshake dataset with one leading
frame type byte. It adds no length prefix, because complete messages cross the
channel as single lists.

```text
request:  HandshakeRequestMessage
          version (short) | name (string) | messageMaxByteSize (int)
response: HandshakeResponseMessage
          version (short) | name (string) | agreedMax (int)
reject:   HandshakeRejectMessage
          serverVersion (short) | serverName (string) | status (byte) |
          text (string, optional)
```

The server validates an inbound handshake synchronously, in this order:

1. It rejects a frame whose complete framed size exceeds `HandshakeMaxByteSize`
   before parsing the body. The type byte is included in that total.
2. It rejects a frame whose leading type byte is not `HandshakeRequestMessage`.
3. It delegates the dataset structure, version, name, and capacity checks to the
   shared handshake implementation.
4. It calls the handler factory only after the version and name are accepted.
5. On acceptance it answers with `agreedMax`, the minimum of the requested and
   local message maxima, after validating the complete framed response against
   `HandshakeMaxByteSize`. A reject whose text would exceed the limit falls back
   to a minimal reject with no text.

The client validates the response version, name, and maximum before it reports
`OnConnected`. It enforces `HandshakeMaxByteSize` on the complete framed
response and reject, including the type byte. It maps a reject status to the
abstract cause through `ProtocolMismatch`, `HandshakeRejected`, or `TimeOut`.

Both roles start a positive handshake deadline. The client starts it after the
request is admitted and cancels it when the response or reject wins. The server
starts one timer per waiting handshake and cancels it when the handshake is
claimed, completed, removed, or the server stops. On expiry the server claims
the entry, removes it, abandons the admission, sends a `Timeout` reject when the
channel is still usable, and closes the channel.
## 5. Parameters

| Parameter | Type and unit | Required | Default | Meaning |
| --- | --- | --- | --- | --- |
| `id` | string | yes | none | Channel identifier; the server binds it and the client connects to it. |
| `handshake_max_byte_size` | integer, bytes in `1..2147483647` | no | `128` | Complete framed handshake limit, including the type byte. |
| `message_max_size` | integer, bytes in `1..2147483647` | no | `1048576` | Requested message limit, also accepted by the constructors. |
| `buffer_capacity_bytes` | integer, bytes | no | `104857600` | Outbound buffer budget. |
| `handshake_timeout` | integer, seconds, positive | no | `10` | Handshake deadline for both roles. |
| `handshakeName` | string | no | `direct` | Handshake name sent to the server; constructor only, not exposed by the binding. |

Every parameter is implementation configuration, not part of the abstract
contract. A message maximum below `handshake_max_byte_size` is invalid and
**MUST** be rejected at binding validation and at construction. The public
constructors reject a non-positive or infinite handshake timeout; only the
internal test constructor may disable the deadline.

## 6. Lifecycle and teardown

The carrier owns the `Channel`, its two endpoint halves, and the registry entry
for the server `id`.

`Start` on the server registers `id` with `ChannelManager` under the `direct`
scheme; a duplicate `id` within that scheme fails `Start`. The same `id` may
exist under another Direct scheme, and a client sees only its own scheme.
`Start` on the client creates its single endpoint, which connects to the
registered server as part of its start transition. A missing or rejecting
server stops the client endpoint; it is not a failed `Start`.

The server tracks channels that have not yet completed a handshake. An admitted
session endpoint takes ownership of its channel and detaches it from the server
registry. A channel that is disposed before admission is removed from the
server registry.

`Stop` unregisters the server `id`, closes every channel that is still waiting
for a handshake request, and releases the endpoint wiring and buffered messages.
An admitted session endpoint closes its channel when it stops.

The reject linger of this carrier is `TimeSpan.Zero`: the reject frame is
delivered synchronously before the channel closes, so no waiting is required.
Both roles nevertheless enforce the positive handshake deadline through
`Pontifex.Utils.ITimerFactory`. The system factory uses a
`System.Threading.Timer`; a test injects a manual factory through the internal
constructor.


An inbound frame with no frame type, an unknown frame type, or a body above
`MessageMaxByteSize` is logged, released, and dropped. It does not stop the
session; later traffic still flows. Transport framing never counts toward
`MessageMaxByteSize`: the carrier measures the application payload before the
frame type is prepended, and the shared endpoint measures the true body after
the frame type is popped.

A payload that arrives after the handshake but before the handler's
`OnConnected` callback returns is buffered in arrival order and drained
immediately after the callback. It is delivered exactly once and in order, not
dropped. The pre-connect buffer is capped at 64 messages and at
`64 * MessageMaxByteSize` bytes; exceeding either cap releases every buffered
payload and the triggering payload, then stops the session with a `TextFail`
reason. A stop before or during the callback releases the buffer without
delivering it.

## 7. Failure, deviations, and protection

A channel-level failure terminates the session or the transport according to the
abstract failure rules. A server `id` collision is a start failure. A handshake
rejection is an ordinary connection outcome, not a transport failure.

This implementation declares no deviation from the abstract type. It adds no
authentication, confidentiality, or integrity protection; the process boundary is
the isolation boundary.

## 8. Implementation-defined choices

| Area | Choice |
| --- | --- |
| Carrier and wire encoding | In-process `Channel`; each message is one complete `UnionDataList`, with no byte serialization. |
| Addressing | The server is a `StringEndPoint` holding `id`; each client transport owns a unique `GuidEndPoint`. |
| Callback scheduler | Each endpoint owns a serial action queue; channel delivery may run on the sending thread. |
| Queue capacity and drain rate | Bounded by the endpoint buffer capacity, which defaults to `104857600` bytes; a full queue yields `BufferOverflow`. |
| Framing | The frame type is prepended on send and popped on receive; the payload limit excludes it, and the framed receive body is measured again against the limit. |
| Pre-connect payloads | Buffered in arrival order (64 messages, 64 payload maxima) and drained after `OnConnected`; an overflow stops the session. |
| Reject linger | Zero; the reject frame is delivered synchronously before the channel closes. |
| Handshake path | Synchronous validation on the channel plus a positive deadline from the handshake timer factory. |
| Failure detection | Channel disposal is the only disconnection signal; a missing server is detected during endpoint start. |
| Logging | Channel and handshake failures, malformed frames, and oversized bodies are logged through `Log`. |
| Protection | None beyond the process boundary. |

## 9. Implementer checklist

- [ ] Register the scheme `direct` for `TransportType.RawReliable`.
- [ ] Use `direct` as the handshake name and `1` as the handshake version.
- [ ] Bind the server to the exact `id` and fail `Start` on a duplicate `id`
      within the `direct` scheme.
- [ ] Pair the client and server halves by exact `id` equality and identify each
      client with a unique `GuidEndPoint`.
- [ ] Prepend the frame type byte and keep every handshake frame within
      `HandshakeMaxByteSize`.
- [ ] Reject an oversized handshake frame before parsing and reject a frame with
      an unexpected leading type byte.
- [ ] Validate structure, version, name, and capacity before calling the factory.
- [ ] Deliver the reject synchronously with zero linger and enforce the positive
      handshake deadline in both roles.
- [ ] Enforce `HandshakeMaxByteSize` on the complete framed request, response,
      and reject, including the type byte.
- [ ] Fall back to a minimal reject when the reject text would exceed
      `HandshakeMaxByteSize`.
- [ ] Reject a constructor or binding message maximum below
      `HandshakeMaxByteSize`.
- [ ] Detach an admitted session channel from the server registry.
- [ ] Remove a channel from the server registry when it is disposed before
      admission.
- [ ] Close every channel still awaiting a handshake when the server stops.
- [ ] Close the channel and release the endpoint wiring and buffers on stop.
- [ ] Report a missing or rejecting server as an ordinary client stop, not a
      failed `Start`.
- [ ] Drop and log a malformed or unknown frame without stopping the session.
- [ ] Exclude transport framing from `MessageMaxByteSize` in both directions.
- [ ] Buffer a pre-`OnConnected` payload and drain it after the callback, or
      release it on stop or overflow.
- [ ] Add no protection beyond the process boundary.
