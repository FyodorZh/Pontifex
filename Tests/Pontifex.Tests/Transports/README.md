# Transports Test Suite

Black-box contract tests for the abstract transport class hierarchy in `Pontifex`.

The suite lives under `Tests/Pontifex.Tests/Transports/` and tests the transport classes
directly, without carriers, sockets, threads, timers, or wall-clock time. It is the
transport-level counterpart of the `Endpoints` suite: the same deterministic,
public-plus-protected-contract approach, applied one layer up.

## Suite type

**Abstract Test Suit.** Every subject in the selected inheritance tree is abstract, so the suite
is structured as one layer per abstract type rather than one Test Suit per class.

Root subject: `Pontifex.AnyTransport`.

Selected inheritance tree:

```
AnyTransport
└── RawTransport
    ├── RawReliableTransport
    │   ├── RawReliableClientTransport<TClientHandler>
    │   │   └── RawReliableAckClientTransport
    │   └── RawReliableServerTransport<TServerHandler>
    │       └── RawReliableAckServerTransport
```

Planned layers and coverage gaps are recorded in the owning layer's `Coverage.md`.

## Contract references

- [AnyTransport.cs](../../../Pontifex/Implementations/AnyTransport.cs)
- [ITransport.cs](../../../Pontifex/Abstractions/Transports/ITransport.cs)
- [RawTransport.cs](../../../Pontifex/Implementations/Raw/RawTransport.cs)
- [IRawTransport.cs](../../../Pontifex/Abstractions/Transports/Raw/IRawTransport.cs)
- [RawReliableTransport.cs](../../../Pontifex/Implementations/Raw/Reliable/RawReliableTransport.cs)
- [IRawReliableTransport.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableTransport.cs)
- [RawReliableClientTransport.cs](../../../Pontifex/Implementations/Raw/Reliable/RawReliableClientTransport.cs)
- [IRawReliableClientTransport.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableClientTransport.cs)
- [IRawReliableClientHandler.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableClientHandler.cs)
- [RawReliableServerTransport.cs](../../../Pontifex/Implementations/Raw/Reliable/RawReliableServerTransport.cs)
- [IRawReliableServerTransport.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableServerTransport.cs)
- [IRawReliableServerHandler.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableServerHandler.cs)
- [RawReliableAckClientTransport.cs](../../../Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckClientTransport.cs)
- [IRawReliableAckClient.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Client/IRawReliableAckClient.cs)
- [IRawReliableAckClientHandler.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Client/IRawReliableAckClientHandler.cs)
- [RawReliableAckServerTransport.cs](../../../Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckServerTransport.cs)
- [IRawReliableAckServer.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Server/IRawReliableAckServer.cs)
- [IRawReliableAckServerAcknowledger.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Server/IRawReliableAckServerAcknowledger.cs)
- [IRawReliableAckServerHandler.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Server/IRawReliableAckServerHandler.cs)

## Layers

The layer table is the authoritative declaration of layer name, subject, directory,
inheritance parent, and coverage file.

| Layer | Subject | Directory | Inheritance parent | Coverage |
|---|---|---|---|---|
| `AnyTransport` | `Pontifex.AnyTransport` | `Tests/Pontifex.Tests/Transports` | — (root) | [`Coverage.md`](Coverage.md) |
| `RawTransport` | `Pontifex.Raw.RawTransport` | `Tests/Pontifex.Tests/Transports/Raw` | `AnyTransport` | [`Raw/Coverage.md`](Raw/Coverage.md) |
| `RawReliableTransport` | `Pontifex.Raw.Reliable.RawReliableTransport` | `Tests/Pontifex.Tests/Transports/Raw/Reliable` | `RawTransport` | [`Raw/Reliable/Coverage.md`](Raw/Reliable/Coverage.md) |
| `RawReliableClientTransport` | `Pontifex.Raw.Reliable.RawReliableClientTransport<TClientHandler>` | `Tests/Pontifex.Tests/Transports/Raw/Reliable/Client` | `RawReliableTransport` | [`Raw/Reliable/Client/Coverage.md`](Raw/Reliable/Client/Coverage.md) |
| `RawReliableServerTransport` | `Pontifex.Raw.Reliable.RawReliableServerTransport<TServerHandler>` | `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server` | `RawReliableTransport` | [`Raw/Reliable/Server/Coverage.md`](Raw/Reliable/Server/Coverage.md) |
| `RawReliableAckClientTransport` | `Pontifex.Raw.Reliable.Ack.RawReliableAckClientTransport` | `Tests/Pontifex.Tests/Transports/Raw/Reliable/Client/Ack` | `RawReliableClientTransport<TClientHandler>` | [`Raw/Reliable/Client/Ack/Coverage.md`](Raw/Reliable/Client/Ack/Coverage.md) |
| `RawReliableAckServerTransport` | `Pontifex.Raw.Reliable.Ack.RawReliableAckServerTransport` | `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/Ack` | `RawReliableServerTransport<TServerHandler>` | [`Raw/Reliable/Server/Ack/Coverage.md`](Raw/Reliable/Server/Ack/Coverage.md) |

