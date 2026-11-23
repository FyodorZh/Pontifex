# RRReliableTransport Coverage

| Field | Value |
| --- | --- |
| Layer | Root |
| Subject | `Pontifex.RR.Reliable.RRReliableTransport` |
| Directory | `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport/` |
| Inheritance parent | none |
| Dependency | The transport base is public; the frame constants are internal, so the suite relies on the Direct assembly's `InternalsVisibleTo` entry. |
| Contract | [RRReliable specification](../../../../Pontifex/Abstractions/Transports/RR/Reliable/Specification.md), [shared RR specification](../../../../Pontifex/Abstractions/Transports/RR/Specification.md) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `Type_IsRRReliable` | Type identity | The base reports the reliable RR transport type | The type never varies by carrier. |
| `FrameMaxByteSize_IsTheMessageLimitPlusTheLargestHeaderAndOne` | Section 8 frame limit | The protected ceiling is the message limit plus the largest header plus one | Direct does not consume the property; it exists for carriers that frame messages. |
| `Start_IsOneTimeAndCallsTheCarrierOnce` | Section 7 start | The first start succeeds, the second fails, and the carrier starts once | Inherited `AnyTransport` one-time semantics. |
| `Stop_BeforeStart_DoesNotClearOrStopTheCarrier` | Section 7.3 stop | A pre-start stop does nothing to carrier or pending state | A later start is still possible. |
| `Stop_StopsTheCarrierWithTheSuppliedReasonAfterClearingPending` | Section 7.3/7.4 stop | Clearing runs once before the carrier stop and the supplied reason reaches it | The base clears pending state before stopping the carrier. |
| `Stop_SchedulesTerminalWorkBeforeOnStoppedBegins` | Section 7.4 stop ordering | Terminal work is already scheduled when `onStopped` runs, and the base does not wait for it | Uses the manual timer; a root override only schedules the work. |
| `Start_WhenTheCarrierReturnsFalse_InvalidatesWithoutOnStopped` | Section 7.2 failed start | A false carrier start invalidates without a stop callback | `AnyTransport` clears the started flag before failing. |
| `Start_WhenTheCarrierThrows_AbortsWithoutOnStopped` | Section 7.2 failed start | A throwing carrier start clears pending state, stops the carrier, and delivers no callback | The catch path aborts instead of running a normal stop. |
| `ValidatePayload_WhenNotStarted_ReleasesAndReturnsTheSelectedResult` | Section 9 precedence | A payload for a stopped transport is released and the role-selected result is returned | The role chooses `NotConnected` for a client and `Error` for a server. |
| `ValidatePayload_WithNull_ReturnsInvalidMessage` | Section 9 precedence | A null payload returns `InvalidMessage` and is not consumed | Null has no buffer to release. |
| `ValidatePayload_WhenStartedAndWithinTheLimit_AcceptsIt` | Section 8 payload limit | A started transport accepts a payload within the limit and keeps it alive | The limit is inclusive. |
| `ValidatePayload_AboveTheLimit_ReturnsMessageTooBigAndReleases` | Section 8 payload limit | An oversized payload returns `MessageTooBig` and is released | The check is on the serialized payload only, before the header. |
| `NextServerRequestId_StartsAtOneAndIncrements` | Server-global ids | The first id is one and ids increment | Ids are local to one server transport. |

## Planned

| Missing test | Owner layer |
| --- | --- |
| `RRReliableClientTransport` | Planned client layer (`Direct.RRReliableTransport.Client`), added with the core client behaviour. |
| Client request lifecycle, ordered responses, cancellation, TTLs, and stop scheduling | Planned client layer. |
| Client request-id allocation, provisional buffering, and callback pump | Planned client layer. |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Real timer firing for TTLs | The suite is deterministic; the manual factory drives scheduling, and the transport layers fire explicitly. |
| Carrier framing behaviour | Direct is in-process and does not consume `FrameMaxByteSize`; the later carrier specification owns framing. |
