# RRUnreliableTransport Coverage

| Field | Value |
| --- | --- |
| Layer | `RRUnreliableTransport` |
| Subject | `Pontifex.RR.Unreliable.RRUnreliableTransport` |
| Directory | `Tests/Pontifex.Tests/Transports/RR/Unreliable` |
| Inheritance parent | `Pontifex.RR.RRTransport` |
| Dependency | — |
| Contract | [RRUnreliableTransport.cs](../../../../../Pontifex/Implementations/RR/Unreliable/RRUnreliableTransport.cs), [IRRUnreliableTransport.cs](../../../../../Pontifex/Abstractions/Transports/RR/Unreliable/IRRUnreliableTransport.cs), [RRUnreliable specification](../../../../../Pontifex/Abstractions/Transports/RR/Unreliable/Specification.md), [shared RR specification](../../../../../Pontifex/Abstractions/Transports/RR/Specification.md) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `Type_IsRRUnreliable` | Type identity | The family base reports the unreliable RR transport type | The type never varies by carrier. |
| `FrameMaxByteSize_UsesTheUnreliableOverhead` | Section 8 frame limit | The protected ceiling is the message limit plus the largest unreliable header plus one | The shared formula test lives in the `RRTransport` layer; this row pins the unreliable overhead constant. |

The shared lifecycle, payload-validation, frame-size-formula, and server-request-id tests are
owned by the `RRTransport` ancestor layer and recorded in [`../Coverage.md`](../Coverage.md). They
are not repeated here.

## Planned

| Missing layer or test | Owner | Reason |
| --- | --- | --- |
| — | — | No known missing test for this layer. |

## Deferred by design

| Gap | Reason |
| --- | --- |
| Inherited `RRTransport`/`AnyTransport` lifecycle tests | Ancestor layers own that coverage; this layer covers only the family delta. |
| Carrier framing behaviour | The abstract base only computes the ceiling; carriage and framing belong to the carrier specification. |
| Real carriers, sockets, threads, and wall-clock timers | The default suite is deterministic and exercises no carrier. |
| Conformance controls | The conformance subsystem is out of scope for this suite. |
