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
| `TryAcknowledge_WhenStarted_PassesSameDataWithExtraReferenceAndReturnsHandler` | The acknowledger receives the remote endpoint and the same buffer with one added reference. | The handler is returned unchanged, the remote endpoint is the same instance, and the added reference is released exactly once. | Reference counts are asserted around the acknowledger call. |
| `TryAcknowledge_WhenAcknowledgerRejects_ReturnsNullAndKeepsCallerReference` | A rejection returns null without consuming the caller's reference. | The acknowledger's added reference is released and the caller's reference remains alive. | — |
| `TryAcknowledge_WhenAcknowledgerThrows_ReturnsNullAndDoesNotStopTransport` | An acknowledger exception is isolated. | `TryAcknowledge` returns null, the buffer survives, and the transport remains valid and started. | — |
| `TryAcknowledge_BeforeStart_ReturnsNullAndDoesNotCallAcknowledger` | `TryAcknowledge` requires a started transport. | The acknowledger is not called and the caller keeps ownership. | — |
| `TryAcknowledge_AfterStop_ReturnsNullAndDoesNotCallAcknowledger` | `TryAcknowledge` after a normal stop is rejected without calling the acknowledger. | The buffer survives and ownership stays with the caller. | — |
| `TryAcknowledge_AfterFail_ReturnsNullAndDoesNotCallAcknowledger` | `TryAcknowledge` after a failure is rejected without calling the acknowledger. | The buffer survives and ownership stays with the caller. | — |

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
  exposes `TryAcknowledge`, `TryMarkInitialized`, and `HasStartBeenAttempted`.
- `RecordingRawReliableAckServerAcknowledger` records the remote endpoint, the buffer identity,
  and the reference counts before and after its configured behavior.
- The tests allocate `UnionDataList` instances, so the fixture runs `GC.Collect()` in `TearDown`
  to keep pooled-buffer diagnostics deterministic.
