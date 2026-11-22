# Log Specification

## 1. Scope

`log` is a transparent wrapper that implements
[`RawReliable`](../../Abstractions/Transports/Raw/Reliable/Specification.md)
over exactly one nested `RawReliable` transport and logs its traffic and
lifecycle events.

This implementation adds logging only. It does not change the delivery
guarantee, the lifecycle, `MessageMaxByteSize`, the send results, or the buffer
ownership of the abstract type.

### 1.1 References

- [`RawReliable`](../../Abstractions/Transports/Raw/Reliable/Specification.md)
  — the contract implemented here.

## 2. Normative language

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD NOT**,
and **MAY** in this document are to be interpreted as described in
[RFC 2119](https://datatracker.ietf.org/doc/html/rfc2119).

## 3. Contract

The wrapper is selected by the scheme `log` and produces an `IRawReliableClient`
or an `IRawReliableServer` of the abstract type. It is registered for
`TransportType.RawReliable` and is obtained through a required nested
`RawReliable` transport.

## 4. Scheme and negotiation

- Scheme: `log`
- Negotiation name: none
- Negotiation version: none

The wrapper performs no negotiation, adds no round trip, and introduces no
message that the peer can observe.

## 5. Parameters

| Parameter | Type and unit | Required | Default | Meaning |
| --- | --- | --- | --- | --- |
| `nested` | transport, `RawReliable` | yes | none | The transport wrapped by `log`. |

The parameter is implementation configuration, not part of the abstract
contract.

## 6. Lifecycle and teardown

The wrapper creates a logging endpoint and a logging handler around the nested
transport's types. Every callback, send, and lifecycle transition of the nested
transport is forwarded unchanged and additionally logged.

Start, stop, and terminal ordering are those of the nested transport. Stop
releases the wrapper and stops the nested transport.

## 7. Failure, deviations, and protection

Every failure and terminal sequence of the nested transport is forwarded
unchanged. This implementation declares no deviation from the abstract type.

Logging writes to the configured log sink and adds no protection.

## 8. Implementation-defined choices

| Area | Choice |
| --- | --- |
| Log format | Chosen by the logging wrapper. |
| Log sink | The transport `Log` instance. |
| Logged content | Lifecycle events and message metadata; payload bytes only where the wrapper chooses. |
| Overhead | One wrapper layer with no additional scheduling. |

## 9. Implementer checklist

- [ ] Register the scheme `log` for `TransportType.RawReliable`.
- [ ] Require exactly one nested `RawReliable` transport.
- [ ] Perform no negotiation and add no round trip.
- [ ] Forward every callback unchanged.
- [ ] Forward `Send` unchanged and return the nested result.
- [ ] Forward `MessageMaxByteSize` unchanged.
- [ ] Forward the terminal lifecycle exactly once.
- [ ] Log through the transport `Log` instance.
- [ ] Stop the nested transport on stop.
