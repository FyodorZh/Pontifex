# Endpoint Test Suite

Black-box unit tests for the `RawEndpoint` class hierarchy in `Pontifex`.

The suite lives under `Tests/Pontifex.Tests/Endpoints/` and tests the endpoint classes
directly (no transports, sockets, or threads), one test-set per endpoint class.

```
Endpoints/
├── README.md                  (this file)
├── Harness/                   shared test doubles
│   ├── ManualSerialActionQueue.cs
│   ├── RecordingRawHandler.cs
│   ├── TestBuffers.cs
│   └── TestRawEndpoint.cs     test double for RawEndpoint
└── RawEndpointTests.cs        test-set for RawEndpoint
```

## Goals

- Exercise each endpoint class through its **public + protected contract**.
- Assert only **observable effects**, never private state.
- Be deterministic and fast: no real time, no real transports, no sleeping.

## What is covered

| Endpoint class | Test-set | Status | Focus |
|---|---|---|---|
| `RawEndpoint` | `RawEndpointTests` | covered (32 tests) | lifecycle, send admission, inbound dispatch, hard-fail |
| `RawReliableEndpoint` | — | planned | connect/disconnect stage machine, framing |
| `RawReliableClientEndpoint` | — | planned | ping/pong keep-alive |
| `RawReliableServerEndpoint` | — | planned | idle watchdog (`PeriodicTick`) |
| `RawReliableAckClientEndpoint` | — | planned | ack handshake (client side) |
| `RawReliableAckServerEndpoint` | — | planned | ack handshake (server side) |

Test-sets are **independent per class**: a test-set covers the behavior that class itself
introduces (its own delta + invariants), not the inherited layers again.

### `RawEndpoint` contract matrix

- **Constructor**: `messageMaxByteSize < 1` and `bufferCapacityBytes < 1` throw
  `ArgumentException`; `RemoteEndPoint`/`MessageMaxByteSize` are captured; `IsRunning` is
  `false`.
- **Start**: happy path reaches `Started`/`IsRunning`; a second `Start` fails hard
  (`ExceptionFail` → `OnStopped`); `Start` after `Stop` fails hard; a `Stop` that arrives
  while `Starting` is deferred until `FinishStart` completes.
- **FinishStart**: wrong stage → `onFail` + hard-fail; correct stage → `Started`.
- **Send**: `null` → `InvalidMessage`; size > max → `MessageTooBig`; not started/stopping →
  `NotConnected`; over capacity → `BufferOverflow`; valid → `Ok`, `DoSendRaw` receives the
  buffer and the buffer is released afterwards; admission order is preserved; budget is
  freed after a drain.
- **Inbound**: not running → released with no callback; running → `OnReceived`; no delivery
  after `OnStopped`.
- **CanSendNow**: `NotConnected` unless `Started` and no stop requested.
- **Stop**: before start → `Stopped` (no handler callback on the base path); idempotent;
  `reason`/`tryBeGentle` forwarded to `BeginStop`; running → `OnStopped(reason)`; cannot be
  restarted.
- **FailHard**: started path → `OnStopped` + `Stopped`; reached via the queue's `onError`
  when a queued action throws.
- **GetControls**: the base implementation adds nothing.

## How it works

### Black-box boundary

The public interface of the endpoint classes includes `protected` members, so the boundary
of the test-set is **public + protected**. Tests reach protected members through a
**test double** that derives from the class under test; they never inspect private fields
and never use reflection. Only these are asserted:

- method return values,
- handler callbacks (`OnReceived`/`OnConnected`/`OnDisconnected`/`OnStopped`) and reasons,
- buffers captured by `DoSendRaw`,
- read-only observables (`IsRunning`, `IsConnected`, `IsLowLevelConnected`, `RemoteEndPoint`,
  `MessageMaxByteSize`),
- items added by `GetControls`.

### Determinism: the injectable serial action queue

Every transition and callback is serialized through `RawEndpoint`'s action queue. That queue
is injectable through the production seam:

- `ISerialActionQueue` (`Pontifex/Utils/ISerialActionQueue.cs`) declares the two members the
  endpoints use: `Schedule(Action)` and `Schedule<T>(T, Action<T>, Action<T>?)`.
- `RawEndpoint.SerialQueueConstructor(Action<Exception> onError)` is a `protected virtual`
  factory that defaults to an unbounded `SerialActionQueue`. Tests override it to return a
  `ManualSerialActionQueue`.

`ManualSerialActionQueue` models `SerialCommandQueue.OnTick` faithfully:

- actions run strictly one at a time;
- an action that schedules another action has it run on a **later iteration**, not inline;
- an exception from an action is routed to `onError` and the drain continues;
- `DrainOne()` runs one accepted action, `DrainAll()` runs until the queue is empty.

The typical test shape is therefore:

```csharp
using var handler = new RecordingRawHandler(MemoryRental.Shared);
using var ep = new TestRawEndpoint(handler, messageMaxByteSize: 64);

ep.StartAndComplete();          // Start -> BeginStart -> FinishStart

Assert.That(ep.IsRunning, Is.True);
Assert.That(ep.BeginStartCalls, Is.EqualTo(1));
```

