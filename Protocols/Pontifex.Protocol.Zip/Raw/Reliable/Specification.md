# Zip Specification

## 1. Scope

`zip` is a wrapper that implements
[`RawReliable`](../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md)
over exactly one nested `RawReliable` transport, compressing application
payloads on the wire.

This implementation adds the compression of the nested carrier and its own
negotiation exchange. It does not change the delivery guarantee, the lifecycle,
the send results, or the buffer ownership of the abstract type.

### 1.1 References

- [`RawReliable`](../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md)
  — the contract implemented here.

## 2. Normative language

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD NOT**,
and **MAY** in this document are to be interpreted as described in
[RFC 2119](https://datatracker.ietf.org/doc/html/rfc2119).

## 3. Contract

The wrapper is selected by the scheme `zip` and produces an `IRawReliableClient`
or an `IRawReliableServer` of the abstract type. It is registered for
`TransportType.RawReliable` and is obtained through a required nested
`RawReliable` transport.

## 4. Scheme and negotiation

- Scheme: `zip`
- Negotiation name: `zip`
- Negotiation version: `1`
- Custom tail: empty

After the inner transport reports `OnConnected`, the wrapper performs its own
negotiation in a flat `UnionDataList` envelope:

```text
Request:  [version, name, NegotiationStatus.Request,  ...custom tail]
Response: [version, name, NegotiationStatus.Accepted, ...custom tail]
Reject:   [serverVersion, serverName, NegotiationStatus.Rejected, text?]
```

The status element is always the discriminator, so a custom tail element cannot
be mistaken for a status. The wrapper enforces its own handshake limit of 128
bytes and a handshake deadline of 10 seconds, and it waits 2 seconds before
disconnecting the inner transport after a reject.

## 5. Parameters

| Parameter | Type and unit | Required | Default | Meaning |
| --- | --- | --- | --- | --- |
| `nested` | transport, `RawReliable` | yes | none | The transport wrapped by `zip`. |
| `compression_level` | integer, 0 to 9 | no | `9` | Compression effort applied to application payloads. |

Every parameter is implementation configuration, not part of the abstract
contract.

## 6. Lifecycle and teardown

The wrapper performs its negotiation only after the inner transport reports
`OnConnected`. It gates `OnReceived`, `Send`, and `IsConnected` until the
negotiation is admitted, and it mirrors the reliable terminal lifecycle exactly
once.

Start starts the nested transport. Stop stops the nested transport and releases
the negotiation state. Wrappers may be stacked.

## 7. Failure, deviations, and protection

A negotiation reject or timeout terminates the wrapper session with the same
terminal sequence as the abstract type. A failure of the nested transport is
reported as the wrapper's own failure.

This implementation declares no deviation from the abstract type. Compression is
not encryption and provides no confidentiality.

## 8. Implementation-defined choices

| Area | Choice |
| --- | --- |
| Compression algorithm | Chosen by the wrapper implementation. |
| Negotiation limit | Fixed at 128 bytes. |
| Negotiation deadline | Fixed at 10 seconds. |
| Reject linger | Fixed at 2 seconds. |
| Stacking | Any number of `RawReliable` wrappers may be stacked. |

## 9. Implementer checklist

- [ ] Register the scheme `zip` for `TransportType.RawReliable`.
- [ ] Require exactly one nested `RawReliable` transport.
- [ ] Negotiate with name `zip` and version `1`, with an empty custom tail.
- [ ] Use the status element as the discriminator and never as an index.
- [ ] Gate `OnReceived`, `Send`, and `IsConnected` until admission.
- [ ] Mirror the terminal lifecycle exactly once.
- [ ] Enforce the 128-byte negotiation limit and the 10-second deadline.
- [ ] Wait 2 seconds before disconnecting the inner transport after a reject.
- [ ] Apply `compression_level` within the range 0 to 9.
- [ ] Report nested failures as wrapper failures.
- [ ] Provide no confidentiality claim for compression.