Directory nesting does not imply inheritance. The inheritance parent is the one declared in the
layer table above.

## Naming conventions

- Layer name = subject type name.
- Test file = `<Subject>Tests.cs`; fixture class = `<Subject>Tests`.
- Test methods = `Scenario_ExpectedResult`, for example
  `ConnectionFinished_WhenActionThrows_FailsTransportWithoutConnecting`.
- Namespace = `Pontifex.Tests.Transports` for every file in the suite, including harness files.
- Every fixture carries `[Category("Transports")]`.

## Directory layout

```
Transports/
├── README.md
├── Coverage.md                                      AnyTransport layer
├── AnyTransportTests.cs
├── Harness/
│   └── TestAnyTransport.cs
└── Raw/                                             RawTransport layer
    ├── Coverage.md
    ├── RawTransportTests.cs
    ├── Harness/
    │   └── TestRawTransport.cs
    └── Reliable/                                    RawReliableTransport layer
        ├── Coverage.md
        ├── RawReliableTransportTests.cs
        ├── Harness/
        │   └── TestRawReliableTransport.cs
        ├── Client/                                  RawReliableClientTransport layer
        │   ├── Coverage.md
        │   ├── RawReliableClientTransportTests.cs
        │   ├── Harness/
        │   │   ├── RecordingRawReliableClientHandler.cs
        │   │   └── TestRawReliableClientTransport.cs
        │   └── Ack/                                 RawReliableAckClientTransport layer
        │       ├── Coverage.md
        │       ├── RawReliableAckClientTransportTests.cs
        │       └── Harness/
        │           └── TestRawReliableAckClientTransport.cs
        └── Server/                                  RawReliableServerTransport layer
            ├── Coverage.md
            ├── RawReliableServerTransportTests.cs
            ├── Harness/
            │   ├── ManualPeriodicLogicDriver.cs
            │   ├── ScriptedSerialActionQueue.cs
            │   ├── TestRawReliableServerSession.cs
            │   └── TestRawReliableServerTransport.cs
            └── Ack/                                 RawReliableAckServerTransport layer
                ├── Coverage.md
                ├── RawReliableAckServerTransportTests.cs
                └── Harness/
                    ├── RecordingRawReliableAckServerAcknowledger.cs
                    └── TestRawReliableAckServerTransport.cs
```

## Harness layout and sharing

Harness contains stubs, recording doubles, test implementations of abstract types, and manual
drivers.

- The root `Harness/` holds the root layer's `TestAnyTransport`.
- `Raw/Harness/` holds `TestRawTransport`.
- `Raw/Reliable/Harness/` holds `TestRawReliableTransport`.
- Client-specific harness stays in `Raw/Reliable/Client/Harness/`.
- Server-specific harness stays in `Raw/Reliable/Server/Harness/`. `ManualPeriodicLogicDriver`
  is here because it is shared by the server layer and its Ack child.
- Ack-specific harness stays in the corresponding Ack layer's `Harness/`.
- The client layer reuses the endpoint double from
  `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/Harness`. Both suites live in the same test
  assembly and the endpoint double is covered by the `Endpoints` suite.

## Coverage

Coverage is recorded per layer, next to the layer's tests:

