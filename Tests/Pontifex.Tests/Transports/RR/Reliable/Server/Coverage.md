# RRReliableServerTransport Coverage

| Field | Value |
| --- | --- |
| Layer | `RRReliableServerTransport` |
| Subject | `Pontifex.RR.Reliable.RRReliableServerTransport` |
| Directory | `Tests/Pontifex.Tests/Transports/RR/Reliable/Server` |
| Inheritance parent | `Pontifex.RR.Reliable.RRReliableTransport<THandler>` |
| Dependency | — |
| Contract | [RRReliableServerTransport.cs](../../../../../../Pontifex/Implementations/RR/Reliable/RRReliableServerTransport.cs), [RRReliable specification](../../../../../../Pontifex/Abstractions/Transports/RR/Reliable/Specification.md), [shared RR specification](../../../../../../Pontifex/Abstractions/Transports/RR/Specification.md) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `ResponseAndFinish_ProduceOrderedFramesWithServerIdsFromOne` | Sections 6.3, 6.4, 6.6 | Responses carry consecutive ordinals, the completion follows, and server ids start at one | The client-local id travels on the wire; the server id is local. |
| `ResponseOrdinal_IsCommittedOnlyAfterOk` | Section 6.6 ordinal commitment | A failed send consumes no ordinal and the client stream does not stall | The next successful response reuses ordinal one. |
| `ResponseHandle_MatrixOfFinishRejectAndClosedCalls` | Section 6.6 precedence | The handle throws after acceptance on `Reject`, errors after completion, and errors after stop | Stop is checked before the rejected state. |
| `Finish_WhenTheSendFails_LeavesTheHandleRetryable` | Section 6.6 / section 9 failed send | A failed terminal send leaves the handle retryable and the retry completes it once | The terminal is committed only after a successful send. |
| `Reject_WhenTheSendFails_LeavesTheHandleRetryable` | Section 6.6 / section 9 failed send | A failed rejection send leaves the handle retryable and the retry succeeds | Rejection is best effort but still closes the handle on success. |
| `Reject_AfterTheResponseTtlClosedTheHandle_ReturnsError` | Section 6.5 response TTL | After the response TTL closes the handle, `Reject`, `Response`, and `Finish` all return `Error` | The TTL close is indistinguishable from any other closed handle. |
| `Reject_ClosesTheHandleAndRepeatedRejectReturnsOk` | Section 6.6 rejection | The first `Reject` sends and closes; a repeated call returns `Ok`; later responses throw | The rejection is best effort. |
| `Cancel_ClosesTheLiveHandleAndSuppressesLaterResponses` | Section 6.7 cancellation | A client cancel closes the handle, disposes the timer, and later handle calls return `Error` | The server sends no cancellation callback. |
| `CancelOfAQueuedRequest_PreventsPresentation` | Section 6.7 / section 10 | A cancelled request that is still queued is never presented | The dispatch worker checks the closed handle on dequeue. |
| `ResponseTtlWhileQueued_SendsFailedAndNeverPresents` | Section 6.5 response TTL | The response TTL starts at receipt; expiry sends `Finish(Failed)` and the queued request is released without presentation | The manual factory fires the second request's timer while the first blocks the queue. |
| `FullDispatchQueue_ReleasesTheOverflowingRequest` | Section 12 capacity | A full dispatch queue rejects the frame without blocking and the client can fail at its TTL | The queue capacity is configured to one. |
| `DuplicateRequest_IsSuppressedLiveAndAfterEveryTerminalCause` | Section 10 duplicate suppression | A duplicate live request is ignored and a completed id is suppressed after `Finish`, `Reject`, `Cancel`, and response-TTL expiry | All four terminal causes record the client-local id. |
| `TwoClientsWithTheSameClientLocalId_DoNotSuppressEachOther` | Section 10 per-session scope | The completed window and live map are per session | The same id from another route is a different request. |
| `OnRequested_IsSerializedAcrossRequests` | Section 6.3 / section 11 | The single server handler never runs two callbacks concurrently | The dispatch queue is one serialized worker. |
| `ThrowingHandler_RejectsTheUndecidedHandle` | Section 6.3 throwing handler | A throwing handler is logged, an undecided handle is rejected, and the transport stays valid | The sent frame is `Finish(Rejected)`. |
| `RequestBeforeOnStarted_IsHeldUntilTheReadyGateOpens` | Section 7.2 start gating | A request received before `OnStarted` is not presented until the ready gate opens | The test uses the internal dispatch seam instead of a timing race. |
| `FullPendingListBeforeStart_ReleasesTheOverflowingFrame` | Section 12 capacity | The pre-start pending list is bounded and releases the overflow | The list shares the dispatch capacity. |
| `StopDispatch_ReleasesARequestStillQueued` | Section 7.4 stop | A request still queued at stop is released and never presented | The handle is closed and the id is terminal. |
| `OnSessionClosed_ClosesOnlyThatSession` | Section 7.4 failure / session closure | Closing one session errors its handles and leaves another session usable | A later request from the closed route creates a fresh session. |
| `BlockedResponse_WithConcurrentStop_DoesNotHang` | Section 11 / section 7.4 stop | Stop marks the handle closed without waiting for an in-flight `Response` | The response gate is not taken by stop. |
| `TwoThreadsOnOneHandle_ProduceOneTerminalResult` | Section 6.6 exactly once | Two concurrent `Finish` calls produce one `Ok` and one terminal frame | The reentrant gate serializes the calls. |

## Planned

| Missing test | Owner layer |
| --- | --- |
| The transport-level full-window `Reject` path | The session window is unit-tested in `RRReliableHelpers`; a transport-level path would need 65,536 presented requests. The branch is a direct `Reject` on a new handle and is covered by the response-handle matrix and the session window test. |
| None for the remaining server behaviour. | — |

## Deferred by design

| Gap | Reason |
| --- | --- |
| Real response-TTL timing | The suite is deterministic; the manual factory fires the timer explicitly. |
| A live carrier round trip | The server layer uses the scripted carrier; the Direct carrier and binding belong to the Direct suite. |
| A full 65,536-request transport run | Exhausting the window through the transport would present 65,536 requests; the window itself is exercised directly in `RRReliableHelpers`. |
