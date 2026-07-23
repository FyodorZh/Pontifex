# CheckPointGate — Technical Specification

| Field | Value |
|---|---|
| **Component** | `Pontifex.Utils.CheckPointGate` |
| **Version** | 1.0.0-draft |
| **Status** | Draft |
| **Last updated** | 2026-07-23 |

---

## 1. Purpose

CheckPointGate is a synchronisation primitive that bridges producer-side
back-pressure and consumer-side signalling.  It allows a configurable number
of "hits" (signals) to pass through freely, then **blocks** subsequent hits
until the gate is explicitly released by the controller.  At the same moment
the first blocked hit arrives, a waiter (the code that called `Arm`) is
notified — enabling patterns such as "wait for N items, then stop; resume
when drained."

---

## 2. Scope

This specification covers the public API surface of the `ICheckPoint` and
`ICheckPointCtl` interfaces and the `CheckPointWaitResult` enumeration.
It defines exact behavioural contracts, threading guarantees, exception
behaviour, and edge-case semantics — but does **not** dictate the internal
implementation.

---

## 3. Definitions

| Term | Definition |
|---|---|
| **Gate** | The logical synchronisation state managed by an `ICheckPointCtl` instance. |
| **Armed** | State in which hits are counted and eventually block. |
| **Hit** | A call to `Hit()` or `HitAsync()`. |
| **Blocked Hit** | A `Hit()` call whose thread is suspended, or a `HitAsync()` call whose returned `Task` has not yet completed, because `HitCount == 0` and `IsArmed == true`. |
| **Release** | The act of resetting the gate via `Reset()`, `Dispose()`, or a new `Arm()` call. Unblocks all blocked hits. |
| **Pending Arm** | The `Task<CheckPointWaitResult>` returned by an `Arm()` call that has **not** yet completed. |
| **Controller** | Code that holds an `ICheckPointCtl` reference and calls `Arm`, `Reset`, `Dispose`. |
| **Producer** | Code that calls `Hit()` / `HitAsync()`. |

---

## 4. API Surface

### 4.1. `CheckPointWaitResult` enum

```
namespace Pontifex.Utils.CheckPointGate
{
    public enum CheckPointWaitResult
    {
        Reached,
        Released,
    }
}
```

| Member | Meaning |
|---|---|
| `Reached` | The required hit count was exhausted — a hit is now blocked. |
| `Released` | The gate was released before the required hit count was reached. |

### 4.2. `ICheckPoint` interface

```
namespace Pontifex.Utils.CheckPointGate
{
    public interface ICheckPoint
    {
        void Hit();
        Task HitAsync();
    }
}
```

### 4.3. `ICheckPointCtl` interface

```
namespace Pontifex.Utils.CheckPointGate
{
    public interface ICheckPointCtl : ICheckPoint, IDisposable
    {
        bool IsArmed { get; }
        int HitCount { get; }
        Task<CheckPointWaitResult> Arm(int requiredHits = 1);
        void Reset();
    }
}
```

---

## 5. Behavioural Specification

### 5.1. Instance lifecycle

```
  ┌──────────┐  Arm(N)   ┌──────────┐  HitCount==0  ┌────────────┐
  │ Unarmed  │──────────▶│  Armed   │──(next Hit)──▶│  Blocking  │
  │          │           │          │               │            │
  │ H=0, A=F │◀──────────│ H=N-1,A=T│◀──────────────│ H=0, A=T   │
  └──────────┘  Reset()   └──────────┘   Reset()     └────────────┘
       │         Arm(M)                              │
       └─────────────────────────────────────────────┘
          (Arm() always resets first)

  H = HitCount, A = IsArmed
  Dispose() = same transition as Reset() but disables further use.
```

### 5.2. State variables

| Variable | Type | Initial | Description |
|---|---|---|---|
| `IsArmed` | `bool` | `false` | Whether the gate is counting hits. Set `true` by `Arm()`, set `false` by `Reset()` / `Dispose()`. |
| `HitCount` | `int` | `0` | Remaining free hits before the next call blocks. Never negative. |
| `_disposed` | `bool` | `false` | (internal) Whether `Dispose()` has been called. |

### 5.3. `Arm(requiredHits)`

**Signature:** `Task<CheckPointWaitResult> Arm(int requiredHits = 1)`

**Preconditions:**
- `requiredHits > 0`.
- `ObjectDisposedException` if the instance is disposed.

**Effect:**
1. If the gate is already armed or has blocked hits: performs an implicit
   `Reset()` first (see §5.5).
2. Sets `IsArmed = true`.
3. Sets `HitCount = requiredHits - 1`.
4. Returns a `Task<CheckPointWaitResult>` that will complete:
   - With `Reached` when the **next hit after `HitCount` reaches 0**
     blocks (i.e. the `requiredHits`-th hit call arrives and blocks).
   - With `Released` when a subsequent `Reset()`, `Dispose()`, or
     new `Arm()` call occurs before the above condition.

**Postconditions:**
- `IsArmed == true`
- `HitCount == requiredHits - 1`
- No blocked hits remain (any that existed were unblocked by the implicit
  `Reset()`).

**Exceptions:**

| Condition | Exception |
|---|---|
| `requiredHits <= 0` | `ArgumentOutOfRangeException` |
| Instance disposed | `ObjectDisposedException` |

### 5.4. `Hit()`

**Signature:** `void Hit()`

