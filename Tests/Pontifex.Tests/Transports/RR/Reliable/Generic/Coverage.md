# RRReliableTransportGeneric Coverage

| Field | Value |
| --- | --- |
| Layer | `RRReliableTransportGeneric` |
| Subject | `Pontifex.RR.Reliable.RRReliableTransport<THandler>` |
| Directory | `Tests/Pontifex.Tests/Transports/RR/Reliable/Generic` |
| Inheritance parent | `Pontifex.RR.Reliable.RRReliableTransport` |
| Dependency | — |
| Contract | [RRReliableTransport.cs](../../../../../../Pontifex/Implementations/RR/Reliable/RRReliableTransport.cs), [RRReliable specification](../../../../../../Pontifex/Abstractions/Transports/RR/Reliable/Specification.md), [shared RR specification](../../../../../../Pontifex/Abstractions/Transports/RR/Specification.md) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `Init_WithNull_Throws` | Section 7.1 initialization | A null handler throws without binding | The throw is `ArgumentNullException`. |
| `Init_IsOneTime` | Section 7.1 initialization | Only the first `Init` succeeds | Racing or later calls return false. |
| `Init_AfterTheStartAttempt_ReturnsFalse` | Section 7.1 initialization | `Init` after a start attempt is refused | Even a successful start seals the binding. |
| `Init_BeforeAStartAttempt_DoesNotStartTheCarrier` | Section 7.1 initialization | `Init` alone starts no carrier activity | Registration belongs to the concrete carrier. |
| `Start_WithoutInit_ReturnsFalseAndInvalidates` | Section 7.2 start | A server start without a handler fails and invalidates without touching the carrier | No `onStopped` is invoked for a failed initial start. |
| `DispatchHandler_IsGatedByStartAndStop` | Section 7.2/7.3 handler gating | The handler is hidden before start, visible while running, and hidden after stop | The gate is the base's only handler exposure. |
| `Start_WhenTheCarrierReturnsFalse_LeavesTheHandlerUndispatched` | Section 7.2 failed start | A failed carrier start never enables dispatch | A throwing or false start delivers no handler callback. |
| `Init_AfterStop_ReturnsFalse` | Section 7.1/7.3 initialization | A stopped transport cannot be reinitialized | Stopping is terminal after a successful start. |

## Planned

| Missing test | Owner layer |
| --- | --- |
| None. The server layer exists and owns the server behaviour. | — |

## Deferred by design

| Gap | Reason |
| --- | --- |
| Carrier registration during `Init` | `Init` must not start carrier activity; the concrete carrier registers in `StartCarrier`. |
| Handler callbacks during the initial-start window | The generic layer only exposes the gate; callback scheduling belongs to the client and server layers. |
