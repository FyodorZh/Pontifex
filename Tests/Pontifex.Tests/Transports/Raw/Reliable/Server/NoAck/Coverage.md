# RawReliableNoAckServerTransport Layer Coverage

- **Layer:** RawReliableNoAckServerTransport
- **Subject:** `Pontifex.Raw.Reliable.NoAck.RawReliableNoAckServerTransport`
- **Directory:** `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/NoAck`
- **Inheritance parent:** `Pontifex.Raw.Reliable.RawReliableServerTransport<TServerHandler>`
- **Contract:** [RawReliableNoAckServerTransport.cs](../../../../../../../Pontifex/Implementations/Raw/Reliable/NoAck/RawReliableNoAckServerTransport.cs),
  [IRawReliableNoAckServer.cs](../../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/NoAck/Server/IRawReliableNoAckServer.cs),
  [IRawReliableNoAckServerHandlerFactory.cs](../../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/NoAck/Server/IRawReliableNoAckServerHandlerFactory.cs),
  [IRawReliableNoAckServerHandler.cs](../../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/NoAck/Server/IRawReliableNoAckServerHandler.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `NoAckServerTransport_ExposesReliableNoAckServerContract` | The NoAck server transport exposes the NoAck server and reliable server contracts. | All five interface contracts are implemented. | `Type` is asserted separately. |
| `Type_IsRawReliableNoAck` | The facade fixes the transport type. | `Type` is `TransportType.RawReliableNoAck`. | — |
| `Init_WithNull_ThrowsWithoutChangingLifecycle` | A null factory is rejected without changing the lifecycle. | `ArgumentNullException` is thrown, the transport stays valid and unstarted, and a later valid `Init` succeeds. | — |
| `Init_FirstCallSucceeds_SecondFails` | `Init` is one-shot and keeps the first factory. | The second initializer is ignored and the first one serves `TryCreateHandler`. | The returned gate's `Inner` identifies the factory handler. |
| `Init_AfterStartAttempt_ReturnsFalse` | `Init` is illegal after a start attempt. | The transport rejects initialization and does not restart the carrier. | — |
| `TryCreateHandler_WhenNotStarted_ReturnsNull` | The factory requires a started transport. | No handler is created and the factory is not called before start. | — |
| `TryCreateHandler_ReturnsStopOnceWrapper` | A successful factory call returns a stop-once gate. | The result is a `StopOnceNoAckServerHandler` whose `Inner` is the factory's handler. | The wrapper changes handler identity; reference comparisons use `Inner`. |
| `TryCreateHandler_WhenFactoryThrows_ReturnsNull` | A factory exception is isolated. | The result is null and the transport remains valid and started. | — |
| `TryCreateHandler_InvokesFactoryWithRemote` | The factory receives the remote endpoint. | The recorded remote endpoint is the same instance. | — |
| `TryCreateHandler_CallsDoNotOverlap` | Factory invocations are serialized. | Two gated contenders produce two calls with at most one in the factory at a time. | `Transports.Concurrency`; gated threads are joined with bounded timeouts and the gates are released in `finally`. |
| `AbandonAdmission_WithHandler_NotifiesOnStoppedOnce` | An abandoned admission notifies the created handler. | The gated handler receives one `OnStopped`; a later direct call is ignored and the session slot is released. | Uses the exact gate returned by `TryCreateHandler` for both calls. |
| `CompleteAdmission_WhenSessionCannotStart_NotifiesOnStoppedOnce` | A session that cannot start has no terminal notification of its own. | The admission fallback delivers one `OnStopped` and the drained session adds no second one. | Covers the rejected-start path only. |
| `CompleteAdmission_WhenTransportStopping_DoesNotPreemptStartedSession` | A started session owns its terminal notification. | The admission fallback stays silent immediately after completion; draining the session delivers one `OnStopped` and releases the slot. | The base server-session double does not raise `OnConnected`/`OnDisconnected`, so those are not asserted. |

## Planned

| Missing layer or test | Owner | Reason |
|---|---|---|
| — | — | No known missing tests for this layer. |

## Deferred by design

| Gap | Reason |
|---|---|
| Inherited session admission, registry, transport stop, and ticker behaviour. | Covered once by `RawReliableServerTransportTests`; not repeated here. |
| Direct NoAck carrier. | Direct NoAck is deferred; only the TCP `tcp-noack` carrier is implemented. |
| NoAck conformance controls and suite. | NoAck conformance controls and the conformance suite are deferred. |
| Real carrier accept loops and end-to-end no-handshake sessions. | Carrier and integration behaviour belong to the carrier suites. |
| Logging verification for factory exceptions. | Requires a custom observable `ILogSink`; the transport-level policy is already covered. |

## Notes

- `TestRawReliableNoAckServerTransport` supplies the carrier hooks and a hand-driven ticker and
  exposes `TryCreateHandler`, `TryBeginAdmission`, `CompleteAdmission`, `AbandonAdmission`,
  `TryMarkInitialized`, `HasStartBeenAttempted`, and `ActiveSessions`. `Init` is already public on
  the subject.
- `StopOnceNoAckServerHandler` wraps every handler returned by the factory. The carrier, the session
  endpoint, and the admission fallback share the gate, so the wrapped handler observes at most one
  `OnStopped`.
- `RecordingRawReliableNoAckServerHandlerFactory` records the call count and the last remote
  endpoint and does not serialize the calls itself; the transport's factory lock remains under test.
