# RRReliableTransport Coverage

| Field | Value |
| --- | --- |
| Layer | `RRReliableTransport` |
| Subject | `Pontifex.RR.Reliable.RRReliableTransport` |
| Directory | `Tests/Pontifex.Tests/Transports/RR/Reliable` |
| Inheritance parent | `Pontifex.RR.RRTransport` |
| Dependency | — |
| Contract | [RRReliableTransport.cs](../../../../../Pontifex/Implementations/RR/Reliable/RRReliableTransport.cs), [RRReliable specification](../../../../../Pontifex/Abstractions/Transports/RR/Reliable/Specification.md), [shared RR specification](../../../../../Pontifex/Abstractions/Transports/RR/Specification.md) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `Type_IsRRReliable` | Type identity | The base reports the reliable RR transport type | The type never varies by carrier. |
| `FrameMaxByteSize_IsTheMessageLimitPlusTheLargestHeaderAndOne` | Section 8 frame limit | The protected ceiling is the message limit plus the largest reliable header plus one | The shared formula test lives in the `RRTransport` layer; this row pins the reliable overhead constant. |
| `TimerFactory_IsInjected` | Section 7.1 timer seam | The constructor-supplied timer factory is the one the base uses | The test double supplies a manual factory instead of the system timer. |

The shared lifecycle, payload-validation, frame-size-formula, and server-request-id tests are
owned by the `RRTransport` ancestor layer and recorded in
[`../Coverage.md`](../Coverage.md). They are not repeated here.

## Planned

| Missing test | Owner layer |
| --- | --- |
| None. The generic, client, and server layers exist and own their behaviour. | — |

## Deferred by design

| Gap | Reason |
| --- | --- |
| Real timer firing for TTLs | The suite is deterministic; the manual factory drives scheduling, and the transport layers fire explicitly. |
| Carrier framing behaviour | Direct is in-process and does not consume `FrameMaxByteSize`; the carrier specification owns framing. |
