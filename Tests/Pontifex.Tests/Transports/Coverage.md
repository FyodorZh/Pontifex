# AnyTransport Layer Coverage

- **Layer:** AnyTransport (root layer)
- **Subject:** `Pontifex.AnyTransport`
- **Directory:** `Tests/Pontifex.Tests/Transports`
- **Inheritance parent:** — (root)
- **Contract:** [AnyTransport.cs](../../../Pontifex/Implementations/AnyTransport.cs),
  [ITransport.cs](../../../Pontifex/Abstractions/Transports/ITransport.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Constructor_CapturesNameMemoryAndLog` | The base constructor captures its name and memory rental and produces a non-null logger. | `Name` and `Memory` expose the supplied values and `Log` is usable. | Identity assertion for the rental; the logger is only checked for non-null. |
| `Constructor_InitiallyValidNotStartedAndNotAttempted` | A constructed transport is valid, not started, and has no start attempt. | The lifecycle starts in the constructed state. | — |
| `ToString_ReturnsRuntimeTypeName` | `ToString` reports the runtime type name. | The derived test-double name is returned. | — |
| `Start_WhenConstructed_CallsTryStartThenOnStartedAndReturnsTrue` | `Start` runs `TryStart`, then `OnStarted`, and returns true. | Hook order, validity, started state, and the start-attempt flag. | Ordering is asserted through the shared event log. |
| `Start_WithNullCallback_ThrowsAndDoesNotStart` | A null start callback is rejected before any hook runs. | `ArgumentNullException` is thrown and no lifecycle state changes. | — |
| `Start_WhenTryStartReturnsFalse_InvalidatesTransportWithoutNotifications` | A rejected `TryStart` invalidates the transport. | No `OnStarted`/`OnStopped` notification and no start callback occur. | — |
| `Start_CalledTwice_SecondReturnsFalseWithoutHooks` | `Start` is one-shot. | The second call returns false without repeating hooks. | — |
| `Start_AfterStop_ReturnsFalseWithoutHooks` | A stopped transport cannot restart. | The rejected restart leaves hook counts unchanged. | — |
| `Start_AfterStopBeforeStart_Succeeds` | A stop before the first start does not consume the one-shot start. | A later `Start` still succeeds. | — |
| `Start_AfterFailBeforeStart_ReturnsFalseWithoutHooks` | A failed transport cannot start. | No hooks run and the transport stays invalid. | — |
| `Start_AfterFailedStart_ReturnsFalseWithoutHooks` | A failed start is terminal for start attempts. | A retry returns false without repeating hooks. | — |
| `Start_WhenTryStartStopsTransport_ReturnsFalseWithoutOnStarted` | A synchronous stop during `TryStart` beats the start path. | `Start` returns false, `OnStarted` is skipped, and the stop callback fires once. | The stop reason is `Unknown` because the stop supplied none. |
| `Stop_BeforeStart_ReturnsTrueWithoutNotificationsOrStartAttempt` | `Stop` before start is a no-op. | No notification and no start attempt are recorded. | — |
| `Stop_WhenRunning_StopsNotifiesInOrderAndKeepsValid` | A running stop notifies `OnStopped` then the start callback and keeps the transport valid. | Callback order, reason identity, and the state observed inside `OnStopped`. | The reason instance is asserted by identity. |
| `Stop_WithNullReason_UsesUnknownSourcedFromName` | A null stop reason becomes `Unknown` sourced from the transport name. | The produced reason is shared by `OnStopped` and the start callback. | — |
| `Stop_CalledTwice_SecondReturnsTrueWithoutSecondNotification` | Repeated stops are idempotent. | Only one notification reaches the callback. | — |
| `Stop_WhenInvalid_ReturnsFalseWithoutNotification` | `Stop` on an invalid transport is rejected. | No notification is raised. | — |
| `Stop_WhenOnStoppedThrows_StillInvokesCallbackAndReturnsTrue` | A throwing `OnStopped` hook is isolated. | The start callback still runs and `Stop` returns true. | — |
| `Stop_WhenOnStoppedCallbackThrows_ReturnsTrue` | A throwing start callback is isolated. | `Stop` still completes and marks the transport stopped. | — |
| `Fail_WhenRunning_InvalidatesAndNotifiesInOrder` | A failure while running invalidates the transport and notifies in order. | Reason identity, callback order, and the state observed inside `OnStopped`. | — |
| `Fail_WhenNotStarted_InvalidatesWithoutNotification` | A failure before start only invalidates. | No `OnStopped`, no start attempt, and `Start`/`Stop` are rejected. | — |
| `Fail_AfterStop_InvalidatesWithoutSecondNotification` | A failure after a normal stop invalidates without notifying again. | No second callback is raised. | — |
| `Fail_CalledTwice_SecondDoesNothing` | The first failure wins. | Only the first reason is observed. | — |
| `Fail_WithText_CreatesTextFailSourcedFromName` | `Fail(string, …)` creates a `TextFail` sourced from the transport name. | Formatting reaches `TextFail.Text` and the reason is shared with the callback. | — |
| `Fail_WithCause_CreatesChainFail` | `Fail(StopReason, …)` creates a `ChainFail` carrying the cause. | Source, cause identity, and callback reason. | — |
| `FailException_CreatesExceptionFail` | `FailException` creates an `ExceptionFail` carrying the exception. | Source, exception identity, and callback reason. | — |
| `Fail_WhenOnStoppedThrows_StillInvokesCallback` | A throwing `OnStopped` hook is isolated during failure. | The start callback still runs and the transport is invalid. | — |
| `Fail_WhenOnStoppedCallbackThrows_DoesNotThrow` | A throwing start callback is isolated during failure. | `Fail` does not throw and the transport is invalid. | — |
| `HasStartBeenAttempted_FalseBeforeAnyStart` | The start-attempt flag starts false. | No attempt is recorded on construction. | — |
| `HasStartBeenAttempted_TrueAfterAcceptedStartAttempt` | A successful start records an attempt. | The flag becomes true. | — |
| `HasStartBeenAttempted_StaysFalseAfterStopBeforeStart` | A stop before start does not record an attempt. | The flag stays false. | — |
| `HasStartBeenAttempted_TrueAfterFailedStart` | A failed `Start` still records an attempt. | The flag becomes true. | — |

## Planned

| Missing layer or test | Owner | Reason |
|---|---|---|
| `RawReliableNoAckTransport` layer | This layer | Abstract descendant of `AnyTransport` with no layer yet; the nearest existing ancestor is the root layer. |
| `RawReliableAckClient` layer | This layer | Abstract descendant of `AnyTransport` with no layer yet; the nearest existing ancestor is the root layer. |
| `RawReliableAckServer` layer | This layer | Abstract descendant of `AnyTransport` with no layer yet; the nearest existing ancestor is the root layer. |

## Deferred by design

| Gap | Reason |
|---|---|
| Conformance controls (`IConformanceControl`, gates, failure injection, the conformance role of `GetControls`). | The conformance subsystem is out of scope and planned for a rewrite; the suite must not depend on it. |
| Real carriers, sockets, networks, threads, timers, and wall-clock time. | The default suite is deterministic; those facets belong to carrier/integration/conformance suites. |
| Concrete transport descendants. | Concrete carrier transports live outside `Pontifex` and are not part of this abstract hierarchy. |

## Notes

- `TestAnyTransport : AnyTransport` implements the abstract lifecycle hooks and records `TryStart`,
  `OnStarted`, and `OnStopped` calls, their order, and the state observed inside `OnStopped`.
- Every test constructs its own transport; there is no shared mutable state between tests.
- Assertions use only public observables plus the protected hooks exposed by the double.
