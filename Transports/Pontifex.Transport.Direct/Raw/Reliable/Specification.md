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

1. It rejects a frame whose serialized size exceeds `HandshakeMaxByteSize`
   before parsing the body.
2. It rejects a frame whose leading type byte is not
   `HandshakeRequestMessage`.
3. It delegates the dataset structure, version, name, and capacity checks to
   the shared handshake implementation.
4. It calls the handler factory only after the version and name are accepted.
5. On acceptance it answers with `agreedMax`, the minimum of the requested and
   local message maxima.

The client validates the response version, name, and maximum before it reports
`OnConnected`. It maps a reject status to the abstract cause through
`ProtocolMismatch`, `HandshakeRejected`, or `TimeOut`.

## 5. Parameters

| Parameter | Type and unit | Required | Default | Meaning |
| --- | --- | --- | --- | --- |
| `id` | string | yes | none | Channel identifier; the server binds it and the client connects to it. |
| `handshake_max_byte_size` | integer, bytes | no | `128` | Inbound handshake frame limit. |
| `handshakeName` | string | no | `direct` | Handshake name sent to the server; constructor only, not exposed by the binding. |
| `messageMaxSize` | integer, bytes | no | `1048576` | Requested message limit; constructor only, not exposed by the binding. |
| `bufferCapacityBytes` | integer, bytes | no | `104857600` | Outbound buffer budget; constructor only, not exposed by the binding. |

Every parameter is implementation configuration, not part of the abstract
contract. A `messageMaxSize` below `handshake_max_byte_size` is invalid and
**MUST** be rejected at construction.

## 6. Lifecycle and teardown

The carrier owns the `Channel`, its two endpoint halves, and the registry entry
for the server `id`.

`Start` on the server registers `id` with `ChannelManager`; a duplicate `id`
fails `Start`. `Start` on the client creates its single endpoint, which connects
to the registered server as part of its start transition. A missing or rejecting
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
The carrier creates no `System.Threading.Timer` for the handshake.

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
| Reject linger | Zero; the reject frame is delivered synchronously before the channel closes. |
| Handshake path | Synchronous validation on the channel with no handshake timer. |
| Failure detection | Channel disposal is the only disconnection signal; a missing server is detected during endpoint start. |
| Logging | Channel and handshake failures are logged through `Log`. |
| Protection | None beyond the process boundary. |

## 9. Implementer checklist

- [ ] Register the scheme `direct` for `TransportType.RawReliable`.
- [ ] Use `direct` as the handshake name and `1` as the handshake version.
- [ ] Bind the server to the exact `id` and fail `Start` on a duplicate `id`.
- [ ] Pair the client and server halves by exact `id` equality and identify each
      client with a unique `GuidEndPoint`.
- [ ] Prepend the frame type byte and keep every handshake frame within
      `HandshakeMaxByteSize`.
- [ ] Reject an oversized handshake frame before parsing and reject a frame with
      an unexpected leading type byte.
- [ ] Validate structure, version, name, and capacity before calling the factory.
- [ ] Deliver the reject synchronously with zero linger and create no handshake
      timer.
- [ ] Reject a constructor message maximum below `HandshakeMaxByteSize`.
- [ ] Detach an admitted session channel from the server registry.
- [ ] Remove a channel from the server registry when it is disposed before
      admission.
- [ ] Close every channel still awaiting a handshake when the server stops.
- [ ] Close the channel and release the endpoint wiring and buffers on stop.
- [ ] Report a missing or rejecting server as an ordinary client stop, not a
      failed `Start`.
- [ ] Add no protection beyond the process boundary.
