# RawReliableTransport Layer Coverage

- **Layer:** RawReliableTransport
- **Subject:** `Pontifex.Raw.Reliable.RawReliableTransport`
- **Directory:** `Tests/Pontifex.Tests/Transports/Raw/Reliable`
- **Inheritance parent:** `Pontifex.Raw.RawTransport`
- **Contract:** [RawReliableTransport.cs](../../../../../Pontifex/Implementations/Raw/Reliable/RawReliableTransport.cs),
  [IRawReliableTransport.cs](../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableTransport.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `RawReliableTransport_ExposesReliableRawTransportContract` | `RawReliableTransport` is an `IRawReliableTransport`. | The reliable marker contract is exposed. | The class adds no lifecycle or message-handling behaviour, so only its delta is asserted. |

## Planned

| Missing layer or test | Owner | Reason |
|---|---|---|
| — | — | Every abstract child of this layer has its own layer or is recorded as planned in `Raw/Coverage.md`. |

## Deferred by design

| Gap | Reason |
|---|---|
| Re-testing inherited `AnyTransport`/`RawTransport` behaviour. | Ancestor layers own that coverage. |
| Conformance controls. | The conformance subsystem is out of scope and planned for a rewrite. |
| Real carriers, sockets, threads, and timers. | Carrier behaviour belongs to the carrier/conformance suites. |

## Notes

- `RawReliableTransport` is a marker facade over `RawTransport`. This layer intentionally
  records only the marker interface; constructor forwarding and inherited observables are
  covered by `RawTransportTests` and `AnyTransportTests`.
- `TestRawReliableTransport : RawReliableTransport` supplies the inherited abstract members and a
  trivial conformance control only to satisfy the production constructor.
