# TimerFactory Coverage

| Field | Value |
| --- | --- |
| Layer | Root |
| Subject | `ITimerFactory` and its two implementations |
| Directory | `Tests/Pontifex.Tests/TimerFactory/` |
| Inheritance parent | none |
| Dependency | `SystemTimerFactory` and `ManualTimerFactory` are public production types in the Pontifex assembly. |
| Contract | [ITimerFactory](../../../Pontifex/Utils/TimerFactory/ITimerFactory.cs), [ManualTimerFactory](../../../Pontifex/Utils/TimerFactory/ManualTimerFactory.cs) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `Schedule_WithNullCallback_Throws` | Argument guard | A null callback is rejected | No implementation may schedule a null action. |
| `Schedule_WithNegativeDelay_Throws` | Delay domain | Any negative delay other than `Timeout.InfiniteTimeSpan` is rejected | The check precedes the timer construction, so nothing is armed. |
| `Schedule_WithInfiniteDelay_ReturnsAHandleThatNeverFires` | Infinite delay | Infinite arms a handle that never fires and disposes idempotently | The system factory creates no timer for this case. |
| `Schedule_WithALongDelay_DoesNotFireSynchronouslyAndDisposeCancelsIt` | Non-blocking scheduling and cancellation | Scheduling returns without firing, and disposing cancels | A one-hour delay proves the callback is deferred without waiting. |
| `Dispose_IsIdempotent` | Cancellation | Disposing a handle twice is safe | The transports may dispose a timer more than once on racing closes. |
| `Schedule_RecordsTheDelayAndCallback` | Manual support | Scheduling records one pending handle with its delay | The manual factory never fires on its own. |
| `Schedule_WithInfiniteDelay_RecordsAPendingHandleThatNeverFiresOnItsOwn` | Manual support, infinite delay | Infinite is recorded as a pending handle with the infinite delay and never becomes due | An explicit fire remains possible; the factory never self-fires. |
| `Advance_FiresDueCallbacksInDueOrder` | Virtual clock | Advancing fires every due handle in due-time order and closes it | Ordering is by due time, not by scheduling order. |
| `Advance_DoesNotFireAFutureCallback` | Virtual clock | A handle whose due time is beyond the new time stays pending | The clock can be advanced repeatedly until the due time. |
| `Advance_FiresACallbackScheduledWithZeroDelayDuringTheAdvance` | Virtual clock | A callback that schedules a zero-delay timer is fired in the same advance | Demonstrates chained scheduling against the new time. |
| `Advance_WithInfiniteDelay_NeverFiresTheHandle` | Virtual clock, infinite delay | Infinite handles never become due, even after a large advance | The handle stays pending until suppressed or fired explicitly. |
| `Advance_MovesNowByTheRequestedAmount` | Virtual clock | `Now` moves forward by exactly the requested amount | `Now` is the timestamp harnesses pass to time-dependent code. |
| `Advance_WithNegativeBy_Throws` | Virtual clock guard | A negative advance is rejected | Time never moves backwards. |
| `PendingDelays_IsOrderedByDueTime` | Virtual clock | Pending delays are exposed in due-time order | Lets a test assert what is armed without inferring scheduling order. |
| `Clear_DoesNotResetNow` | Manual support, retention | Clearing handles leaves the virtual time untouched | Time and handle tracking are independent. |
| `Fire_RunsTheCallbackOnlyOnce` | Manual support | Firing runs the callback once and closes the handle | A second fire is a no-op. |
| `Suppress_ClosesTheHandleWithoutRunningTheCallback` | Manual support | Suppression closes a handle without a callback | Models a cancelled TTL. |
| `Dispose_ClosesTheHandleWithoutRunningTheCallback` | Manual support | Disposal closes a handle without a callback and is idempotent | Models a closed request. |
| `FireAll_RunsEveryPendingCallback` | Manual support | Every pending handle fires and the pending count reaches zero | Used to drive several TTLs without ordering assumptions. |
| `Clear_SuppressesPendingHandlesAndResetsTheCounts` | Manual support, retention | Clear suppresses pending handles and resets counts and the tracked list | Handles captured by the caller keep their own state. |
| `Clear_WhenNothingIsScheduled_IsSafe` | Manual support | Clearing an empty factory is safe | Lets a fixture reset unconditionally between scenarios. |
| `Schedule_WithNullCallback_Throws` (manual) | Manual argument guard | The manual factory rejects a null callback | Mirrors the production contract. |
| `Schedule_WithNegativeDelay_Throws` (manual) | Manual delay domain | The manual factory rejects a negative delay | Mirrors the production contract. |
| `Schedule_WithAShortDelay_FiresTheCallbackOnce` | Real delivery | A system timer eventually delivers exactly one callback | `TimerFactory.Integration`; waits on an event with a bounded timeout, never on a fixed sleep. |
| `Schedule_WhenTheCallbackThrows_KeepsOtherTimersAlive` | Exception isolation | A throwing callback is swallowed and later timers still fire | `TimerFactory.Integration`; proves the process and the factory survive a callback failure. |

## Planned

| Missing test | Owner layer |
| --- | --- |
| Concurrent scheduling and firing on the manual factory | Root — deferred until a consumer needs it; the factory is lock-protected but the transports serialize their own scheduling. |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Real-time accuracy, drift, and ordering between two real timers | The contract is eventual one-shot delivery, not scheduling precision. |
| Callback delivery thread identity | The contract states only that a production callback runs on a pool thread; asserting the specific thread would over-constrain the implementation. |