- [Root `Coverage.md`](Coverage.md) — `AnyTransport`
- [`Raw/Coverage.md`](Raw/Coverage.md) — `RawTransport`
- [`Raw/Reliable/Coverage.md`](Raw/Reliable/Coverage.md) — `RawReliableTransport`
- [`Raw/Reliable/Client/Coverage.md`](Raw/Reliable/Client/Coverage.md) — `RawReliableClientTransport`
- [`Raw/Reliable/Server/Coverage.md`](Raw/Reliable/Server/Coverage.md) — `RawReliableServerTransport`
- [`Raw/Reliable/Client/Ack/Coverage.md`](Raw/Reliable/Client/Ack/Coverage.md) — `RawReliableAckClientTransport`
- [`Raw/Reliable/Server/Ack/Coverage.md`](Raw/Reliable/Server/Ack/Coverage.md) — `RawReliableAckServerTransport`

Each layer covers only the contract delta and invariants introduced by its subject. Ancestor
behaviour is not repeated. A layer's `Coverage.md` lists the current covered tests, planned
gaps, and deferred-by-design gaps for that layer.

## Categories

- Suite root category: `Transports`. Every fixture carries it.
- Additional filtering categories: none currently.

Any future non-default facet (real I/O, load, timing stress) must go into a separate layer or
group with its own declared category, not the default suite run.

## Contract summaries

### `AnyTransport` contract matrix

- **Constructor / observables**: `Name` and `Memory` are captured by the base constructor;
  `Type` is abstract and supplied by the concrete transport; `Log` is a non-null wrapped logger;
  `IsValid` is `true`; `IsStarted` is `false`; `HasStartBeenAttempted` is `false`.
- **Start**: null callback throws `ArgumentNullException` without attempting a start; the happy
  path calls `TryStart` then `OnStarted` and returns `true`; a `TryStart` that returns `false`
  invalidates the transport, returns `false`, and raises no stop notification; a second `Start`
  or a `Start` after a stop/fail returns `false` without calling the hooks again; a `Start` after
  a stop that happened *before* the first start still succeeds.
- **Start / stop race**: if `TryStart` stops the transport synchronously, `Start` returns `false`,
  `OnStarted` is not called, and the stop callback is delivered once.
- **Stop**: before start is a no-op that returns `true` and does not consume the one-shot start;
  while running it stops the transport, invokes `OnStopped` and then the `Start` callback, and
  keeps `IsValid` true; a null reason becomes `Unknown` sourced from `Name`; repeated stops return
  `true` without a second notification; a stop on an invalid transport returns `false`.
- **Failure visibility**: during `OnStopped` from `Stop`, `IsStarted` is already false and
  `IsValid` is still true; during `OnStopped` from `Fail`, both are false.
- **Fail**: while running it invalidates the transport and notifies `OnStopped` then the start
  callback; while not started it only invalidates; after a stop it invalidates without a second
  notification; repeated failures do the first one only.
- **Fail overloads**: `Fail(string, string, params object[])` creates a `TextFail` sourced from
  `Name`; `Fail(StopReason, string)` creates a `ChainFail`; `FailException` creates an
  `ExceptionFail`. The reason delivered to `OnStopped` and the start callback is the same instance.
- **Exception isolation**: an exception thrown by `OnStopped` or by the start callback is caught;
  the other callback is still invoked and the lifecycle call completes.
- **HasStartBeenAttempted**: becomes true when `Start` is accepted for processing, including a
  failed `TryStart`; a stop before the first start leaves it false; it never resets.
- **ToString** returns the runtime type name.

### `RawTransport` contract matrix

`RawTransport` is a thin typed facade over `AnyTransport`; its delta is small, so this layer
stays small and deliberately does not repeat the full `AnyTransport` matrix.

- **Constructor**: `Name` and `Memory` are forwarded to the base; `Log` is non-null.
- **Message size**: `MessageMaxByteSize` is supplied by the derived transport, reflects the
  configured value, and is exposed through `IRawTransport`.
- **Interface exposure**: an instance is assignable to `IRawTransport` and `ITransport`.

### `RawReliableTransport` contract matrix

`RawReliableTransport` is a marker facade over `RawTransport` and adds no lifecycle or
message-handling behavior.

