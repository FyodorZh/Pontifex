# RRReliableClientTransport Coverage

| Field | Value |
| --- | --- |
| Layer | `RRReliableClientTransport` |
| Subject | `Pontifex.RR.Reliable.RRReliableClientTransport` |
| Directory | `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport/Client/` |
| Inheritance parent | `Pontifex.RR.Reliable.RRReliableTransport` |
| Dependency | The transport and the frame codec are internal or protected; the suite relies on the Direct test assembly's `InternalsVisibleTo` entry. |
| Contract | [RRReliable specification](../../../../../Pontifex/Abstractions/Transports/RR/Reliable/Specification.md), [shared RR specification](../../../../../Pontifex/Abstractions/Transports/RR/Specification.md) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `OrderedResponses_AreDeliveredBeforeFinishNormal` | Section 6.6 ordered stream | Responses arrive in ordinal order and the completion follows them | The pump serializes the callbacks. |
| `ZeroResponseFinish_IsDeliveredImmediately` | Section 6.6 completion | A completion with no responses is delivered without waiting | The terminal position is zero. |
| `DuplicateResponseOrdinal_ProducesOneCallback` | Section 10 duplicate suppression | A repeated ordinal is dropped and released | Lower/duplicate ordinals never reach the handler. |
| `ResponseAfterFinish_IsDropped` | Section 6.6 no response after finish | A response after the terminal is dropped | The live table entry leaves with the terminal. |
| `OutOfOrderResponse_IsHeldUntilTheGapFillsAndFinishWaits` | Section 6.6 / section 10 ordering | An out-of-order response and the terminal wait for the missing ordinal | The terminal position is the highest known ordinal at arrival. |
| `LowerAndDuplicateOrdinals_AreDropped` | Section 10 duplicate suppression | A duplicate and a lower ordinal are not delivered twice | The state owns and releases a dropped frame. |
| `NonOkRequest_SchedulesNoFinish` | Section 9 non-Ok result | A failed request produces no callback and leaves no terminal scheduled | The handler is unbound before return. |
| `ProvisionalBuffer_IsReleasedWhenTheRequestReturnsNonOk` | Section 9 non-Ok result | A frame that arrived while provisional is released when the request fails | Nothing is delivered from the provisional buffer. |
| `FullIdTable_ReturnsBufferOverflow` | Section 9 outbound capacity | A full 65,536-id table returns `BufferOverflow` | Uses the internal `RequestsForTests` seam to prefill the table; the retry loop is bounded by the id space. |
| `StopDuringSend_ReturnsErrorWithNoCallback` | Section 9 synchronous errors | A stop inside `SendFrame` yields `Error` with no callback | The stop removes the provisional entry silently. |
| `SynchronousFinishInsideSendFrame_IsBufferedUntilOk` | Section 9 / provisional buffering | A terminal delivered inside `SendFrame` is held until `Request` accepts | Proves the buffer drains only after `Ok`. |
| `StopInjectedAtTheBeforeSendGate_ReturnsNotConnectedWithNoCallback` | Section 6.5/7.3 stop | A stop before the send re-check aborts the request | The payload is released and no frame is sent. |
| `CancelInjectedAtTheBeforeSendGate_ReturnsErrorWithNoCallback` | Section 6.7 cancellation | A cancel before the send re-check aborts the request | A provisional request is removed silently. |
| `TerminalKeepsTheHandlerBoundUntilTheFinishCallbackReturns` | Section 6.3/7.4 binding | A handler stays bound during `Finish` and can be reused only after the callback returns | A rebind attempt during the callback throws. |
| `RejectTerminal_ReachesTheClientAsRejected` | Section 6.6 completion | A rejection frame becomes `Finish(Rejected)` | Best-effort rejection is the terminal cause here. |
| `LostTerminal_FailsAtTheRequestTtlWithOneFinish` | Section 6.5 request TTL | A missing terminal fails once at the request TTL and disposes the timer | The manual factory drives the TTL. |
| `CancelOutsideACallback_DeliversOneCanceledAndSuppressesLateResponses` | Section 6.7 cancellation | Cancellation delivers one `Finish(Canceled)`, sends a best-effort cancel, and drops later responses | The timer is disposed at the terminal. |
| `CancelInsideAResponseCallback_DefersTheTerminal` | Section 6.7 | A cancel from inside a callback is delivered after that callback returns | The pump serializes the deferred terminal. |
| `Stop_SchedulesOneFailedPerPendingRequestBeforeOnStoppedAndDeliversAfter` | Section 7.4 stop ordering | Two pending requests are scheduled before `onStopped` and each fails once | The gate holds the pump while `onStopped` inspects the scheduled count. |
| `StopFromInsideAResponseCallback_DoesNotDeadlockAndDeliversFailed` | Section 7.4 stop from a callback | Stopping from a callback completes and schedules one `Finish(Failed)` | A normal stop keeps the transport valid. |
| `RequestId_IsPublishedBeforeSendFrame` | Section 6.3/6.4 ids | The id is bound before the carrier sees the frame | The random seed is deterministic. |
| `RequestIds_StartAtTheRandomSeedAndWrap` | Section 6.4 ids | The first id is the seed and the next wraps unchecked | Overrides `CreateRandomRequestId`. |
| `ResponseFromAForeignRoute_IsReleasedWithoutACallback` | Section 6.1 routing | A frame from another route is released and ignored | The route filter is not authentication. |
| `Request_WithHandlerChecks_ThrowsBeforeConsumingThePayload` | Section 6.3 handler checks | Null, wrong-type, and already-bound handlers throw before consuming the payload | The payload stays with the caller on a throw. |
| `Request_AfterStop_ReturnsNotConnectedAndReleases` | Section 9 transport state | A stopped client returns `NotConnected` and releases | The handler remains unbound. |
| `Request_AboveTheMessageLimit_ReturnsMessageTooBigAndReleases` | Section 8 message limit | An oversized request is rejected and released | The limit is on the payload only. |
| `Request_WithNullPayload_ReturnsInvalidMessage` | Section 9 message validity | A null request returns `InvalidMessage` and is not consumed | Null has no buffer. |
| `ThrowingClientHandler_IsIsolatedAndTheTerminalStillArrives` | Section 11 exception isolation | A throwing `Response` callback does not stop the transport or hide the terminal | The handler still receives `Finish`. |
| `ConcurrentResponsesForDifferentRequests_AreDeliveredSafely` | Section 11 concurrency | Two requests can receive responses from different threads | Table and per-request locks guard the state. |

## Planned

| Missing test | Owner layer |
| --- | --- |
| None. The Direct carrier round trip belongs to the Direct suite of the later stage. | — |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Real timer firing | The suite is deterministic; the manual factory fires the request TTL explicitly. |
| A live carrier round trip | The client layer uses the scripted carrier; the Direct carrier and binding are out of this stage. |
