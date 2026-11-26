# RRTransport Coverage

| Field | Value |
| --- | --- |
| Layer | `RRTransport` |
| Subject | `Pontifex.RR.RRTransport` |
| Directory | `Tests/Pontifex.Tests/Transports/RR` |
| Inheritance parent | `Pontifex.AnyTransport` |
| Dependency | — |
| Contract | [RRTransport.cs](../../../../Pontifex/Implementations/RR/RRTransport.cs), [IRRTransport.cs](../../../../Pontifex/Abstractions/Transports/RR/IRRTransport.cs), [shared RR specification](../../../../Pontifex/Abstractions/Transports/RR/Specification.md) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `Start_IsOneTimeAndCallsTheCarrierOnce` | Section 7.2 start | The first start succeeds, the second fails, and the carrier starts once | Inherited `AnyTransport` one-time semantics. |
| `Stop_BeforeStart_DoesNotClearOrStopTheCarrier` | Section 7.3 stop | A pre-start stop does nothing to carrier or pending state | A later start is still possible. |
| `Stop_StopsTheCarrierWithTheSuppliedReasonAfterClearingPending` | Section 7.3/7.4 stop | Clearing runs once before the carrier stop, the supplied reason instance reaches it, and the carrier is untouched inside the clear callback | The base clears pending state before stopping the carrier. |
| `Stop_ClearsPendingBeforeTheUserOnStoppedCallback` | Section 7.4 stop ordering | Pending state is already cleared when the user stop callback runs | The probe records from inside the clear and reads it from the user callback; neither callback asserts. |
| `Start_WhenTheCarrierReturnsFalse_InvalidatesWithoutOnStopped` | Section 7.2 failed start | A false carrier start invalidates without a stop callback | `AnyTransport` clears the started flag before failing. |
| `Start_WhenTheCarrierThrows_AbortsWithoutOnStopped` | Section 7.2 failed start | A throwing carrier start clears pending state, stops the carrier, and delivers no callback | The catch path aborts instead of running a normal stop. |
| `ValidatePayload_WhenNotStarted_ReleasesAndReturnsTheSelectedResult` | Section 9 precedence | A payload for a stopped transport is released and the role-selected result is returned | The role chooses `NotConnected` for a client and `Error` for a server. |
| `ValidatePayload_WhenNotStartedWithNull_ReturnsTheStateResult` | Section 9 precedence | A null payload for a stopped transport returns the selected state result, not `InvalidMessage` | The state check precedes the null check. |
| `ValidatePayload_WhenNotStartedWithOversizedPayload_ReturnsTheStateResultAndReleases` | Section 9 precedence | An oversized payload for a stopped transport returns the state result and is released | The state check precedes the size check. |
| `ValidatePayload_WhenStartedWithNull_ReturnsInvalidMessage` | Section 9 precedence | A null payload for a started transport returns `InvalidMessage` and is never released | A null buffer has no reference to release. |
| `ValidatePayload_WhenStartedAndWithinTheLimit_AcceptsIt` | Section 8 payload limit | A started transport accepts a payload within the limit and keeps it alive | The limit is inclusive. |
| `ValidatePayload_AboveTheLimit_ReturnsMessageTooBigAndReleases` | Section 8 payload limit | An oversized payload returns `MessageTooBig` and is released | The check is on the serialized payload only, before the header. |
| `ValidatePayload_WhenSerializationThrows_ReturnsInvalidMessageAndReleases` | Section 8 / section 9 serialization guard | A payload whose element size cannot be computed returns `InvalidMessage` and is released | A `default(UnionData)` element has `UnionDataType.Unknown`, for which `GetDataSize()` throws. |
| `NextServerRequestId_StartsAtOneAndIncrements` | Section 6.4 identifiers | The first server request id is one and ids increment | Ids are local to one server transport. |
| `FrameMaxByteSize_IsTheMessageLimitPlusTheOverheadAndOne` | Section 8 frame limit | The protected ceiling is the message limit plus the family overhead plus one | Uses a custom overhead byte count so the shared formula is asserted independently of the reliable constant. |

## Planned

| Missing layer or test | Owner | Reason |
| --- | --- | --- |
| None at this layer. Both families have their base layers; the remaining unreliable client and server layers are planned in their nearest ancestors. | — | — |

The reliable family lives under `Transports/RR/Reliable`. Its layers cover only their delta over
this base; the shared lifecycle, payload-validation, frame-size, and id-allocation tests recorded
above are not repeated there.

## Deferred by design

| Gap | Reason |
| --- | --- |
| Carrier framing behaviour | The abstract base only computes the ceiling; carriage and framing belong to the carrier specification. |
| Real carriers, sockets, threads, and wall-clock timers | The default suite is deterministic and exercises no carrier. |
| Conformance controls | The conformance subsystem is out of scope for this suite. |