- **Interface exposure**: an instance is assignable to `IRawReliableTransport`.

### `RawReliableClientTransport<TClientHandler>` contract matrix

The client transport owns a small state machine (`Constructed`, `Initialized`, `Connecting`,
`Connected`, `Disconnected`) and the endpoint hand-off. This layer covers that delta; the
inherited `AnyTransport` lifecycle and the `RawTransport`/`RawReliableTransport` facades are not
repeated.

- **Init**: from `Constructed`, `Init` runs `SetupHandler`, stores the returned handler, and moves
  to `Initialized`; a null handler, a null `SetupHandler` result, a wrong state, or an already
  started transport fails and invalidates. A second `Init` fails but keeps the first handler.
  `Init` after a failed start does not call `SetupHandler`; `Init` while connecting or connected
  fails and drives the stop path (handler `OnStopped`, or `OnDisconnected` then `OnStopped`).
- **Start / connect**: `Start` works only from `Initialized`; it calls `BeginConnect` and, while
  the state is still `Initialized`, moves to `Connecting`; `OnStarted` invokes
  `OnReadyToConnect`. A `false` from `BeginConnect` fails, destroys, and notifies the handler
  `OnStopped`; a `BeginConnect` that stops the transport synchronously makes `Start` return
  `false` without `OnReadyToConnect`.
- **ConnectionFinished / ConnectionFailed**: `ConnectionFinished` is legal only from `Connecting`;
  it invokes the supplied action with the stored handler *before* setting `Connected`. If the
  action stops or fails the transport synchronously, the state is not overwritten back to
  `Connected`. If the action throws, the transport fails with an `ExceptionFail` carrying that
  exception and does not become connected. `ConnectionFailed` is legal only from `Connecting` and
  fails with a `TextFail` whose text is `Connection failed`. From any other state both methods
  call `Stop` with a `TextFail`; while `Constructed` or `Initialized` that stop is a no-op and a
  later `Init`/`Start` can still succeed.
- **IsConnected**: without an attached endpoint it mirrors the internal state (`Connected` only)
  and is already `false` during `OnDisconnected`, during `OnStopped`, and inside
  `DestroyTransport`. With an attached endpoint it delegates to `endpoint.IsConnected` until the
  base detaches, so a still-connected attached endpoint can report `true` inside
  `DestroyTransport`.
- **Endpoint attach / detach ownership**: `AttachEndpoint` rejects `null` and a second endpoint
  (the first stays attached); an attached endpoint's terminal `Stopped` event stops the transport
  with the same reason instance. The base class owns detachment: `DestroyTransport` can still read
  `AttachedEndpoint`, and after `Stop` completes the exposed attached endpoint is `null`.
  `DetachEndpoint` is **private**; implementations must not detach.
- **Stop notification matrix**: stopping after `Init` but before `Start` is a no-op that keeps the
  `Initialized` state and still allows a later `Start`. While `Connecting` the order is
  `handler.OnStopped`, `DestroyTransport`, detach, start callback. While `Connected` it is
  `handler.OnDisconnected`, `handler.OnStopped`, `DestroyTransport`, detach, start callback. While
  `Initialized` it is `DestroyTransport`, detach, `handler.OnStopped`. With an attached endpoint the
  transport skips its own handler notifications because the endpoint owns them.
- **Handler exception isolation**: exceptions from `OnDisconnected` or `OnStopped` are caught,
  logged, and swallowed at this layer; the remaining notifications, `DestroyTransport`, detach, and
  the start callback still run, and the transport ends in the disconnected state.

### `RawReliableServerTransport<TServerHandler>` contract matrix

The server transport owns the carrier hooks, the bounded admission gates, the live-session registry,
and the shared ticker. This layer covers that delta; the inherited `AnyTransport` lifecycle and
the `RawTransport`/`RawReliableTransport` facades are not repeated.

- **Constructor / interface**: each numeric limit (`maxConcurrentHandshakes`, `maxActiveSessions`,
  `tickPeriodMs`) rejects values below 1 with `ArgumentOutOfRangeException` and the matching parameter
  name. An instance is assignable to `IRawReliableServerTransport<TServerHandler>`,
  `IRawReliableTransport`, `IRawTransport`, and `ITransport`.
