# Endpoint Test Suite

Black-box unit tests for the `RawEndpoint` class hierarchy in `Pontifex`.

The suite lives under `Tests/Pontifex.Tests/Endpoints/` and tests the endpoint classes
directly (no transports, sockets, or threads), one test-set per endpoint class.

```
Endpoints/
├── README.md                          (this file)
├── Harness/                           test doubles shared by all test-sets
│   ├── ManualSerialActionQueue.cs
│   ├── RecordingRawHandler.cs
│   ├── TestBuffers.cs
│   └── TestRawEndpoint.cs             test double for RawEndpoint
├── RawEndpointTests.cs                test-set for RawEndpoint
└── RawReliable/                       test-sets for the raw-reliable endpoint family
    ├── Harness/
    │   ├── RecordingRawReliableHandler.cs
    │   ├── RecordingRawReliableClientHandler.cs
    │   ├── RecordingRawReliableServerHandler.cs
    │   ├── TestRawReliableEndpoint.cs
    │   ├── TestRawReliableClientEndpoint.cs
    │   └── TestRawReliableServerEndpoint.cs
    ├── RawReliableEndpointTests.cs
    ├── RawReliableClientEndpointTests.cs
    ├── RawReliableServerEndpointTests.cs
    └── RawReliableAck/                 test-sets for the ack endpoints
        ├── Harness/
        │   ├── RecordingRawReliableAckClientHandler.cs
        │   ├── RecordingRawReliableAckServerHandler.cs
        │   ├── TestRawReliableAckClientEndpoint.cs
        │   └── TestRawReliableAckServerEndpoint.cs
        ├── RawReliableAckClientEndpointTests.cs
        └── RawReliableAckServerEndpointTests.cs
```

Layout rule: each endpoint family lives in its own directory (`<Family>/`) with the family-specific
test doubles under `<Family>/Harness/`, one `<ClassName>Tests.cs` file per class. A sub-family may be
nested the same way (`<Family>/<SubFamily>/Harness/`). Test doubles shared across families stay in the
top-level `Harness/`. The folder structure does not change the namespace: every file in this suite
uses `namespace Pontifex.Tests.Endpoints;`.

## Goals

- Exercise each endpoint class through its **public + protected contract**.
- Assert only **observable effects**, never private state.
- Be deterministic and fast: no real time, no real transports, no sleeping.

## What is covered

| Endpoint class | Test-set | Status | Focus |
|---|---|---|---|
| `RawEndpoint` | `RawEndpointTests` | covered (32 tests) | lifecycle, send admission, inbound dispatch, hard-fail |
| `RawReliableEndpoint` | `RawReliableEndpointTests` | covered (25 tests) | connect/disconnect stage machine, framing |
| `RawReliableClientEndpoint` | `RawReliableClientEndpointTests` | covered (8 tests) | keep-alive loop, config, pong handling |
| `RawReliableServerEndpoint` | `RawReliableServerEndpointTests` | covered (7 tests) | idle watchdog (`PeriodicTick`), ping answering |
| `RawReliableAckClientEndpoint` | `RawReliableAckClientEndpointTests` | covered (9 tests) | ack handshake (client side) |
| `RawReliableAckServerEndpoint` | `RawReliableAckServerEndpointTests` | covered (10 tests) | ack handshake (server side) |

Test-sets are **independent per class**: a test-set covers the behavior that class itself
introduces (its own delta + invariants), not the inherited layers again. For example,
`RawReliableEndpointTests` deliberately omits inherited reject paths (e.g. `Send`/`CanSendNow` when
not connected) because those are covered once by `RawEndpointTests`; it keeps only the framing,
dispatch, and connect/disconnect deltas. (Such tests still exercise the base machinery as setup, so
some line coverage of `RawEndpoint.cs` is shared by construction.)

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

### `RawReliableEndpoint` contract matrix

- **Connect**: `Start` reaches `Connecting` and calls `BeginConnect`; `FinishConnect` →
  `Connected` + `OnConnected` (and `IsLowLevelConnected`/`IsConnected`/`IsRunning` become true);
  before completion those are false; `FinishConnect` without `Start` → `ifFailed`, no
  `OnConnected`.
- **Stop while connecting**: the connection is still reported (`OnConnected` once) and then torn
  down (`BeginDisconnect`, then `OnStopped`).
- **Send**: frames the payload with `PayloadMessage`; framing counts toward `MessageMaxByteSize`
  (a payload that fits raw can be `MessageTooBig` once framed).
- **Inbound**: payload frame → `OnReceived` (type byte stripped); disconnect frame →
  `OnStopped(GracefulRemoteIntention)`; invalid/empty/unhandled (ping/pong/ack) frame →
  `OnStopped(TextFail)` and the buffer is released; inbound while not low-level connected is
  released and stops with `TextFail` without delivery.
- **Disconnect**: `DisconnectGracefully()` frames `DisconnectMessage` and stops with
  `UserIntention` sourced from the transport name; when the frame cannot be admitted the stop is
  not gentle; `IRawReliableEndpoint.Disconnect(r)` → `BeginDisconnect(r, gentle: true)`.
  Stopping while connected reaches `Disconnecting` (not low-level connected, no `OnStopped` yet)
  before `FinishDisconnect` raises `OnStopped`.
