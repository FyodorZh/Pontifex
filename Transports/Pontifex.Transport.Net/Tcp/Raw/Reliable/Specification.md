# TCP RawReliable Specification

## 1. Scope

The TCP carrier implements
[`RawReliable`](../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md)
over a TCP socket.

This implementation adds the socket, the length-prefixed framing, the receiver
and sender loops, the connect and accept paths, and the handshake transport. It
does not change the delivery guarantee, the lifecycle, the send results, or the
buffer ownership of the abstract type.

### 1.1 References

- [`RawReliable`](../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md)
  — the contract implemented here.

## 2. Normative language

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD NOT**,
and **MAY** in this document are to be interpreted as described in
[RFC 2119](https://datatracker.ietf.org/doc/html/rfc2119).

## 3. Contract

The carrier is selected by the scheme `tcp` and produces an `IRawReliableClient`
or an `IRawReliableServer` of the abstract type. It is registered for
`TransportType.RawReliable`.

## 4. Scheme and negotiation

- Scheme: `tcp`
- Handshake name: `tcp`
- Handshake version: the type contract version

The handshake is the `RawReliable` handshake. Request, response, and reject
frames are length-prefixed, and the server enforces `HandshakeMaxByteSize` on
the inbound prefix before allocating the body.

## 5. Parameters

| Parameter | Type and unit | Required | Default | Meaning |
| --- | --- | --- | --- | --- |
| `host` | string | yes | none | Local bind address on the server, remote address on the client. |
| `port` | integer, port number | yes | none | Local listen port on the server, remote port on the client. |
| `disconnection_timeout` | integer, seconds | no | `180` | Idle or disconnect grace period of the carrier. |
| `handshake_max_byte_size` | integer, bytes | no | `128` | Inbound handshake frame limit. |
| `handshake_timeout` | integer, seconds | no | `10` | Handshake deadline on both peers. |
| `reject_linger` | integer, milliseconds | no | `2000` | Time a rejected socket stays open so the client can read the reject. |

Every parameter is implementation configuration, not part of the abstract
contract.

## 6. Lifecycle and teardown

The server owns a listening socket and one connected socket per session. The
client owns one connected socket per connection attempt.

Start binds the listening socket on the server and begins the accept loop, or
begins the connect attempt on the client. A bind or accept failure that is
detected during start fails `Start`; a failure detected after `Start` returned
`true` is reported as an ordinary stop through `onStopped`.

The carrier tracks sockets that are still in the handshake and closes them when
the transport stops. After writing a reject frame, it keeps the socket open for
`reject_linger` and then closes it.

Stop closes the listening socket, every session socket, and every tracked
handshake socket, and releases the carrier's buffers and timers.

## 7. Failure, deviations, and protection

A socket error during a session terminates that session and raises
`OnDisconnected` followed by `OnStopped`. A listener failure is an unrecoverable
transport failure.

An undecodable frame whose length prefix is valid is logged and dropped; the
receive stream continues, so a later frame still flows. The receive compositor
is sized with the framed payload cap (`MessageMaxByteSize` plus the maximum
frame overhead), so an application payload exactly at `MessageMaxByteSize`
frames within the receive cap and is accepted; the shared endpoint then enforces
the exact payload limit after popping the frame type.

A length prefix above the framed cap cannot be resynchronized because the
stream has consumed the prefix and the body boundary is unknown. The socket is
then torn down as an unrecoverable carrier failure. This is an explicit, bounded
deviation from the abstract rule that malformed data never invalidates the
transport; it applies only to an unrecoverable length-prefix error.

A pre-`OnConnected` payload is buffered and drained after the callback, or
released on stop or pre-connect buffer overflow, exactly as the shared
`RawReliableEndpoint` defines.

This implementation declares no other deviation from the abstract type. It adds
no authentication, confidentiality, or integrity protection; TCP provides none.

## 8. Implementation-defined choices

| Area | Choice |
| --- | --- |
| Framing | Length-prefixed frames with a control byte preceding the payload. |
| Concurrency | Dedicated receiver and sender per socket; accept loop on the server. |
| Socket options | Keep-alive, buffer sizes, and no-delay follow the platform defaults used by the carrier. |
| `disconnection_timeout` semantics | The carrier treats it as a grace period before tearing down a socket. |
| Logging | Carrier failures and undecodable frames are logged through `Log`. |
| Capacity | Bounded by the socket send queue; a full queue yields `BufferOverflow`. |
| Receive cap | The framed payload limit plus the maximum frame overhead; the shared endpoint enforces the exact payload limit. |

## 9. Implementer checklist

- [ ] Register the scheme `tcp` for `TransportType.RawReliable`.
- [ ] Use `tcp` as the handshake name and the abstract handshake version.
- [ ] Validate the inbound handshake length before allocating the body.
- [ ] Enforce `HandshakeMaxByteSize` on the inbound handshake prefix.
- [ ] Apply `handshake_timeout` to the client and server handshake.
- [ ] Keep a rejected socket open for `reject_linger` after writing the reject.
- [ ] Close listening, session, and handshake sockets on stop.
- [ ] Fail `Start` on a bind or listen error, and report a later failure through
      `onStopped`.
- [ ] Map a session socket error to `OnDisconnected` then `OnStopped`.
- [ ] Release socket, buffer, and timer resources on teardown.
- [ ] Size the receive compositor for the framed payload cap, not the payload
      cap alone.
- [ ] Log and drop an undecodable frame without stopping the session; document
      the unrecoverable length-prefix deviation.
- [ ] Add no authentication or protection beyond the abstract contract.
