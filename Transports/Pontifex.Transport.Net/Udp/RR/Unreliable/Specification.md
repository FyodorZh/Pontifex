# UDP RRUnreliable Specification

## 1. Scope

The UDP carrier implements
[`RRUnreliable`](../../../../../Pontifex/Abstractions/Transports/RR/Unreliable/Specification.md)
over UDP datagrams.

This implementation adds datagram framing, address resolution, and TTL
configuration. It does not change the pending window, the response message
sequence, the correlation rules, or the ownership rules of the abstract type.

### 1.1 References

- [`RRUnreliable`](../../../../../Pontifex/Abstractions/Transports/RR/Unreliable/Specification.md)
  — the contract implemented here.

## 2. Normative language

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD NOT**,
and **MAY** in this document are to be interpreted as described in
[RFC 2119](https://datatracker.ietf.org/doc/html/rfc2119).

## 3. Contract

The carrier is selected by the scheme `udp_rr` and produces an
`IRRUnreliableClientTransport` or an `IRRUnreliableServerTransport` of the
abstract type. It is registered for `TransportType.RRUnreliable`.

## 4. Scheme and negotiation

- Scheme: `udp_rr`
- Negotiation name: none
- Negotiation version: none

The model has no handshake. A request frame carries the client-local request id.
A response frame carries the id the server received, the zero-based message id,
and the message count.

## 5. Parameters

| Parameter | Type and unit | Required | Default | Meaning |
| --- | --- | --- | --- | --- |
| `host` | string | yes | none | Local bind address on the server, remote address on the client. |
| `port` | integer, port number | yes | none | Local listen port on the server, remote port on the client; zero is rejected. |
| `request_ttl` | integer, seconds | no | `30` | Client pending-window lifetime; valid only for a client transport. |
| `response_ttl` | integer, seconds | no | `10` | Server response-handle lifetime; valid only for a server transport. |

Every parameter is implementation configuration, not part of the abstract
contract. `request_ttl` on a server transport and `response_ttl` on a client
transport are configuration errors.

This implementation exposes `MessageMaxByteSize` as `540` bytes, excluding the
request and response headers and the datagram framing. The value is the `548`-byte
guaranteed payload of the minimum IPv4 path minus the `7`-byte response header
and the `1`-byte element-count prefix growth.

## 6. Lifecycle and teardown

The server binds a UDP socket at the configured address; a bind failure fails
`Start`. The client resolves its `ServerEndPoint` at construction.

The carrier owns its socket, receive loop, and send path. Stop closes the socket
and releases the receive buffer.

## 7. Failure, deviations, and protection

A socket error after a successful start stops the transport through `onStopped`.
A datagram from an unknown source is dropped.

This implementation declares two bounded deviations from the abstract type.

1. **Server dispatch race.** A request is selected by reading the dispatch gate
   and then invoking `OnRequested`; the check is not synchronized with the stop
   transition. A frame that passes the check just before `Stop` may reach
   `OnRequested` while the transport is stopping, and may even run after
   `onStopped` has been invoked. The response handle already returns `Error`, no
   second callback is delivered for the request, and no other handler callback
   overlaps it. A handler that releases its own state in `onStopped` **MUST**
   tolerate one late request and release its payload.
2. **Client response dispatch race.** A response message taken from the pending
   window can reach `Response` while stopping, possibly after `onStopped` has
   run. The transport releases the message when the stop is detected. A client
   handler **MUST** tolerate one late response message and release its payload.

Both deviations violate the stop ordering of the abstract type, and both are
bounded by the response-handle and pending-window rules. They are removed when a
callback dispatcher serializes dispatch with the stop transition.

UDP provides no authentication, confidentiality, or integrity protection, and it
adds no source-spoofing protection.

## 8. Implementation-defined choices

| Area | Choice |
| --- | --- |
| Datagram framing | One request or one response message per datagram, with the request-id header and the response message headers. |
| Receive loop | A dedicated socket receive loop. |
| TTL defaults | 30 seconds for `request_ttl`, 10 seconds for `response_ttl`. |
| Payload limit | 540 bytes: `548` minus the `7`-byte response header and the `1`-byte prefix growth. |
| Address resolution | Host and port resolved at construction. |
| Capacity | Bounded by the datagram send queue; a full queue yields `BufferOverflow`. |

## 9. Implementer checklist

- [ ] Register the scheme `udp_rr` for `TransportType.RRUnreliable`.
- [ ] Require a non-zero port.
- [ ] Reject `request_ttl` on a server transport and `response_ttl` on a client
      transport.
- [ ] Default `request_ttl` to 30 seconds and `response_ttl` to 10 seconds.
- [ ] Expose `MessageMaxByteSize` as 540 bytes.
- [ ] Carry the client-local request id in request frames and the request id,
      message id, and message count in response frames.
- [ ] Drop a datagram from an unknown source.
- [ ] Fail `Start` on a bind error and report a later socket error through
      `onStopped`.
- [ ] Close the socket and release buffers on stop.
- [ ] Keep the two declared dispatch races within their stated bounds.
- [ ] Add no authentication or integrity protection.
