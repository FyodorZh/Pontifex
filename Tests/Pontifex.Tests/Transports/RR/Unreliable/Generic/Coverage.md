# RRUnreliableTransportGeneric Coverage

| Field | Value |
| --- | --- |
| Layer | `RRUnreliableTransportGeneric` |
| Subject | `Pontifex.RR.Unreliable.RRUnreliableTransport<THandler>` |
| Directory | `Tests/Pontifex.Tests/Transports/RR/Unreliable/Generic` |
| Inheritance parent | `Pontifex.RR.Unreliable.RRUnreliableTransport` |
| Dependency | — |
| Contract | [RRUnreliableTransport.cs](../../../../../../Pontifex/Implementations/RR/Unreliable/RRUnreliableTransport.cs), [RRUnreliable specification](../../../../../../Pontifex/Abstractions/Transports/RR/Unreliable/Specification.md), [shared RR specification](../../../../../../Pontifex/Abstractions/Transports/RR/Specification.md) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `Init_WithNull_Throws` | Section 7.1 initialization | A null handler throws without binding | The throw is `ArgumentNullException`. |
| `Init_IsOneTime` | Section 7.1 initialization | Only the first `Init` succeeds | Later calls return false. |
| `Init_AfterStartAttempt_ReturnsFalse` | Section 7.1 initialization | `Init` after a start attempt is refused | Even a successful start seals the binding. |
| `Init_BeforeStart_DoesNotStartCarrier` | Section 7.1 initialization | `Init` alone starts no carrier activity | Registration belongs to the concrete carrier. |
| `Start_WithoutInit_ReturnsFalseAndInvalidates` | Section 7.2 start | A start without a bound handler fails and invalidates without touching the carrier | No `onStopped` is invoked for a failed initial start. |
| `DispatchHandler_IsGatedByStartAndStop` | Section 7.2/7.3 handler gating | The handler is hidden before start, visible while running, and hidden after stop | The gate is the base's only handler exposure. |
| `Start_WhenCarrierReturnsFalse_LeavesHandlerUndispatched` | Section 7.2 failed start | A failed carrier start never enables dispatch | A false start delivers no handler callback. |
| `Stop_WhileRunning_HidesHandlerBeforeTheCarrierStops` | Section 7.3 stop ordering | The dispatch gate is already closed when the carrier is stopped | The double records `DispatchHandler` inside `StopCarrier`; the assertion runs after `Stop` returns. |
| `Init_AfterStop_ReturnsFalse` | Section 7.1/7.3 initialization | A stopped transport cannot be reinitialized | Stopping is terminal after a successful start. |

## Planned

| Missing layer or test | Owner | Reason |
| --- | --- | --- |
| — | — | No known missing test for this layer. |

## Deferred by design

| Gap | Reason |
| --- | --- |
| Inherited `RRTransport`/`RRUnreliableTransport` lifecycle tests | The ancestor layers own that coverage; this layer covers only the handler-binding delta. |
| Carrier registration during `Init` | `Init` must not start carrier activity; the concrete carrier registers in `StartCarrier`. |
| Handler callbacks during the initial-start window | The generic layer only exposes the gate; callback scheduling belongs to the client and server layers. |
