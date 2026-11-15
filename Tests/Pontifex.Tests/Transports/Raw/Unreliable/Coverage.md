# RawUnreliableTransport Layer Coverage

- **Layer:** RawUnreliableTransport
- **Subject:** `Pontifex.Raw.Unreliable.RawUnreliableTransport`
- **Directory:** `Tests/Pontifex.Tests/Transports/Raw/Unreliable`
- **Inheritance parent:** `Pontifex.Raw.RawTransport`
- **Contract:** [RawUnreliableTransport.cs](../../../../../Pontifex/Implementations/Raw/Unreliable/RawUnreliableTransport.cs),
  [IRawUnreliableTransport.cs](../../../../../Pontifex/Abstractions/Transports/Raw/Unreliable/IRawUnreliableTransport.cs),
  [IRawUnreliableDebugReliability.cs](../../../../../Pontifex/Implementations/Raw/Unreliable/IRawUnreliableDebugReliability.cs),
  [RawUnreliableTransportConformanceControl.cs](../../../../../Pontifex/Implementations/Raw/Unreliable/RawUnreliableTransportConformanceControl.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `RawUnreliableTransport_ExposesUnreliableRawTransportContract` | `RawUnreliableTransport` is an `IRawUnreliableTransport` and an `IRawTransport`. | The unreliable marker contract is exposed, which the legacy base deliberately omitted. | The class adds no lifecycle, carrier, or message-handling behaviour, so only its delta is asserted. |
| `GetControls_ExposesRawUnreliableTransportConformanceControl` | The conformance control published by `GetControls` is the typed unreliable transport control. | Exactly one control is added and it is the same instance the typed `Conformance` property returns; the public control interface is implemented. | The control is inspected but never armed, and no failure is injected: the suite does not use the conformance subsystem as a test mechanism. |
| `TryMakeReliable_WhenOwnerImplementsDebugReliability_DelegatesToIt` | The conformance control prefers `IRawUnreliableDebugReliability` on the transport instance. | `TryMakeReliable` returns the carrier's answer and calls it once per invocation. | The double implements the interface, so this pins the preferred lookup branch. The legacy fallback cast is still exercised by the conformance suite against the four legacy carriers and is deleted in Stage 6. |

## Planned

| Missing layer or test | Owner | Reason |
|---|---|---|
| — | — | Both abstract children of this layer have their own layer (`Client`, `Server`). The two dead Ack/NoAck legacy bases are recorded as planned in `Raw/Coverage.md`. |

## Deferred by design

| Gap | Reason |
|---|---|
| Re-testing inherited `AnyTransport`/`RawTransport` lifecycle/facade behaviour. | Ancestor layers own that coverage. |
| Arming the conformance gates and injecting failures. | The conformance subsystem is out of scope for this suite; the control is asserted only as a published object. |
| Real carriers, sockets, threads, and timers. | Carrier behaviour belongs to the carrier/conformance suites. |

## Notes

- `RawUnreliableTransport` is a marker facade over `RawTransport`. This layer intentionally records only
  the marker interface, the typed conformance-control view, and the preferred debug-reliability lookup.
- `TestRawUnreliableTransport : RawUnreliableTransport, IRawUnreliableDebugReliability` supplies the
  inherited abstract members and the debug switch; the transport constructor is called with the default
  conformance control.
- The root layer's conformance assertion is the only place in this suite that touches the conformance
  control, and it neither arms nor injects.
