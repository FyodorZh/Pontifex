# RawReliableAckServerTransport Layer Coverage

- **Layer:** RawReliableAckServerTransport
- **Subject:** `Pontifex.Raw.Reliable.Ack.RawReliableAckServerTransport`
- **Directory:** `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/Ack`
- **Inheritance parent:** `Pontifex.Raw.Reliable.RawReliableServerTransport<TServerHandler>`
- **Contract:** [RawReliableAckServerTransport.cs](../../../../../../../Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckServerTransport.cs),
  [IRawReliableAckServer.cs](../../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Server/IRawReliableAckServer.cs),
  [IRawReliableAckServerAcknowledger.cs](../../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Server/IRawReliableAckServerAcknowledger.cs),
  [IRawReliableAckServerHandler.cs](../../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Server/IRawReliableAckServerHandler.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `AckServerTransport_ExposesReliableAckServerContract` | The Ack server transport exposes the Ack server and reliable server contracts. | All five interface contracts are implemented and `Type` is `TransportType.RawReliableAck`. | — |
| `Init_WithNull_ThrowsWithoutChangingLifecycle` | A null acknowledger is rejected without changing the lifecycle. | `ArgumentNullException` is thrown, the transport stays valid and unstarted, and a later valid `Init` succeeds. | — |
| `Init_FirstCallSucceedsSecondFailsAndKeepsFirstAcknowledger` | `Init` is one-shot and keeps the first acknowledger. | The second initializer is ignored and the first one serves `TryAcknowledge`. | — |
| `Init_AfterStartAttempt_ReturnsFalse` | `Init` is illegal after a start attempt. | The transport rejects initialization and does not restart the carrier. | — |
| `Init_AfterFailedStartBeforeInit_ReturnsFalse` | `Init` is illegal after a failed start. | The transport is invalid and rejects initialization. | — |
| `Init_AfterStopBeforeStart_ReturnsTrue` | A stop before the first start does not consume the init opportunity. | The transport remains valid and accepts initialization. | — |
| `Init_AfterNormalStop_ReturnsFalse` | `Init` is illegal after a normal stop. | The transport stays valid but refuses initialization. | — |
| `Init_AfterFail_ReturnsFalse` | `Init` is illegal after a failure. | The invalid transport refuses initialization. | — |
| `Init_WhenAlreadyMarkedInitialized_DoesNotInstallAcknowledger` | A subclass that already marked the transport initialized blocks `Init`. | No acknowledger is installed and `TryAcknowledge` returns null. | Exercises the protected initialization marker exposed by the double. |
| `Init_ConcurrentCalls_ExactlyOneSucceeds` | Concurrent `Init` calls have exactly one winner. | Exactly one of 20 initializers is installed and serves the single `TryAcknowledge` call. | Uses `Parallel.For`; the assertion is an invariant, not a scheduling order. |
| `TryAcknowledge_WithoutInit_ReturnsNullAndLeavesDataOwnedByCaller` | `TryAcknowledge` before `Init` returns null. | The caller keeps ownership and the reference count stays at one. | — |
| `TryAcknowledge_WhenStarted_PassesSameDataWithExtraReferenceAndReturnsHandler` | The acknowledger receives the remote endpoint and the same buffer with one added reference. | The tuple carries a stop-once gate wrapping the handler and the response the acknowledger built, and the gate guarantees one `OnStopped`. The remote endpoint is the same instance, and the added reference is released exactly once. | Reference counts are asserted around the acknowledger call. The test takes ownership of the tuple's response and releases it because no endpoint receives it. |
| `TryAcknowledge_WhenAcknowledgerRejects_ReturnsNullAndKeepsCallerReference` | A rejection returns null without consuming the caller's reference. | The acknowledger's added reference is released and the caller's reference remains alive. | — |
| `TryAcknowledge_WhenAcknowledgerThrows_ReturnsNullAndDoesNotStopTransport` | An acknowledger exception, including a response-construction failure, is isolated. | `TryAcknowledge` returns null, the buffer survives, and the transport remains valid and started. | — |
| `TryAcknowledge_NullResponseWithHandler_ReturnsNullAndNotifiesOnStoppedOnce` | A handler without a response is unusable. | The attempt is rejected and the handler receives exactly one `OnStopped` with a `TextFail` reason. | The handler is notified directly because no session exists to own the terminal notification. |
| `TryAcknowledge_ResponseWithNullHandler_ReleasesResponseAndReturnsNull` | A response without a handler is unusable. | The attempt is rejected and the returned response is released by the transport. | The response is asserted dead without handing it to an endpoint. |
| `TryAcknowledge_BeforeStart_ReturnsNullAndDoesNotCallAcknowledger` | `TryAcknowledge` requires a started transport. | The acknowledger is not called and the caller keeps ownership. | — |
| `TryAcknowledge_AfterStop_ReturnsNullAndDoesNotCallAcknowledger` | `TryAcknowledge` after a normal stop is rejected without calling the acknowledger. | The buffer survives and ownership stays with the caller. | — |
| `TryAcknowledge_AfterFail_ReturnsNullAndDoesNotCallAcknowledger` | `TryAcknowledge` after a failure is rejected without calling the acknowledger. | The buffer survives and ownership stays with the caller. | — |
| `TryAcknowledge_ReturnsStopOnceWrapper` | A successful acknowledgement returns a stop-once gate plus the response. | The tuple's `handler` is a `StopOnceAckServerHandler` whose `Inner` is the acknowledger's handler, and its `ackResponse` is the response the acknowledger built. | The wrapper changes handler identity; reference comparisons use `Inner`. The response is released because no endpoint receives it. |
| `StopOnceWrapper_ForwardsLifecycleCallbacks` | The gate forwards the non-terminal callbacks. | `OnConnected`, `OnReceived`, and `OnDisconnected` are observed by the wrapped handler with the original arguments. | The acknowledgement server gate no longer has a response-building callback. |
| `StopOnceWrapper_ForwardsOnStoppedOnce` | The gate guarantees one terminal notification. | Two `OnStopped` calls through the interface deliver the first reason once and ignore the second. | — |
| `AbandonAdmission_WithHandler_NotifiesOnStoppedOnce` | An abandoned admission notifies the created handler. | The gated handler receives one `OnStopped`; a later direct call is ignored and the session slot is released. | Uses the exact gate returned by `TryAcknowledge` for both calls. |
| `CompleteAdmission_WhenTransportStopping_DoesNotPreemptStartedSession` | A started session owns its terminal notification. | The admission fallback stays silent immediately after completion; draining the session delivers one `OnStopped` and releases the slot. | The base server-session double does not raise `OnConnected`/`OnDisconnected`, so those are not asserted. |
| `CompleteAdmission_WhenSessionCannotStart_NotifiesOnStoppedOnce` | A session that cannot start has no terminal notification of its own. | The admission fallback delivers one `OnStopped` and the drained session adds no second one. | Covers the rejected-start path only; a second rejected-start variant is deliberately not added. |

## Planned

| Missing layer or test | Owner | Reason |
|---|---|---|
| — | — | No known missing tests for this layer. |

## Deferred by design

| Gap | Reason |
|---|---|
| Inherited session admission, registry, transport stop, and ticker behaviour. | Covered once by `RawReliableServerTransportTests`; not repeated here. |
| Conformance controls. | The conformance subsystem is out of scope and planned for a rewrite. |
| Real carrier handshakes and wire-level Ack encoding. | Carrier and wire behaviour belong to the carrier/conformance suites. |
| Logging verification for acknowledger exceptions. | Requires a custom observable `ILogSink`; the transport-level policy is already covered. |

## Notes

- `TestRawReliableAckServerTransport` supplies the carrier hooks and a hand-driven ticker and
  exposes `TryAcknowledge`, `TryBeginAdmission`, `CompleteAdmission`, `AbandonAdmission`,
  `TryMarkInitialized`, `HasStartBeenAttempted`, and `ActiveSessions`.
- `StopOnceAckServerHandler` wraps every handler returned by the acknowledger. The carrier, the
  session endpoint, and the admission fallback share the gate, so the wrapped handler observes at
  most one `OnStopped`.
- `RecordingRawReliableAckServerAcknowledger` records the remote endpoint, the buffer identity, the
  reference counts before and after its configured behavior, and the response it returned.
- The transport owns the returned ACK response only until it returns the tuple: the caller must release
  the response unless it hands it to a session endpoint. The tests that never build an endpoint release
  the response themselves.
- The tests allocate `UnionDataList` instances, so the fixture runs `GC.Collect()` in `TearDown`
  to keep pooled-buffer diagnostics deterministic.
