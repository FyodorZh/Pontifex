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
| `RawUnreliableTransport_ExposesUnreliableRawTransportContract` | `RawUnreliableTransport` is an `IRawUnreliableTransport` and an `IRawTransport`. | The unreliable marker contract is exposed by the marker facade. | The class adds no lifecycle, carrier, or message-handling behaviour, so only its delta is asserted. |
| `GetControls_ExposesRawUnreliableTransportConformanceControl` | The conformance control published by `GetControls` is the typed unreliable transport control. | Exactly one control is added and it is the same instance the typed `Conformance` property returns; the public control interface is implemented. | The control is inspected but never armed, and no failure is injected: the suite does not use the conformance subsystem as a test mechanism. |
| `TryMakeReliable_WhenOwnerImplementsDebugReliability_DelegatesToIt` | The conformance control forwards to `IRawUnreliableDebugReliability` on the transport instance. | `TryMakeReliable` returns the carrier's answer and calls it once per invocation. | The double implements the interface, so this pins the forwarding branch. |
| `TryMakeReliable_WhenOwnerLacksDebugReliability_ReturnsFalse` | An owner without debug-reliability support is declined, not treated as an error. | The call returns `false` and throws nothing. | The second double deliberately omits the interface. The owner is bound by `AnyTransport` during construction, so the branch is reachable without adding production surface. |

## Planned

| Missing layer or test | Owner | Reason |
|---|---|---|
| — | — | Both abstract children of this layer have their own layer (`Client`, `Server`). |

## Deferred by design

| Gap | Reason |
|---|---|
| Re-testing inherited `AnyTransport`/`RawTransport` lifecycle/facade behaviour. | Ancestor layers own that coverage. |
| Arming the conformance gates and injecting failures. | The conformance subsystem is out of scope for this suite; the control is asserted only as a published object. |
| Real carriers, sockets, threads, and timers. | Carrier behaviour belongs to the carrier/conformance suites. |

## Notes

- `RawUnreliableTransport` is a marker facade over `RawTransport`. This layer intentionally records only
  the marker interface, the typed conformance-control view, and both debug-reliability lookup branches.
- `TestRawUnreliableTransport : RawUnreliableTransport, IRawUnreliableDebugReliability` supplies the
  inherited abstract members and the debug switch, while
  `TestRawUnreliableTransportWithoutDebugReliability` supplies the same members without the interface.
  Both call the transport constructor with the default conformance control, which `AnyTransport` binds to
  the instance.
- The three conformance-control tests are the only place in this suite that touch the conformance control,
  and none of them arms or injects; `TryMakeReliable` is a debug-mode request, not a checkpoint.