For race windows, drive the queue step by step instead of draining everything at once:

```csharp
ep.Start();
ep.DrainAll();                       // BeginStart recorded; stage is Starting (not completed)
ep.Stop(reason, tryBeGentle: false); // latch a stop while Starting
ep.DrainAll();                       // stop is deferred: it waits for FinishStart
ep.CompleteStart();                  // Starting -> Started -> Stopping -> BeginStop
ep.CompleteStop();                   // Stopping -> Stopped + handler.OnStopped(reason)

Assert.That(handler.StoppedReason, Is.Not.Null);
```

### Test doubles

- **`ManualSerialActionQueue : ISerialActionQueue`** — the controllable queue described above.
  Endpoints never call `Release` or `ScheduleFast`, so neither is modelled and `onFailToRun` is
  never invoked.
- **`RecordingRawHandler : IRawHandler, IDisposable`** — clones each buffer delivered through
  `OnReceived` for inspection, releases the original, and releases the clones on `Dispose`;
  records `StoppedReason`/`StoppedCount` and an `Events` sequence (`received`/`stopped`) for
  callback-ordering assertions.
- **`TestBuffers`** — builds `UnionDataList` buffers of controlled size
  (`Acquire`/`WithByte`/`WithPayload`).
- **`TestRawEndpoint : RawEndpoint, IDisposable`** — the seam for the class under test. It
  installs the manual queue via `SerialQueueConstructor`, exposes the protected surface
  (`ProcessInboundMessage`, `ScheduleToSend`, `CanSendNow`, `FailHard`, `FinishStart`,
  `FinishStop`, `TransportName`/`Log`/`Memory`), records `BeginStart`/`BeginStop` and captures
  `DoSendRaw` buffers as clones, and provides the completion helpers (`CompleteStart`,
  `CompleteStop`, `StartAndComplete`, `StopAndComplete`).

Buffers are acquired exactly as in production:
`MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>()` (via `TestBuffers`) and released
with `Release()`/`using var _ = list.AsDisposable()`. The harness clones-out captured buffers
and releases the originals so tests can inspect content without leaking pooled memory.

### Why this iteration has no fake transport

`RawEndpoint`'s constructor takes the transport name, logger, and memory rental explicitly
rather than an `IRawTransport`, so the `RawEndpoint` test-set needs no transport at all. The
`RawReliable*` constructors still take a transport owner; a `FakeRawTransport` will be added
when those test-sets are introduced.

## Harness invariants (important)

1. **No field initializer for the injected queue.** `SerialQueueConstructor` is invoked from
   the base constructor, before the test double's field initializers run. Store the created
   queue in a plain field (`private ManualSerialActionQueue? _queue;`) and assign it inside the
   override; a field initializer would overwrite it afterwards.
2. **Completion is explicit (Model B).** The test double's `BeginStart` and `BeginStop` only
   record the call; tests complete them with `CompleteStart()`/`CompleteStop()` (or
   `StartAndComplete()`/`StopAndComplete()`), which run `FinishStart`/`FinishStop` and drain.
   This models asynchronous transports and makes the deferred paths explicit.

## How to extend: add a test-set for a new endpoint class

1. Add a test double in `Harness/`, e.g. `TestRawReliableEndpoint.cs`, deriving from the class
   under test:
   - implement the class's `abstract` hooks;
   - override `SerialQueueConstructor` to install a `ManualSerialActionQueue`;
   - expose the `protected` members you need for the test-set;
   - record hook invocations (`BeginConnect`, `BeginDisconnect`, `OnConnected`, …) and capture
     outbound buffers as clones;
   - provide completion helpers for the class's `Begin*`/`Finish*` hooks (follow
     `TestRawEndpoint`).
2. Add any shared fakes the class needs in `Harness/`:
   - a `FakeRawTransport` if the class's constructor takes a transport owner (`RawReliable*`);
   - a recording handler if the class uses a new handler interface (follow
     `RecordingRawHandler`).
3. Add `<ClassName>Tests.cs` next to `RawEndpointTests.cs` and cover **that class's delta**
   plus its invariants.
4. Follow the conventions below.
5. Run the suite.

## Conventions

- NUnit 4. `using NUnit.Framework;` is global (declared in the test project).
- `namespace Pontifex.Tests.Endpoints;`, `public sealed class <ClassName>Tests`.
- One test class per file; file named after the class.
- Test methods: `Scenario_ExpectedResult` (e.g. `Send_WhenNotStarted_ReturnsNotConnected`).
- Hand-written fakes only — the repo has **no mocking library**.
- Add `[Category("Endpoints")]` to each test class.
- If a test allocates `UnionDataList`/byte arrays, add `[TearDown] public void TearDown() =>
  GC.Collect();` (see `UnionDataListTests`).

## Running

```
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj
```

Filter to this suite:

```
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Endpoints
```