- **Initialization / start**: `TryMarkInitialized` succeeds once and fails afterwards. `Start` before
  initialization fails, invalidates the transport, calls neither `StartCarrier` nor the ticker
  factory, and does not invoke the start callback. After initialization, `Start` calls `StartCarrier`
  and then creates and starts the ticker. A `false` or throwing `StartCarrier` invalidates the
  transport and creates no ticker. A second `Start` does nothing. A stop before the first start is a
  no-op that leaves the transport startable. If `StartCarrier` stops the transport synchronously,
  `Start` returns `false`, `OnStarted` and the ticker do not run, and the stop callback still fires.
- **Handshake gate**: `TryBeginHandshake` is `false` before start and after stop or fail. While started
  it admits exactly `maxConcurrentHandshakes` slots; `EndHandshake` releases one. The gate is
  independent of the session gate. A bounded parallel test confirms the lock-free counter never
  exceeds its limit.
- **Session gate**: `TryReserveSession` follows the same lifecycle and limit rules, with
  `ReleaseSession` releasing a slot. `ActiveSessions` reflects the current reservations.
- **Admission**: `TryStartAndRegisterSession` rejects `null`. On the happy path it starts the endpoint,
  adds it to the registry, and keeps the reserved slot; the endpoint is started on its own queue, so
  `BeginConnect` runs when that queue drains. When the transport is not alive, the endpoint is started
  and immediately stopped with a `TextFail` sourced from the transport name, the slot is released
  directly, and `OnSessionStopped` is **not** called. If the endpoint rejects or throws from `Start`,
  the slot is released through the endpoint's terminal `Stopped` notification, which may be deferred
  past the `false` return; `OnSessionStopped` is not called. The same no-hook rule applies when the
  endpoint stops synchronously during start or the transport begins stopping during start. The slot is
  released exactly once on every path.
- **Session termination**: when a registered session stops, its slot is released, it is removed from
  the registry, and `OnSessionStopped` is invoked once with the same reason. `ActiveSessions` is
  already decremented inside the hook. A repeated stop does not notify twice, and stopping one session
  does not affect the others. A throwing `OnSessionStopped` is isolated: the endpoint still stops, the
  slot is still released, and the transport stays valid.
- **Transport stop**: `Stop` calls `StopCarrier` with the same reason instance, stops the ticker, then
  asks every registered session to stop with the transport reason and `tryBeGentle: false`. Sessions
  already stopped are not stopped again. A null reason is replaced by `Unknown` sourced from the
  transport name before it reaches the carrier. Stopping before start does not call `StopCarrier` and
  still allows a later start. `Fail` runs the same stop delta and invalidates the transport.
- **Ticker**: the ticker is created through the `LogicDriverFactory` seam and started exactly once
  after a successful carrier start. Each tick calls `PeriodicTick` on every registered session; a
  stopped session is skipped, a session added after start is picked up, and an exception from one
  session does not prevent the others from ticking. Ticks after the transport stops are ignored, and
  no ticker is created when the start fails. The test double supplies a hand-driven
  `ILogicDriver<IPeriodicLogicDriverCtl>`, so no background thread or real time is involved.

### `RawReliableAckClientTransport` contract matrix

`RawReliableAckClientTransport` is a facade over
`RawReliableClientTransport<IRawReliableAckClientHandler>`. It only fixes `Type` to
`RawReliableAck` and exposes `IRawReliableAckClient`; the inherited init, connect state machine,
endpoint ownership, connectivity, and terminal notifications are covered by the
`RawReliableClientTransport` layer and are not repeated.

- **Interface exposure**: `IRawReliableAckClient`,
  `IRawReliableClientTransport<IRawReliableAckClientHandler>`, `IRawReliableTransport`,
  `IRawTransport`, and `ITransport`.
- **Type**: `Type` is `TransportType.RawReliableAck`.

### `RawReliableAckServerTransport` contract matrix

The Ack server transport adds the acknowledger lifecycle and the acknowledgement hand-off to
`RawReliableServerTransport<IRawReliableAckServerHandler>`. The inherited session admission and
ticker behavior is covered by the `RawReliableServerTransport` layer and is not repeated.