- **Fail**: `FailHard` → `Disconnected` + `OnStopped`, and no `OnDisconnected`.
- **Guards**: `FinishDisconnect` in the wrong stage → hard-fail; default
  `IsConnected == IsLowLevelConnected`.

> Note: the base class never invokes `IRawReliableHandler.OnDisconnected`; only the ack
> subclasses do. `DisconnectCycle_DoesNotInvokeOnDisconnected` pins this invariant.

### `RawReliableClientEndpoint` contract matrix

- **Keep-alive configuration**: when `PingOptions` are supplied the constructor requires a positive
  `PingPeriod` and `DisconnectTimeout >= 2 * PingPeriod`, otherwise it throws
  `InvalidOperationException`; valid options do not throw.
- **Inbound pong**: a `PongMessage` frame is handled (not delegated) — released, not delivered to
  the handler, and it does not stop the endpoint (proving the override against the base `TextFail`).
- **Keep-alive loop (`KeepAliveTick`)**: sends a `PingMessage` frame when `PingPeriod` has elapsed
  and continues; sends nothing within the period; stops with `TimeOut` when `DisconnectTimeout`
  has elapsed; stops with `TextFail` when the ping cannot be admitted.

### `RawReliableServerEndpoint` contract matrix

- **Constructor**: a non-positive `disconnectTimeout` throws `ArgumentException`.
- **Ping answering**: a `PingMessage` frame is answered with a `PongMessage` frame, is not delivered
  to the handler, and keeps the endpoint connected; when the pong cannot be admitted the endpoint
  stops with `TextFail`.
- **Idle-session watchdog (`PeriodicTick`)**: does nothing when not connected or within the
  disconnect timeout; after the timeout elapses it stops the endpoint with `TimeOut`; an inbound
  frame refreshes the deadline so a subsequent tick within the timeout does nothing.

Both time-driven paths are exercised deterministically through the `protected virtual DateTime Now`
seam (`TestRawReliableServerEndpoint.SetNow`/`AdvanceNow` for the watchdog, and
`TestRawReliableClientEndpoint.SetNow`/`AdvanceNow` for the keep-alive). The client keep-alive loop
body lives in the synchronous `KeepAliveTick`; the harness overrides the `protected virtual
StartKeepAlive` scheduling seam to a no-op and drives `KeepAliveTick` directly, so the asynchronous
`Task.Run`/`Task.Delay` wrapper is the only part left untested.

### `RawReliableAckClientEndpoint` contract matrix

- **Handshake request**: on connection the endpoint calls `FillAckData` once and sends one
  `AckRequestMessage`-framed message; the logical `IsConnected` stays false until the response
  arrives (the low-level connection is already up).
- **Handshake completion**: an `AckResponseMessage` frame completes the handshake — `OnConnected`
  is invoked once with the endpoint and the response payload, and `IsConnected` becomes true.
- **Handshake failure**: a second acknowledgement response (wrong stage) fails the endpoint hard
  (`ExceptionFail`); a `FillAckData` that throws stops with `TextFail`; an ack request that cannot be
  admitted stops with `TextFail`.
- **Disconnect / FailHard notifications**: `OnDisconnected` is raised exactly once when the endpoint
  was logically connected (both on graceful disconnect and on hard failure), and never while the
  handshake is still pending.

### `RawReliableAckServerEndpoint` contract matrix

- **Handshake response**: connecting sends nothing; the first `AckRequestMessage` frame calls
  `FillAckResponse` once, sends one `AckResponseMessage`-framed response, invokes `OnConnected` once
  with the endpoint, and flips logical `IsConnected` to true.
- **Handshake failure**: a second acknowledgement request (wrong stage) fails the endpoint hard
  (`ExceptionFail`); a `FillAckResponse` that throws stops with `TextFail`; a response that cannot be
  admitted stops with `TextFail`.
- **Disconnect / FailHard notifications**: `OnDisconnected` is raised exactly once when the endpoint
  was logically connected (both on graceful disconnect and on hard failure), and never while the
  handshake is still pending.
- **Watchdog interaction**: an acknowledgement request refreshes the idle-session watchdog deadline.

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

For a reliable endpoint, connection completion goes through `FinishConnect` (which also completes
the raw start):

```csharp
using var handler = new RecordingRawReliableHandler(MemoryRental.Shared);
using var ep = new TestRawReliableEndpoint(handler);

ep.ConnectAndComplete();        // Start -> BeginConnect -> FinishConnect -> OnConnected

Assert.That(ep.IsConnected, Is.True);
Assert.That(ep.OnConnectedCalls, Is.EqualTo(1));
```

### Test doubles

- **`ManualSerialActionQueue : ISerialActionQueue`** — the controllable queue described above.
  Endpoints never call `Release` or `ScheduleFast`, so neither is modelled and `onFailToRun` is
  never invoked.
