# RRUnreliableClientTransport Coverage

| Field | Value |
| --- | --- |
| Layer | `RRUnreliableClientTransport` |
| Subject | `Pontifex.RR.Unreliable.RRUnreliableClientTransport` |
| Directory | `Tests/Pontifex.Tests/Transports/RR/Unreliable/Client` |
| Inheritance parent | `Pontifex.RR.Unreliable.RRUnreliableTransport` |
| Dependency | — |
| Contract | [RRUnreliableClientTransport.cs](../../../../../../Pontifex/Implementations/RR/Unreliable/RRUnreliableClientTransport.cs), [IRRUnreliableClientTransport.cs](../../../../../../Pontifex/Abstractions/Transports/RR/Unreliable/Client/IRRUnreliableClientTransport.cs), [IRRUnreliableRequestHandler.cs](../../../../../../Pontifex/Abstractions/Transports/RR/Unreliable/Client/IRRUnreliableRequestHandler.cs), [RRUnreliable specification](../../../../../../Pontifex/Abstractions/Transports/RR/Unreliable/Specification.md), [shared RR specification](../../../../../../Pontifex/Abstractions/Transports/RR/Specification.md) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `Constructor_WithNullServerEndPoint_Throws` | Section 4 constructor | A null fixed server route is rejected | The throw is `ArgumentNullException` for `serverEndPoint`. |
| `Constructor_WithNonPositiveRequestTtl_Throws` | Section 6.5 request TTL | A zero request TTL is rejected | The pending table raises the range check, so `ParamName` is `ttl`, not `requestTtl`. |
| `Constructor_ExposesServerEndPointAndRequestTtl` | Section 4 observables | The fixed route and request TTL are readable unchanged | The route instance is retained, not copied. |
| `Request_WithNullHandler_ThrowsBeforeConsumingPayload` | Section 9 result precedence | A null handler throws before the payload is validated or released | The caller still owns the payload after the throw. |
| `Request_WithNonRRHandler_ThrowsBeforeConsumingPayload` | Section 4 handler binding | A handler that does not derive from `RRRequestHandler` throws before the payload is consumed | The caller still owns the payload after the throw. |
| `Request_WithAlreadyBoundHandler_Throws` | Section 6.5 pending-window uniqueness | A live handler cannot start a second request | The second payload is not consumed by the rejected request. |
| `Request_AfterStop_ReturnsNotConnectedAndReleases` | Section 7 stopped transport | A stopped client releases the payload and reports `NotConnected` | The stop latch prevents a late provisional request from entering the carrier. |
| `Request_WithNullPayload_ReturnsInvalidMessage` | Section 9 message validity | A null payload is rejected without a callback and without a pending entry | The handler is unbound after the failure. |
| `Request_AboveMessageLimit_ReturnsMessageTooBigAndReleases` | Section 8 limits | A payload above `MessageMaxByteSize` is released and rejected | The framed size is checked after validation; the transport never sends it. |
| `Request_WhenHandlerCancelsBeforePublish_ReturnsErrorAndReleases` | Section 6.7 cancellation | A cancel that arrives before the request id is published abandons the request | The cancellation is consumed before the frame is submitted. |
| `Request_WhenStopRacesBeforeSend_ReturnsErrorAndUnbinds` | Section 7 stop race | A stop during the carrier send makes `Request` return `Error` and unbinds the handler | The post-send stop check wins over the successful carrier result. |
| `Request_WhenSendFrameReturnsNonOk_ReturnsResultAndUnbindsWithoutCallback` | Section 6.6 carrier failure | A non-`Ok` send result is returned, the pending entry is removed, and no callback follows | A later frame for that id is released as unknown. |
| `Request_WhenSendFrameThrows_ReturnsErrorAndUnbindsWithoutCallback` | Section 9 synchronous error | A throwing carrier send is converted to `Error` and the pending entry is removed | The exception is logged, not propagated. |
| `RequestId_IsPublishedBeforeSendFrame` | Section 6.6 request publication | The handler already holds the client-local id when the frame reaches the carrier | The test double observes the handler id from inside `SendFrame`. |
| `Response_WhenDeliveredBeforeRequestReturns_IsStillDelivered` | Section 6.6 early response | A response that arrives during the request send is delivered to the bound handler | The callback runs on the requesting thread inside `SendFrame`. |
| `Response_FromForeignRoute_IsReleasedWithoutCallback` | Section 6.1 routing | A response from another route is released and ignored | The fixed route is not authentication. |
| `Response_WhenMalformed_IsReleasedWithoutCallback` | Section 6.4 response header | A frame without a complete response header is released and ignored | The payload is not inspected after the header failure. |
| `Response_WhenOversized_IsReleasedWithoutCallback` | Section 8 limits | A response whose payload exceeds the message limit is released and ignored | The size is checked after the response header is popped. |
| `Response_ForUnknownId_IsReleasedWithoutCallback` | Section 6.5 pending window | A response for an id outside the pending window is released and ignored | The pending entry stays live. |
| `Response_AfterRequestTtlExpires_IsDroppedAndHandlerUnbound` | Section 6.5 request TTL | A frame after the exact TTL is released, the entry is removed, and the handler is unbound | The table expiry comparison is inclusive (`ExpiresAt <= Now`). |
| `Response_FirstFrameFixesMessageCount` | Section 6.4 message count | The first accepted frame fixes the count used by the whole sequence | The second frame is validated against the fixed count. |
| `Response_WithInconsistentMessageCount_IsDropped` | Section 6.4 message count | A later frame with a different count is released and ignored | The fixed count remains usable for a later matching frame. |
| `Response_WithMessageIdAtOrAboveCount_IsDropped` | Section 6.4 message ids | A message id equal to or above the count is released and ignored | The header is built manually because `EncodeResponse` rejects that id. |
| `Response_WithDuplicateMessageId_IsDropped` | Section 10 deduplication | A duplicate message id is released and produces no second callback | The request stays live for the remaining ids. |
| `Response_LastMessage_UnbindsHandlerAfterCallback` | Section 6.6 completion | The last message unbinds the handler after its callback returns | The same handler can start a new request immediately. |
| `Response_WhenHandlerThrows_IsIsolatedAndStillCompletes` | Section 6.6 exception isolation | A throwing callback is swallowed, the frame is released, and the sequence still completes | The transport stays valid and the handler is unbound by the last message. |
| `Response_Callbacks_AreSerializedPerRequest` | Section 5.1 / 6.6 callback serialization | Two concurrent frames for one request produce one callback at a time and ordered ids | The bounded `SpinUntil` wait on the worker thread state is only diagnostic; the active-callback counter is the behavioural assertion. |
| `Stop_FromInsideAResponseCallback_DoesNotDeadlock` | Section 6.6 / 7 stop from a callback | `Stop` completes from inside a response callback without waiting on the callback gate | The callback records the stop result; the assertion runs after `Receive` returns. |
| `Request_FromInsideAResponseCallback_IsAccepted` | Section 6.6 callback reentrancy | A nested `Request` from a response callback is accepted | A separate handler is used for the nested request. |
| `Cancel_RemovesPendingRequestWithoutCallback` | Section 6.7 cancellation | Cancel removes the pending entry and a later frame is released without a callback | The request id remains readable after unbinding. |
| `Cancel_BeforeSend_RemovesPendingEntryAndDropsLaterResponse` | Section 6.7 cancellation window | A cancel immediately before the send still removes the entry and the request returns `Ok` | Production does not re-check cancellation after the before-send hook. |
| `Cancel_InsideANonLastMessageCallback_DefersUnbind` | Section 6.7 cancellation | Cancel from a non-last callback leaves the handler bound until the callback returns | The recorded bound state is captured inside the callback and asserted afterwards. |
| `Stop_ClearsPendingWindowWithoutCallbacks` | Section 7 stop | Stop clears every pending entry without a completion callback | Each handler is unbound exactly once by the clear. |
| `Response_WhenTransportStopped_IsDropped` | Section 7 stopped transport | A response that arrives after stop is released and ignored | The pending window was already cleared by the stop. |
| `ExpiredRequest_AllowsTheSameHandlerToBeReused` | Section 6.5 expiry and reuse | An expired binding is reaped and the same handler can start a new request | The reaping path runs inside `Request` before the new reservation. |

The shared lifecycle, initialization, dispatch-gate, payload-validation, frame-size-formula, and
server-request-id tests are owned by the ancestor layers and recorded in
[`../Coverage.md`](../Coverage.md) and [`../Generic/Coverage.md`](../Generic/Coverage.md). They are
not repeated here.

## Planned

| Missing layer or test | Owner | Reason |
| --- | --- | --- |
| — | — | No known missing test for this layer. |

## Deferred by design

| Gap | Reason |
| --- | --- |
| Random request-id seed determinism, wraparound, and generator failure | The abstract type exposes no injectable seed: `CreateRandomRequestId` is `private static` and `_nextRequestId` is private, so a production change would be required. Publication is still pinned by `RequestId_IsPublishedBeforeSendFrame`. |
| BufferOverflow prefill | The pending table is private and not injectable; reaching the capacity through the public contract would need 65,536 accepted requests. Reflection is forbidden. |
| Real carriers, sockets, threads, and wall-clock timers | The default suite is deterministic and exercises no carrier; the three concurrency-categorised tests are the only callback-reentrancy facets, and only `Response_Callbacks_AreSerializedPerRequest` starts workers — the other two run the callback on the `Receive` caller thread. |
| Conformance controls | The conformance subsystem is out of scope for this suite. |