- **Interface / type**: `IRawReliableAckServer`,
  `IRawReliableServerTransport<IRawReliableAckServerHandler>`, `IRawReliableTransport`,
  `IRawTransport`, and `ITransport`; `Type` is `TransportType.RawReliableAck`.
- **Init**: null throws without changing the lifecycle and a later valid `Init` succeeds. The first
  call succeeds; a second call returns `false` and keeps the first acknowledger. `Init` after a start
  attempt, after a normal stop, after a failed start, or after a fail returns `false`. `Init` after a
  stop before the first start succeeds. Concurrent calls have exactly one winner. If a subclass
  already marked the transport initialized, `Init` returns `false` without installing the
  acknowledger.
- **TryAcknowledge gating**: returns `null` without calling the acknowledger when the transport is
  invalid, not started, or has no acknowledger. This covers before-start, after-stop, and after-fail.
- **TryAcknowledge hand-off**: when started, passes the remote endpoint and the same
  `UnionDataList` with one additional reference to the acknowledger, and returns its handler
  unchanged. The caller keeps its own reference; the acknowledger must release the added reference
  exactly once. A rejection returns `null`. An acknowledger exception is caught/logged, returns
  `null`, and leaves the transport valid and started.
- **Ownership**: the caller's buffer remains alive with its original reference count after the
  acknowledger releases; mutations made through the shared list are visible to the caller.

## How it works

### Black-box boundary

The boundary of a layer is **public + protected**. Tests reach the abstract hooks through a
**test double** that derives from the class under test; they never inspect private fields and
never use reflection. Only these are asserted:

- method return values,
- the `Start`-supplied stop callback and its reason,
- the abstract hook invocations (`TryStart`, `OnStarted`, `OnStopped`, and the client/server
  admission hooks) and their order,
- read-only observables (`Name`, `Memory`, `Log`, `IsValid`, `IsStarted`,
  `HasStartBeenAttempted`, `IsConnected`, `ActiveSessions`),
- the state observed from inside callbacks and protected hooks.

### Test doubles

- `TestAnyTransport : AnyTransport` implements `Type` and the three abstract lifecycle hooks. It
  records `TryStartCalls`, `OnStartedCalls`, `OnStoppedCalls`, `LastOnStoppedReason`, the state
  observed inside `OnStopped`, hook names in invocation order, and lets each test choose the
  `TryStart` result or supply behavior for the hooks.
- `TestRawTransport : RawTransport` supplies the abstract `MessageMaxByteSize` and `Type` values
  and implements the inherited lifecycle hooks with no additional behavior.
- `TestRawReliableTransport : RawReliableTransport` supplies the inherited abstract members and a
  trivial `RawReliableConformanceControl` constructor argument only.
- `TestRawReliableClientTransport : RawReliableClientTransport<IRawReliableClientHandler>` supplies
  the abstract `Type`, `MessageMaxByteSize`, `BeginConnect`, `OnReadyToConnect`, and
  `DestroyTransport` members. It records hook calls, lets each test choose the `BeginConnect`
  result and the `SetupHandler` transformation, exposes the protected `AttachedEndpoint`, and
  records the endpoint and `IsConnected` observed inside `DestroyTransport`.
- `RecordingRawReliableClientHandler : IRawReliableClientHandler, IDisposable` records
  `Received`/`Disconnected`/`Stopped` counts and reasons, can throw a configured exception from
  any callback, and optionally appends callback names to the shared event log. `OnReceived` does
  not release the delivered buffer; the test owns it (buffers still alive at `Dispose` are
  released as a safety net).
- `TestRawReliableServerTransport : RawReliableServerTransport<IRawReliableServerHandler>`
  supplies the abstract `Type`, `MessageMaxByteSize`, `StartCarrier`, and `StopCarrier` members,
  records carrier/reason call counts, exposes the protected admission and session-registry
  surface, and overrides `LogicDriverFactory` to return a `ManualPeriodicLogicDriver`.
- `TestRawReliableServerSession : RawReliableServerEndpoint` drives its lifecycle through a
  `ScriptedSerialActionQueue` that can run actions inline or on an explicit drain, and exposes a
  controllable clock with a read counter and a configurable exception.