**Effect:**
1. If `IsArmed == false` → return immediately (no-op).
2. If `HitCount > 0` → decrement `HitCount` by 1, return immediately.
3. If `HitCount == 0` (i.e. already at the threshold) → **block the
   calling thread** until `Reset()`, `Dispose()`, or a new `Arm()`
   unblocks it.

**Postconditions:**
- If the call did not block: `HitCount` is decremented by 1 or unchanged.
- If the call blocked: after unblocking, the gate reflects the state of
  the releasing operation.  After `Reset()` or `Dispose()` the gate is
  unarmed with `HitCount == 0`.  After a new `Arm()` the gate is armed
  with `HitCount == requiredHits - 1`.

**Exceptions:**

| Condition | Exception |
|---|---|
| Instance disposed | No-op (does nothing, returns immediately) |

### 5.5. `HitAsync()`

**Signature:** `Task HitAsync()`

**Effect:**
1. If `IsArmed == false` → return `Task.CompletedTask`.
2. If `HitCount > 0` → decrement `HitCount` by 1, return `Task.CompletedTask`.
3. If `HitCount == 0` → return a `Task` that completes when `Reset()`,
   `Dispose()`, or a new `Arm()` unblocks it.

**Postconditions:**
- Same as `Hit()` in terms of state changes.
- Returned `Task` never faults or cancels.

**Exceptions:**

| Condition | Exception |
|---|---|
| Instance disposed | Returns `Task.CompletedTask` (no-op) |

### 5.6. `Reset()`

**Signature:** `void Reset()`

**Effect:**
1. Sets `IsArmed = false`.
2. Sets `HitCount = 0`.
3. Unblocks any threads blocked in `Hit()`.
4. Completes any pending tasks from `HitAsync()`.
5. If there is a pending `Arm()` task that has not yet completed:
   completes it with `Released`.

**Postconditions:**
- `IsArmed == false`
- `HitCount == 0`
- No blocked hits remain.

**Exceptions:** None (even if disposed — no-op).

### 5.7. `Dispose()`

**Signature:** `void Dispose()` (inherited from `IDisposable`)

**Effect:**
1. Performs all actions of `Reset()`.
2. Marks the instance as disposed.
3. Subsequent calls:
   - `Hit()`, `HitAsync()` → no-op.
   - `Arm()` → throws `ObjectDisposedException`.
   - `Reset()` → no-op.
   - `Dispose()` → no-op (per standard `IDisposable` pattern).

---

## 6. Thread Safety

All public members are **thread-safe** and may be invoked concurrently from
multiple threads.

- `Arm()` operations linearise with respect to each other and with
  `Reset()` / `Dispose()`.  If two threads call `Arm()` concurrently, the
  outcome is equivalent to some sequential interleaving; the caller must
  not assume a particular ordering.
- `Hit()` and `HitAsync()` are lock-free (or use minimal contention) and
  never deadlock.
- The `IsArmed` and `HitCount` getters return values that were valid at
  some point during the call; they are not guaranteed to be consistent
  with each other under concurrent mutation.

---

## 7. Exception Matrix

| Method | `ObjectDisposedException` | `ArgumentOutOfRangeException` | Other |
|---|---|---|---|
| `Hit()` | No-op (swallowed) | — | — |
| `HitAsync()` | No-op (returns CompletedTask) | — | — |
| `Arm()` | Thrown | Thrown if `≤ 0` | — |
| `Reset()` | No-op | — | — |
| `Dispose()` | No-op | — | — |
| `IsArmed` | — | — | — |
| `HitCount` | — | — | — |

---

## 8. Edge Cases

| Scenario | Behaviour |
|---|---|
| `Arm(1)` | `HitCount = 0`. Next `Hit()` blocks immediately, `Arm()` Task completes with `Reached`. |
| `Arm(Int32.MaxValue)` | Allowed. `HitCount = Int32.MaxValue - 1`. |
| `Hit()` concurrent with `Reset()` | Linearised. Either the hit decrements and returns, or it blocks and is immediately unblocked by the reset. |
| `Dispose()` while `Hit()` is blocked | The blocked thread unblocks. |
| `Hit()` after `Dispose()` | No-op (returns immediately). |
| `HitAsync()` after `Dispose()` | Returns `Task.CompletedTask`. |
| Multiple blocked `Hit()` calls | All unblock on the next `Reset()` / `Dispose()` / `Arm()`. |
| `Arm()` while hits are already blocked | Implicit `Reset()` unblocks them, old pending `Arm()` task completes `Released`, new arm starts. |

---

## 9. Usage Pattern (Illustrative)

```csharp
// Producer-consumer: stop consuming after N items, resume on drain.
ICheckPoint gate = ...;
ICheckPointCtl ctl = (ICheckPointCtl)gate;

// Consumer (controller)
Task<CheckPointWaitResult> wait = ctl.Arm(5);

// Producer (5 parallel workers)
for (int i = 0; i < 5; i++)
{
    item = TakeWork();
    gate.Hit();           // first 4 return, 5th blocks the producer
}

// Consumer is notified when the 5th producer blocks
CheckPointWaitResult r = await wait;   // Reached

ProcessAllItems();

ctl.Reset();              // unblock the 5th producer
```

---

## 10. Version History

| Version | Date | Author | Changes |
|---|---|---|---|
| 1.0.0-draft | 2026-07-23 | — | Initial specification |
