# RawReliableServerTransport Layer Coverage

- **Layer:** RawReliableServerTransport
- **Subject:** `Pontifex.Raw.Reliable.RawReliableServerTransport<TServerHandler>`
- **Directory:** `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server`
- **Inheritance parent:** `Pontifex.Raw.Reliable.RawReliableTransport`
- **Contract:** [RawReliableServerTransport.cs](../../../../../../Pontifex/Implementations/Raw/Reliable/RawReliableServerTransport.cs),
  [IRawReliableServerTransport.cs](../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableServerTransport.cs),
  [IRawReliableServerHandler.cs](../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableServerHandler.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Constructor_WithNonPositiveMaxConcurrentHandshakes_Throws` | The handshake limit must be at least 1. | 0 and -1 throw `ArgumentOutOfRangeException` naming `maxConcurrentHandshakes`. | Two cases per value pair. |
| `Constructor_WithNonPositiveMaxActiveSessions_Throws` | The session limit must be at least 1. | 0 and -1 throw `ArgumentOutOfRangeException` naming `maxActiveSessions`. | Two cases per value pair. |
| `Constructor_WithNonPositiveTickPeriod_Throws` | The tick period must be at least 1 ms. | 0 and -1 throw `ArgumentOutOfRangeException` naming `tickPeriodMs`. | Two cases per value pair. |
| `ServerTransport_ExposesReliableServerTransportContract` | The class exposes the reliable server transport interfaces. | All four interface contracts are implemented. | — |
| `Start_BeforeInitialization_FailsWithoutStartingCarrierAndInvalidates` | `Start` requires prior initialization. | The carrier and ticker are not started and the transport is invalidated. | The start callback is not invoked on this path. |
| `TryMarkInitialized_FirstCallSucceedsSecondFails` | Initialization is one-shot. | The second mark fails and the transport can still start. | — |
| `Start_AfterInitialization_StartsCarrierAndTicker` | A valid start runs `StartCarrier`, then creates and starts the ticker. | Hook order, driver start, and started state. | Order is asserted through the shared event log. |
| `Start_WhenCarrierReturnsFalse_FailsWithoutStartingTicker` | A rejected carrier start fails the transport. | No ticker is created or started and no carrier stop is issued. | — |
| `Start_WhenCarrierThrows_FailsWithoutStartingTicker` | A throwing carrier start fails the transport. | No ticker is created and the transport is invalid. | — |
| `Start_SecondTime_FailsWithoutCallingCarrierAgain` | `Start` is one-shot. | The second call returns false without repeating carrier or ticker work. | — |
| `Start_AfterStopBeforeFirstStart_StillStarts` | A stop before the first start does not consume the one-shot start. | `StartCarrier` is not called during the stop and a later start succeeds. | — |
| `Start_WhenCarrierStopsTransportSynchronously_ReturnsFalseWithoutStartingTicker` | A synchronous stop during `StartCarrier` beats the start path. | No ticker is created and the stop callback receives the supplied reason. | — |
| `Start_AfterFailedStart_CannotStartAgain` | A failed start is terminal for start attempts. | The carrier is not invoked again and the transport stays invalid. | — |
| `TryBeginHandshake_BeforeStart_ReturnsFalse` | The handshake gate requires a started transport. | Both constructed and initialized-but-not-started states reject admission. | — |
| `TryBeginHandshake_WhenStarted_AllowsUpToLimitThenRejects` | The handshake gate admits exactly `maxConcurrentHandshakes` slots. | The limit is enforced. | — |
| `EndHandshake_ReleasesReservedSlot` | Ending a handshake releases one slot. | A slot can be reused after release. | — |
| `TryBeginHandshake_AfterStop_ReturnsFalse` | A stopping transport rejects handshakes. | Admission is closed and `IsStopping` is observable. | — |
| `TryBeginHandshake_AfterFail_ReturnsFalse` | A failed transport rejects handshakes. | Admission is closed. | — |
| `HandshakeGate_IsIndependentFromSessionGate` | The handshake and session gates are separate counters. | Saturating one gate does not consume the other, and each release affects only its own gate. | — |
| `ConcurrentAdmissionAttempts_NeverExceedConfiguredLimits` | Admission counters are safe under concurrent attempts. | Exactly 8 of 200 attempts win each gate when the limits are 8. | Uses `Parallel.For`; the assertion is an invariant (no over-admission), not a scheduling order. |
| `TryReserveSession_BeforeStart_ReturnsFalse` | The session gate requires a started transport. | Both constructed and initialized-but-not-started states reject reservation. | — |
| `TryReserveSession_WhenStarted_AllowsUpToLimitAndTracksActiveSessions` | The session gate admits up to `maxActiveSessions` and tracks the count. | Admission stops at the limit and `ActiveSessions` reflects reservations. | — |
| `ReleaseSession_ReleasesReservedSlot` | Releasing a session frees one slot. | The active count drops and a new reservation succeeds. | — |
| `TryReserveSession_AfterStop_ReturnsFalse` | A stopping transport rejects session reservations. | Admission is closed. | — |
| `TryReserveSession_AfterFail_ReturnsFalse` | A failed transport rejects session reservations. | Admission is closed. | — |
| `TryStartAndRegisterSession_WithNull_Throws` | Session admission requires a non-null session. | `ArgumentNullException` is thrown without reserving a slot. | — |
| `TryStartAndRegisterSession_WhenStarted_StartsAndRegistersSession` | A valid admission starts the session and keeps its reserved slot. | The session is started on its own queue, registered, and not stopped. | `BeginConnect` runs only when the session queue drains. |
| `TryStartAndRegisterSession_WhenTransportStopping_StartsThenStopsAndReleasesSlot` | Admission after the transport begins stopping fails and cleans up. | The session starts then stops with a `TextFail` sourced from the transport name; the slot is released and no session-stopped hook runs. | — |
| `TryStartAndRegisterSession_WhenSessionStartRejected_ReleasesSlotAfterTerminalNotification` | A rejected session start releases its slot through the terminal notification. | The slot is still reserved when admission returns false and is released after the queue drains. | The endpoint owns the release timing. |
| `TryStartAndRegisterSession_WhenSessionStartThrows_ReleasesSlotAfterTerminalNotification` | A throwing session start follows the same terminal release path. | The slot is released after drain and no session-stopped hook runs. | — |
| `TryStartAndRegisterSession_WhenSessionStopsSynchronouslyDuringStart_ReleasesSlotOnceWithoutNotifying` | A session that stops during its own start releases exactly once. | `ActiveSessions` is zero, the session handler sees the session reason, and `OnSessionStopped` is not called. | — |
| `TryStartAndRegisterSession_WhenTransportStopsDuringSessionStart_ReleasesSlotOnceWithoutNotifying` | A transport stop during session start releases exactly once. | The slot is released, the session handler stops with the transport reason, and the carrier is stopped once. | — |
| `SessionStop_AfterRegistration_ReleasesSlotAndNotifiesOnce` | A registered session's stop releases its slot and notifies once. | The reason reaches `OnSessionStopped`, the slot count is already decremented inside the hook, and the transport stays started. | — |
| `SessionStop_Repeated_DoesNotNotifyTwice` | Repeated session stops are idempotent at the transport. | Only one `OnSessionStopped` and one handler stop are observed. | — |
| `SessionStop_DoesNotAffectOtherSessions` | Sessions are independent. | Stopping one leaves the other registered, running, and ticked. | — |
| `OnSessionStopped_WhenItThrows_DoesNotBreakStopOrDoubleRelease` | A throwing session-stopped hook is isolated. | The hook runs once, the slot is released once, and the transport stays valid. | — |
| `Stop_WhenStarted_CallsStopCarrierWithSameReasonAndStopsTicker` | A started stop calls `StopCarrier` with the same reason and stops the ticker. | Reason identity, `IsStopping` during the carrier call, zero active sessions, and driver stop. | The transport remains valid after a normal stop. |
| `Stop_WhenStarted_StopsEveryRegisteredSessionWithTransportReasonAndNotGentle` | A transport stop stops every registered session with the transport reason. | Every session handler receives the same reason with `tryBeGentle == false`; all slots are released. | — |
| `Stop_WhenSessionAlreadyStopped_DoesNotStopItAgain` | Already stopped sessions are not stopped again. | The original session reason and single notification are preserved. | — |
| `Stop_BeforeStart_DoesNotCallStopCarrierAndAllowsStart` | A stop before start is a no-op for the carrier. | `StopCarrier` is not called and a later start succeeds. | — |
| `Stop_WithNullReason_PassesUnknownSourcedFromTransportNameToCarrier` | A null stop reason becomes `Unknown` sourced from the transport name. | The exact reason reaches `StopCarrier`. | — |
| `Fail_WhenStarted_CallsStopCarrierAndStopsRegisteredSessions` | A failure while running performs the stop delta and invalidates the transport. | The carrier, driver, sessions, and start callback all observe the failure reason. | — |
| `Ticker_WhenStarted_TicksEveryRegisteredSession` | The ticker calls `PeriodicTick` on every registered session. | Each session observes one clock read per tick. | The driver is hand-driven; no timer or thread is involved. |
| `Ticker_WhenNoSessions_DoesNothing` | A tick with no sessions is harmless. | No notification occurs and the driver stays started. | — |
| `Ticker_AfterSessionStopped_DoesNotTickIt` | Stopped sessions are skipped. | Only the live session is ticked. | — |
| `Ticker_WhenOneSessionThrows_ContinuesWithOtherSessions` | A session tick exception is isolated. | The remaining sessions are still ticked. | — |
| `Ticker_AfterTransportStopped_DoesNotTickSessions` | Ticks after transport stop are ignored. | The session clock is not read and the driver was stopped once. | — |
| `Ticker_IsNotCreatedWhenStartFails` | A failed start creates no ticker. | The driver factory and driver start are never called. | — |
| `Ticker_SessionAddedAfterStartIsTicked` | A session added after start is picked up by the ticker. | The new session is ticked on the next tick. | — |

## Planned

| Missing layer or test | Owner | Reason |
|---|---|---|
| — | — | No known missing tests for this layer. |

## Deferred by design

| Gap | Reason |
|---|---|
| Conformance controls (`GetControls`, gates, failure injection). | The conformance subsystem is out of scope and planned for a rewrite. |
| Real carrier start/stop, sockets, and network timing. | The layer is exercised through `StartCarrier`/`StopCarrier` seams. |
| Real periodic scheduling and thread affinity. | The ticker is driven explicitly through `ManualPeriodicLogicDriver`; no timer or thread is involved. |

## Notes

- `TestRawReliableServerTransport` exposes the protected admission and registry surface and
  supplies a hand-driven `ManualPeriodicLogicDriver`.
- `TestRawReliableServerSession` exposes a controllable `Now` clock and a `ScriptedSerialActionQueue`
  so session start/stop and ticker coverage are deterministic.
- `ConcurrentAdmissionAttempts_NeverExceedConfiguredLimits` is the only test using real
  parallelism. It asserts a bounded invariant (`handle/session counts never exceed the
  configured limits`) and completes synchronously; it does not assert scheduling order.
- `OnSessionStopped` is invoked only for sessions that were actually registered. Sessions that
  stop during admission are reported through the `false` return instead.
