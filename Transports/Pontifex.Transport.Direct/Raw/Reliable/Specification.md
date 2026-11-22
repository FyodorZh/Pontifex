# Direct RawReliable Specification

## 1. Scope

The Direct carrier implements
[`RawReliable`](../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md)
over an in-process channel.

This implementation adds the channel, the synchronous handshake path, and the
in-memory endpoint wiring. It does not change the delivery guarantee, the
lifecycle, the send results, or the buffer ownership of the abstract type.

### 1.1 References

- [`RawReliable`](../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md)
  — the contract implemented here.

## 2. Normative language

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD NOT**,
and **MAY** in this document are to be interpreted as described in
[RFC 2119](https://datatracker.ietf.org/doc/html/rfc2119).

## 3. Contract

The carrier is selected by the scheme `direct` and produces an
`IRawReliableClient` or an `IRawReliableServer` of the abstract type. It is
registered for `TransportType.RawReliable`.

## 4. Scheme and negotiation

- Scheme: `direct`
- Handshake name: `direct`
- Handshake version: the type contract version

The handshake request is the first channel message. The server validates it
synchronously, and the reject frame is delivered synchronously before the
channel closes.

## 5. Parameters

| Parameter | Type and unit | Required | Default | Meaning |
| --- | --- | --- | --- | --- |
| `id` | string | yes | none | Channel identifier; the client and server halves must agree. |
| `handshake_max_byte_size` | integer, bytes | no | `128` | Inbound handshake frame limit. |

Every parameter is implementation configuration, not part of the abstract
contract.

## 6. Lifecycle and teardown

The carrier owns the in-process channel and its endpoint pair. Start binds or
connects the channel half. Stop closes the channel and releases the endpoint
wiring.

The reject linger of this carrier is `TimeSpan.Zero`: the reject frame is
delivered before the channel closes, so no waiting is required. The carrier
creates no `System.Threading.Timer` for the handshake.

## 7. Failure, deviations, and protection

A channel-level failure terminates the session or the transport according to the
abstract failure rules. This implementation declares no deviation from the
abstract type.

The carrier runs in process. It provides no protection and relies on the process
boundary for isolation.

## 8. Implementation-defined choices

| Area | Choice |
| --- | --- |
| Channel implementation | In-process queue and endpoint pair selected by `id`. |
| Handshake path | Synchronous validation on the channel, with no handshake timer. |
| Reject path | Synchronous delivery, zero linger. |
| Scheduling | Endpoint callbacks use the carrier's in-process scheduler. |
| Logging | Channel failures are logged through `Log`. |
| Capacity | Bounded by the channel queue; a full queue yields `BufferOverflow`. |

## 9. Implementer checklist

- [ ] Register the scheme `direct` for `TransportType.RawReliable`.
- [ ] Use `direct` as the handshake name and the abstract handshake version.
- [ ] Pair the client and server halves by exact `id` equality.
- [ ] Deliver the handshake request as the first channel message.
- [ ] Validate the handshake synchronously and deliver the reject before closing.
- [ ] Use zero linger and create no handshake timer.
- [ ] Enforce `handshake_max_byte_size` on the inbound handshake.
- [ ] Close the channel and release the endpoints on stop.
- [ ] Add no protection beyond the process boundary.