- **`RecordingRawHandler : IRawHandler, IDisposable`** — clones each buffer delivered through
  `OnReceived` for inspection, releases the original, and releases the clones on `Dispose`;
  records `StoppedReason`/`StoppedCount` and an `Events` sequence (`received`/`stopped`) for
  callback-ordering assertions.
- **`RecordingRawReliableHandler : RecordingRawHandler, IRawReliableHandler`** — inherits the
  received/stopped recording and buffer cloning, and adds `OnDisconnected`
  (`DisconnectedReason`/`DisconnectedCount`, plus the `disconnected` event).
- **`TestBuffers`** — builds `UnionDataList` buffers of controlled size
  (`Acquire`/`WithByte`/`WithPayload`).
- **`TestRawEndpoint : RawEndpoint, IDisposable`** — the seam for the class under test. It
  installs the manual queue via `SerialQueueConstructor`, exposes the protected surface
  (`ProcessInboundMessage`, `ScheduleToSend`, `CanSendNow`, `FailHard`, `FinishStart`,
  `FinishStop`, `TransportName`/`Log`/`Memory`), records `BeginStart`/`BeginStop` and captures
  `DoSendRaw` buffers as clones, and provides the completion helpers (`CompleteStart`,
  `CompleteStop`, `StartAndComplete`, `StopAndComplete`).
- **`TestRawReliableEndpoint : RawReliableEndpoint, IDisposable`** — same idea one layer up:
  exposes `FinishConnect`/`FinishDisconnect`/`DisconnectGracefully`/framed inbound, records
  `BeginConnect`/`BeginDisconnect`/`OnConnected` and captured sends, exposes the `MessageType`
  bytes as public consts and static `Frame(...)` builders, and provides `CompleteConnect`/
  `CompleteDisconnect`/`ConnectAndComplete`.
- **`RecordingRawReliableClientHandler` / `RecordingRawReliableServerHandler`** — thin subclasses of
  `RecordingRawReliableHandler` that implement `IRawReliableClientHandler`/`IRawReliableServerHandler`.
- **`TestRawReliableClientEndpoint : RawReliableClientEndpoint, IDisposable`** and
  **`TestRawReliableServerEndpoint : RawReliableServerEndpoint, IDisposable`** — the same double
  pattern one layer up. The client double builds `PingOptions` from optional `pingPeriod`/
  `disconnectTimeout` constructor arguments; the server double overrides the `Now` seam with
  `SetNow`/`AdvanceNow` so the idle-session watchdog is deterministic.
- **`RecordingRawReliableAckClientHandler : RecordingRawReliableClientHandler, IRawReliableAckClientHandler`**
  — records `FillAckData` (with an optional throw mode) and `OnConnected` (endpoint + response payload).
- **`TestRawReliableAckClientEndpoint : RawReliableAckClientEndpoint, IDisposable`** — the same double
  pattern again; its `BeginDisconnect` delegates to the ack base implementation (to raise the logical
  disconnect notification) before recording, and keep-alive is disabled.
- **`RecordingRawReliableAckServerHandler : RecordingRawReliableServerHandler, IRawReliableAckServerHandler`**
  — records `FillAckResponse` (with an optional throw mode) and `OnConnected` (endpoint).
- **`TestRawReliableAckServerEndpoint : RawReliableAckServerEndpoint, IDisposable`** — same double
  pattern; `BeginDisconnect` delegates to the ack base before recording, and it overrides the `Now`
  seam so the watchdog interaction is deterministic.

Buffers are acquired exactly as in production:
`MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>()` (via `TestBuffers`) and released
with `Release()`/`using var _ = list.AsDisposable()`. The harness clones-out captured buffers
and releases the originals so tests can inspect content without leaking pooled memory.

### Why these test-sets need no fake transport

`RawEndpoint`, `RawReliableEndpoint`, `RawReliableClientEndpoint`, `RawReliableServerEndpoint`,
`RawReliableAckClientEndpoint`, and `RawReliableAckServerEndpoint` constructors take the transport
name, logger, and memory rental explicitly rather than a transport owner, so these test-sets need no
transport at all. (The `IRawReliableTransport owner` parameter was removed from the client and server
endpoint constructors for exactly this reason; the Ack subclasses were updated to match.)

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

1. Add a test double under `<Family>/Harness/`, e.g. `RawReliable/Harness/TestRawReliableEndpoint.cs`,
   deriving from the class under test:
   - implement the class's `abstract` hooks;
   - override `SerialQueueConstructor` to install a `ManualSerialActionQueue`;
   - expose the `protected` members you need for the test-set;
   - record hook invocations (`BeginConnect`, `BeginDisconnect`, `OnConnected`, …) and capture
     outbound buffers as clones;
   - provide completion helpers for the class's `Begin*`/`Finish*` hooks (follow
     `TestRawEndpoint`).
2. Add any shared fakes the class needs:
   - a transport fake (top-level `Harness/`) if the class's constructor takes a transport owner
     (`RawReliableClient*`/`RawReliableServer*`/`RawReliableAck*`);
   - a recording handler if the class uses a new handler interface (follow
     `RecordingRawHandler`/`RecordingRawReliableHandler`).
3. Add `<Family>/<ClassName>Tests.cs` and cover **that class's delta** plus its invariants.
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