- `ManualPeriodicLogicDriver : ILogicDriver<IPeriodicLogicDriverCtl>` is the hand-driven ticker
  driver. `Tick()` is explicit; no thread or timer is involved.
- `ScriptedSerialActionQueue : ISerialActionQueue` models the production serial queue's
  one-action-at-a-time and error-routing behavior, and adds `RunImmediately`,
  `RejectNextSchedule`, and `ThrowNextSchedule`.
- `TestRawReliableAckClientTransport : RawReliableAckClientTransport` supplies the inherited
  abstract members and adds no behavior.
- `TestRawReliableAckServerTransport : RawReliableAckServerTransport` supplies the carrier hooks
  and a hand-driven ticker, and exposes `TryAcknowledge`, `TryMarkInitialized`,
  `HasStartBeenAttempted`, and the carrier call counters.
- `RecordingRawReliableAckServerAcknowledger` records the remote endpoint, the acknowledgement
  buffer, and its reference count around a configurable behavior. The behavior owns the additional
  reference; the recorder releases it as a safety net if the behavior does not.
- A trivial `RawReliableConformanceControl` is passed only because the production constructor
  requires one. It is never armed, inspected, or asserted.

### Typical test shape

```csharp
var transport = new TestAnyTransport("TestTransport");
int stopCallbackCount = 0;

Assert.That(transport.Start(_ => stopCallbackCount++), Is.True);
Assert.That(transport.IsStarted, Is.True);

Assert.That(transport.Stop(new UserIntention("test")), Is.True);
Assert.That(transport.IsStarted, Is.False);
Assert.That(transport.OnStoppedCalls, Is.EqualTo(1));
Assert.That(stopCallbackCount, Is.EqualTo(1));
```

Guard cases are written the same way: configure the double, call the public lifecycle method,
and assert the observable result.

## Conventions

- NUnit 4. `using NUnit.Framework;` is global (declared in the test project).
- `namespace Pontifex.Tests.Transports;`, `public sealed class <ClassName>Tests`.
- One test class per file; file named after the class.
- Test methods: `Scenario_ExpectedResult`.
- Hand-written fakes only.
- Add `[Category("Transports")]` to each test class.
- **Do not use the conformance subsystem in this suite.** Do not obtain it through
  `GetControls`, do not inject a conformance control, do not arm gates, and do not call
  `FailNextStart`/`InjectUnrecoverableFailure`.
- If a test allocates `UnionDataList` or byte arrays, add
  `[TearDown] public void TearDown() => GC.Collect();`.

## Adding a layer

1. Ensure the parent layer already exists.
2. Create the layer directory and add `Coverage.md` with layer identity, subject, directory,
   inheritance parent, contract link, covered tests, planned gaps, and deferred-by-design gaps.
3. Add `Harness/` only if the layer needs new doubles; reuse ancestor harness where possible.
4. Add `<Subject>Tests.cs` with `[Category("Transports")]`.
5. Add the layer row to the table and the directory layout in this README.
6. Run the suite and record it in `Coverage.md`.

## Adding tests

1. Put the test in the layer that owns the behavior (the subject's delta), not in an ancestor or
   sibling layer.
2. Use the public and protected contract.
3. Drive queues, clocks, and completion explicitly through the harness seams.
4. Assert return values, callbacks and reasons, captured buffers, ordering, counts, and
   contract-visible state.
5. Record the test in the owning layer's `Coverage.md`.

## Running

Run the whole test project:

```
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj
```

Filter to this suite:

```
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports
```

## Out of scope

- Endpoint behavior: `RawReliableEndpoint` and the raw-reliable endpoint family are covered by
  the `Endpoints` suite.
- Carrier behavior: delivery, ordering, socket handling, and end-to-end topology behavior belong
  to the conformance suite in `Tests/Pontifex.Transports.Tests`.
- Concrete carrier transports in the `Transports` projects (Direct, TCP, UDP).
- The conformance subsystem: `IConformanceControl`, gates, failure injection, and the
  conformance role of `GetControls`.
- The public `Stopped` event on transports, where it is raised independently of the handler
  callbacks.
- Real time, threads, timers, and wall-clock scheduling.

## Rules

This suite follows [Tests/RULES.md](../../RULES.md).
