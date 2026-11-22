# RawUnreliableToRawReliableConverter Specification

## 1. Scope

`RawUnreliableToRawReliableConverter` implements
[`RawReliable`](../../Abstractions/Transports/Raw/Reliable/Specification.md)
over a
[`RawUnreliable`](../../Abstractions/Transports/Raw/Unreliable/Specification.md)
transport by emulating reliable delivery over a delivery manager.

This implementation adds a delivery manager, retransmission, deduplication, and
a converter handshake. It does not change the delivery guarantee, the send
results, or the buffer ownership of the reliable type.

### 1.1 References

- [`RawReliable`](../../Abstractions/Transports/Raw/Reliable/Specification.md)
  — the contract produced by the converter.
- [`RawUnreliable`](../../Abstractions/Transports/Raw/Unreliable/Specification.md)
  — the carrier contract consumed by the converter.

## 2. Normative language

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD NOT**,
and **MAY** in this document are to be interpreted as described in
[RFC 2119](https://datatracker.ietf.org/doc/html/rfc2119).

## 3. Contract

The converter `RawUnreliableToRawReliableConverter` reports
`From = TransportType.RawUnreliable` and `To = TransportType.RawReliable`. Its
`Convert` operation accepts a transport constructor and returns a constructor
that produces an `IRawReliableClient` when the input is an
`IRawUnreliableClient`, and an `IRawReliableServer` when the input is an
`IRawUnreliableServer`.

The converter is obtained from the conversion graph, not by a scheme name. It
also accepts optional memory and logger overrides.

## 4. Scheme and negotiation

- Scheme: none; the converter is selected by transport conversion
- Negotiation name: `converter`
- Negotiation version: the type contract version

The converter handshake runs as the first delivery-managed message, so it is
retried and deduplicated by the delivery layer:

```text
Request:  [version, "converter", messageMaxByteSize]
Accepted: [version, "converter", HandshakeStatus.Accepted, agreedMax]
Reject:   [serverVersion, "converter", HandshakeStatus.Rejected, text?]
```

The client inspects the status element before parsing the body. The handshake
message is never delivered to the application handler.

## 5. Parameters

| Parameter | Type and unit | Required | Default | Meaning |
| --- | --- | --- | --- | --- |
| none | none | none | none | The converter is selected by transport conversion, not by a parameter. |

## 6. Lifecycle and teardown

The converter creates a delivery-managed session over the unreliable transport.
The local maximum is `DeliveryManager.DeliveryMaxByteSize`. The server returns
`min(request.max, its own DeliveryMaxByteSize)`, and both endpoints apply the
agreed value.

`OnConnected` is raised only after the accepted response validates. Start and
stop follow the reliable lifecycle; stop releases the delivery manager and the
nested transport.

## 7. Failure, deviations, and protection

Because reliability is emulated above an unreliable carrier, delivery depends on
the delivery manager's retries and deduplication, not on the carrier. An
unrecoverable delivery-manager or nested-transport failure follows the reliable
failure path.

This implementation declares no deviation from the reliable type. It adds no
protection and relies on the nested carrier for any carrier-level protection.

## 8. Implementation-defined choices

| Area | Choice |
| --- | --- |
| Retry schedule | Chosen by the delivery manager. |
| Deduplication window | Bounded by the delivery manager. |
| Buffer strategy | Delivery-managed buffers, with optional memory override. |
| Handshake retry | The handshake is retried like any delivery-managed message. |
| Capacity | Bounded by `DeliveryMaxByteSize` and delivery-manager queues. |

## 9. Implementer checklist

- [ ] Report `From = RawUnreliable` and `To = RawReliable`.
- [ ] Produce an `IRawReliableClient` for an unreliable client input.
- [ ] Produce an `IRawReliableServer` for an unreliable server input.
- [ ] Run the converter handshake as the first delivery-managed message.
- [ ] Never deliver the handshake message to the application handler.
- [ ] Validate the status element before parsing the response body.
- [ ] Use `DeliveryManager.DeliveryMaxByteSize` as the local maximum.
- [ ] Agree on `min(request.max, server max)` and apply it on both peers.
- [ ] Raise `OnConnected` only after the accepted response validates.
- [ ] Release the delivery manager and nested transport on stop.
- [ ] Preserve the reliable delivery, ownership, and result rules.
