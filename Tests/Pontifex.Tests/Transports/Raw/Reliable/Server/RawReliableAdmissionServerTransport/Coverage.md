# RawReliableAdmissionServerTransport Layer Coverage

- **Layer:** RawReliableAdmissionServerTransport
- **Subject:** `Pontifex.Raw.Reliable.RawReliableAdmissionServerTransport`
- **Directory:** `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/RawReliableAdmissionServerTransport`
- **Inheritance parent:** `Pontifex.Raw.Reliable.RawReliableServerTransport<TServerHandler>`
- **Contract:** [RawReliableAdmissionServerTransport.cs](../../../../../../../Pontifex/Implementations/Raw/Reliable/RawReliableAdmissionServerTransport.cs),
  [IRawReliableServer.cs](../../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Server/IRawReliableServer.cs),
  [IRawReliableServerHandlerFactory.cs](../../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Server/IRawReliableServerHandlerFactory.cs),
  [IRawReliableServerHandler.cs](../../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableServerHandler.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `AdmissionServerTransport_ExposesReliableServerContract` | The reliable server transport exposes the reliable server and reliable server-transport contracts. | All five interface contracts are implemented. | `Type` is asserted separately. |
| `Type_IsRawReliable` | The facade fixes the transport type. | `Type` is `TransportType.RawReliable`. | — |
| `Init_WithNull_ThrowsWithoutChangingLifecycle` | A null factory is rejected without changing the lifecycle. | `ArgumentNullException` is thrown, the transport stays valid and unstarted, and a later valid `Init` succeeds. | — |
| `Init_FirstCallSucceeds_SecondFails` | `Init` is one-shot and keeps the first factory. | The second initializer is ignored and the first one serves `TryCreateHandler`. | The returned gate's `Inner` identifies the factory handler. |
| `Init_AfterStartAttempt_ReturnsFalse` | `Init` is illegal after a start attempt. | The transport rejects initialization and does not restart the carrier. | — |
| `Constructor_WithNullHandshakeName_ThrowsArgumentNullException` | The handshake name is required. | Construction throws `ArgumentNullException` with the `handshakeName` parameter name. | — |
| `Constructor_WithNonPositiveHandshakeMaxByteSize_ThrowsArgumentOutOfRangeException` | The handshake frame cap must be at least 1. | Zero is rejected with the `handshakeMaxByteSize` parameter name. | — |
| `Constructor_WithNonPositiveHandshakeTimeout_ThrowsArgumentOutOfRangeException` | The handshake timeout must be positive unless it is infinite. | `TimeSpan.Zero` is rejected with the `handshakeTimeout` parameter name. | — |
| `Constructor_WithInfiniteHandshakeTimeout_AcceptsValue` | `Timeout.InfiniteTimeSpan` disables the handshake deadline. | The infinite value is accepted and exposed unchanged. | The timer itself is not created at construction. |
| `Constructor_WithNegativeRejectLinger_ThrowsArgumentOutOfRangeException` | The reject linger must not be negative. | A negative value is rejected with the `rejectLinger` parameter name. | — |
| `Constructor_WithDefaults_UsesDocumentedHandshakeDefaults` | Omitted optional handshake arguments use documented defaults. | The name is passed through, the frame cap is 128, the timeout is 10 s, the reject linger is 2 s, and a timer factory is present. | The test double supplies the handshake name; the other three values are the production defaults. |
| `TryHandshake_BeforeStart_ReturnsRejectedWithoutCallingFactory` | The handshake requires a started transport. | A structurally valid request is rejected before start and the factory is not called. | The request is borrowed, so the test releases it. |
| `TryHandshake_AfterStop_ReturnsRejectedWithoutCallingFactory` | A stopped transport admits no handshakes. | The request is rejected and the factory is not called. | — |
| `TryHandshake_WithEmptyRequest_ReturnsRejectedWithoutCallingFactory` | The request must contain the three-element handshake dataset. | An empty list is rejected before the factory. | — |
| `TryHandshake_WithWrongElementCount_ReturnsRejectedWithoutCallingFactory` | The request element count is validated. | A two-element list is rejected before the factory. | — |
| `TryHandshake_WithWrongElementTypes_ReturnsRejectedWithoutCallingFactory` | The request element types are validated. | A list with a non-array name element is rejected before the factory. | — |
| `TryHandshake_WithVersionMismatch_ReturnsVersionMismatch` | The protocol version is validated. | A different version produces `VersionMismatch` and the factory is not called. | — |
| `TryHandshake_WithNameMismatch_ReturnsNameMismatch` | The handshake name is compared by content. | A different name produces `NameMismatch` and the factory is not called. | — |
| `TryHandshake_WithRequestMaxBelowOne_ReturnsCapacityExceeded` | A non-positive advertised maximum cannot receive a response. | The request is rejected with `CapacityExceeded` and the factory is not called. | Built explicitly because `BuildRequest` rejects a non-positive maximum. |
| `TryHandshake_WithRequestMaxBelowHandshakeMax_ReturnsCapacityExceeded` | The advertised maximum must cover the configured handshake frame cap. | A request one byte below the cap is rejected with `CapacityExceeded` and the factory is not called. | The default cap is 128 bytes. |
| `TryHandshake_WithoutFactory_ReturnsRejected` | Admission requires a bound factory. | A started transport with no factory rejects the handshake with no handler or response. | Uses the harness's initialization seam so the transport can start without `Init`. |
| `TryHandshake_WhenFactoryReturnsNull_ReturnsRejected` | A null factory result rejects the client. | The factory is called once, and the outcome has no handler or response. | — |
| `TryHandshake_WhenFactoryThrows_ReturnsRejectedAndLogs` | A throwing factory is isolated. | The exception is logged, the outcome is a rejection, and no handler or response is produced. | Uses `RecordingLogConsumer`; the assertion is on the exception message. |
| `TryHandshake_WhenRequestMaxIsBelowTransportMax_AcceptsWithRequestMax` | An accepted request negotiates the smaller of the request and transport maxima. | The status is `Accepted`, the handler is wrapped in `StopOnceServerHandler` around the factory handler, and the response parses with the version, name, and the request maximum. | The response is owned by the outcome and released by the test after parsing. |
| `TryHandshake_WhenRequestMaxExceedsTransportMax_AcceptsWithTransportMax` | The transport maximum caps the negotiated size. | The agreed maximum and the parsed response maximum both equal `MessageMaxByteSize`. | The request advertises 2048 while the transport is configured for 1024. |
| `TryHandshake_WhenResponseBuildFails_RejectsAndStopsCreatedHandlerOnce` | A response that cannot be built rejects admission and releases the created handler. | The outcome is a rejection with no response, and the factory handler receives one `OnStopped` with an `ExceptionFail`. | The transport is built with `messageMaxByteSize: 0`, so the negotiated maximum is 0 and `BuildResponse` throws. The handler is notified directly because it was never wrapped. |
| `TryCreateHandler_WhenNotStarted_ReturnsNull` | The factory requires a started transport. | No handler is created and the factory is not called before start. | — |
| `TryCreateHandler_ReturnsStopOnceWrapper` | A successful factory call returns a stop-once gate. | The result is a `StopOnceServerHandler` whose `Inner` is the factory's handler. | The wrapper changes handler identity; reference comparisons use `Inner`. |
| `TryCreateHandler_WhenFactoryThrows_ReturnsNull` | A factory exception is isolated. | The result is null and the transport remains valid and started. | — |
| `TryCreateHandler_AfterThrow_ReinvokesTheFactoryOnTheNextAdmission` | `parsing.md` reliable spec rules 4-5: a rejected `TryCreate` stays eligible. | After a throwing first call, the next admission calls the factory again and admits its handler. | `TryCreateCalls == 2`; the second handler is wrapped in `StopOnceServerHandler`. |
| `TryCreateHandler_AfterNullResult_ReinvokesTheFactoryOnTheNextAdmission` | A null factory result must not be cached. | After a null first call, the next admission obtains a handler from a second call. | The scripted factory returns null once, then a handler. |
| `TryCreateHandler_InvokesFactoryWithRemote` | The factory receives the remote endpoint. | The recorded remote endpoint is the same instance. | — |
| `TryCreateHandler_CallsDoNotOverlap` | Factory invocations are serialized. | Two gated contenders produce two calls with at most one in the factory at a time. | `Transports.Concurrency`; gated threads are joined with bounded timeouts and the gates are released in `finally`. |
| `TryCreateHandler_ConcurrentDistinctSources_SerializeFactoryCalls` | Factory serialization is global, not per remote route. | Two contenders for different `IEndPoint` values produce two calls with at most one in the factory at a time. | Same gated-thread discipline; the sources are distinct instances, so a per-source lock would let both enter. |
| `AbandonAdmission_WithHandler_NotifiesOnStoppedOnce` | An abandoned admission notifies the created handler. | The gated handler receives one `OnStopped`; a later direct call is ignored and the session slot is released. | Uses the exact gate returned by `TryCreateHandler` for both calls. |
| `CompleteAdmission_WhenSessionCannotStart_NotifiesOnStoppedOnce` | A session that cannot start has no terminal notification of its own, and the gate keeps the fallback one-shot. | The admission fallback delivers one `OnStopped`, the drained session adds no second one, and a later direct call through the gate is ignored. | Proves the stop-once wrapper, not the inherited admission mechanics (covered by `RawReliableServerTransportTests`). Covers the rejected-start path only. |
| `CompleteAdmission_WhenTransportStopping_DoesNotPreemptStartedSession` | A started session owns its terminal notification and the gate keeps it one-shot. | The admission fallback stays silent immediately after completion; draining the session delivers one `OnStopped` and releases the slot; a later direct call through the gate is ignored. | Proves the stop-once wrapper, not the inherited admission mechanics (covered by `RawReliableServerTransportTests`). The base server-session double does not raise `OnConnected`/`OnDisconnected`, so those are not asserted. |

## Planned

| Missing layer or test | Owner | Reason |
|---|---|---|
| — | — | No known missing tests for this layer. |

## Deferred by design

| Gap | Reason |
|---|---|
| Inherited session admission, registry, transport stop, and ticker behaviour. | Covered once by `RawReliableServerTransportTests`; not repeated here. |
| `RawReliable` conformance controls and suite. | Conformance controls and the conformance suite are deferred. |
| Handshake timer expiry and the `Timeout` status. | The deadline is scheduled by the carrier's handshake server endpoint after admission, not by `TryHandshake`; it belongs to the handshake endpoint and carrier layers. |
| Real carrier accept loops and end-to-end sessions. | Carrier and integration behaviour belong to the carrier suites. |

## Notes

- `TestRawReliableAdmissionServerTransport` supplies the carrier hooks and a hand-driven ticker and
  exposes `TryCreateHandler`, `TryHandshake`, `TryBeginAdmission`, `CompleteAdmission`,
  `AbandonAdmission`, `TryMarkInitialized`, `HasStartBeenAttempted`, `ActiveSessions`, and the
  handshake configuration getters. `Init` is already public on the subject.
- `StopOnceServerHandler` wraps every handler returned by the factory. The carrier, the session
  endpoint, and the admission fallback share the gate, so the wrapped handler observes at most one
  `OnStopped`.
- `RecordingRawReliableAdmissionServerHandlerFactory` records the call count and the last remote
  endpoint and does not serialize the calls itself; the transport's factory lock remains under test.
