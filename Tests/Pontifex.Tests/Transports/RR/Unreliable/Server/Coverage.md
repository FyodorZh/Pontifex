# RRUnreliableServerTransport Coverage

| Field | Value |
| --- | --- |
| Layer | `RRUnreliableServerTransport` |
| Subject | `Pontifex.RR.Unreliable.RRUnreliableServerTransport` |
| Directory | `Tests/Pontifex.Tests/Transports/RR/Unreliable/Server` |
| Inheritance parent | `Pontifex.RR.Unreliable.RRUnreliableTransport<THandler>` |
| Dependency | — |
| Contract | [RRUnreliableServerTransport.cs](../../../../../../Pontifex/Implementations/RR/Unreliable/RRUnreliableServerTransport.cs), [IRRUnreliableServerTransport.cs](../../../../../../Pontifex/Abstractions/Transports/RR/Unreliable/Server/IRRUnreliableServerTransport.cs), [IRRUnreliableServerHandler.cs](../../../../../../Pontifex/Abstractions/Transports/RR/Unreliable/Server/IRRUnreliableServerHandler.cs), [IRRUnreliableResponse.cs](../../../../../../Pontifex/Abstractions/Transports/RR/Unreliable/Server/IRRUnreliableResponse.cs), [RRUnreliable specification](../../../../../../Pontifex/Abstractions/Transports/RR/Unreliable/Specification.md), [shared RR specification](../../../../../../Pontifex/Abstractions/Transports/RR/Specification.md) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `Constructor_WithNonPositiveResponseTtl_Throws` | Section 4 constructor | A zero response TTL is rejected | The throw is `ArgumentOutOfRangeException` for `responseTtl`. |
| `Constructor_ExposesResponseTtl` | Section 4 observables | The configured response TTL is readable unchanged | The TTL bounds the whole response sequence. |
| `OnFrameReceived_WithNullFrame_DoesNothing` | Section 7.1 callback matrix | A null inbound frame is ignored without a handler callback or send | No buffer exists to release. |
| `OnFrameReceived_WithMalformedFrame_ReleasesAndDoesNotPresent` | Section 6.3 / 8 inbound validation | A frame without a request-id header is released and never presented | The payload is not inspected after the header failure. |
| `OnFrameReceived_WithOversizedFrame_ReleasesAndDoesNotPresent` | Section 8 limits | An oversized request payload is released and never presented | The request header is popped before the size check. |
| `OnFrameReceived_BeforeStart_ReleasesWithoutPresenting` | Section 7 lifecycle | A frame received before the transport starts is released without a callback | The dispatch gate is closed until a successful start. |
| `OnFrameReceived_WhenHandlerNotInitialized_ReleasesWithoutPresenting` | Section 6.2 server association | A frame received without a bound handler is released without a callback | `Init` is the only handler-binding path. |
| `OnRequested_WhenHandlerAccepts_KeepsHandleOpen` | Section 6.3 request acceptance | An accepted request keeps its response handle open and usable | The default message count is one. |
| `OnRequested_WhenHandlerRejects_LaterResponseReturnsError` | Section 6.3 rejection | A rejected request closes the handle, so the later send returns `Error` and releases the payload | Rejection is silent to the client. |
| `OnRequested_WhenHandlerThrows_LogsRejectsAndTransportStaysValid` | Section 6.3 exception isolation | A throwing handler is logged, the request is treated as rejected, and the transport stays valid and started | The test double receives a `RecordingLogConsumer`; the exception message is asserted in the log. |
| `OnRequested_AssignsServerGlobalIdsFromOne` | Section 6.4 identifiers | Server-global request ids start at one and increment | The id is local bookkeeping and is not asserted on the wire. |
| `Response_SendsClientRequestIdMessageIdAndCount` | Section 6.4 / 6.6 wire correlation | A response carries the client request id, the zero-based message id, the declared count, and the request's source route | The captured frame is decoded before the double releases it. |
| `Response_DefaultMessageCountIsOne` | Section 4 message count | A handle that never sets the count declares one message | The default is visible through the observable and the wire. |
| `SetMessageCount_AcceptsOneTo256` | Section 4 message count range | Both range endpoints, one and 256, are accepted | Each boundary uses a separate handle because the declaration is one-time. |
| `SetMessageCount_RejectsOutOfRange` | Section 4 range before state | Values below one and above 256 throw `ArgumentOutOfRangeException` on a closed handle | Proves the range check precedes the state checks. |
| `SetMessageCount_IsOneTimeBeforeFirstResponse` | Section 4 one-time declaration | A second declaration before the first response throws `InvalidOperationException` | The first declaration succeeds. |
| `SetMessageCount_AfterFirstResponseThrows` | Section 4 declaration lock | A declaration after the first response throws while the handle is still open | A second response still succeeds, proving the throw is not caused by closure. |
| `SetMessageCount_AfterCloseThrows` | Section 4 closed handle | A declaration after the last message closed the handle throws `InvalidOperationException` | A default-count handle closes after one accepted send. |
| `SetMessageCount_AfterExpiryThrows` | Section 6.5 response TTL | A declaration after the response TTL expired throws `InvalidOperationException` | The clock advances past the inclusive expiry boundary. |
| `SetMessageCount_AfterStopThrows` | Section 7 stop | A declaration after the transport stopped throws `InvalidOperationException` | The handle is not closed by the stop, but it can no longer answer. |
| `Response_WhenSendFrameReturnsNonOk_DoesNotAdvanceAndCanRetry` | Section 6.6 send result | A non-`Ok` send result keeps the message id and the retry reuses it | The captured frames show ids `0, 0, 1`. |
| `Response_WhenSendFrameThrows_ReturnsErrorAndCanRetry` | Section 6.6 synchronous failure | A throwing send is converted to `Error`, keeps the id, and the retry succeeds | The exception is logged, not propagated. |
| `Response_WithNullPayload_ReturnsInvalidMessage` | Section 6.6 result precedence | A null payload returns `InvalidMessage` without consuming the handle | The handle stays open for a later valid send. |
| `Response_WithOversizedPayload_ReturnsMessageTooBigAndReleases` | Section 6.6 / 8 limits | A payload above `MessageMaxByteSize` is released and returns `MessageTooBig` | The check is on the serialized payload before the response header is added. |
| `Response_DeclaredSequenceSendsZeroBasedIds` | Section 6.6 message sequence | A declared sequence sends message ids from zero in accepted-send order | All frames carry the declared count. |
| `Response_AfterLastMessage_ReturnsError` | Section 6.6 handle closure | A send after the last message returns `Error` and releases the payload | The handle closed on the accepted last message. |
| `Response_AfterResponseTtlExpires_ReturnsError` | Section 6.5 response TTL | A send after the response TTL expired returns `Error` and releases the payload | The server expiry is strict `Now > ExpiresAt`; the clock advances by the TTL plus one tick. |
| `Response_AfterTransportStopped_ReturnsError` | Section 7 stopped transport | A send after the transport stopped returns `Error` and releases the payload | The handle cannot answer a stopped transport. |
| `OnFrameReceived_AfterStop_ReleasesWithoutPresenting` | Section 7 stopped transport | A frame received after stop is released without a callback | The dispatch gate is closed by the stop. |
| `OnFrameReceived_DuplicateClientRequest_IsPresentedAgain` | Section 10 duplicate delivery | The same client request presented twice reaches the handler twice with distinct handles and ids | RRUnreliable has no base-level duplicate suppression. |
| `OnFrameReceived_FromDifferentClients_HaveIndependentHandles` | Section 6.2 routing | Two clients with the same client-local id receive independent handles and each send returns to its own route | The server-global ids keep the handles distinct. |
| `Response_ConcurrentCallsProduceOneSequence` | Section 11 handle serialization | Two concurrent sends on one handle produce one sequence with ids zero and one | A bounded `SpinUntil` thread-state wait is diagnostic; the active-send counter and the recorded sequence are the behavioural assertions. |

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
| `OnRequested` serialization across requests | Serialization is carrier-owned: the abstract base calls the handler directly, and the Direct suite covers it with `ServerHandler_IsNeverConcurrent`. |
| Server-global id observation on the wire | The server-global id is local bookkeeping; the wire carries only the client-local request id, so the id is asserted through `IRRUnreliableResponse.RequestId`. |
| Observable handle closure | The public `IRRUnreliableResponse` surface does not expose closure; a closed or expired handle is observed through a later `Response` returning `Error` and a later `SetMessageCount` throwing. |
| Real carriers, sockets, threads, and wall-clock timers | The default suite is deterministic and exercises no carrier; only `Response_ConcurrentCallsProduceOneSequence` starts workers, and it releases its gate in `finally`. |
| Conformance controls | The conformance subsystem is out of scope for this suite. |
