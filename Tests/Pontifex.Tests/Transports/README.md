# Transports Test Suite

Black-box contract tests for the abstract transport class hierarchy in `Pontifex`.

The suite lives under `Tests/Pontifex.Tests/Transports/` and tests the transport classes
directly, without carriers, sockets, timers, or wall-clock time. It is the transport-level
counterpart of the `Endpoints` suite: the same deterministic, public-plus-protected-contract
approach, applied one layer up. Four categorised tests use gated, joined threads to prove the
admission lock's and the handler-factory lock's single-call invariants, and one bounded parallel
test proves that the admission counters never exceed their limits; see
[Categories](#categories).

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
    │   │   └── RawReliableEndpointClientTransport<TClientHandler>
    │   └── RawReliableServerTransport<TServerHandler>
    │       └── RawReliableAdmissionServerTransport
    └── RawUnreliableTransport
        ├── RawUnreliableClientTransport
        └── RawUnreliableServerTransport<TFactory>
```

The four concrete unreliable carriers (Ack/NoAck × Direct/UDP) live in the `Transports` projects
and are out of scope for this suite, like the reliable carriers.

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
- [IRawReliableClientHandler.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/Client/IRawReliableClientHandler.cs)
- [RawReliableServerTransport.cs](../../../Pontifex/Implementations/Raw/Reliable/RawReliableServerTransport.cs)
- [IRawReliableServerTransport.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableServerTransport.cs)
- [IRawReliableServerHandler.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableServerHandler.cs)
- [RawReliableEndpointClientTransport.cs](../../../Pontifex/Implementations/Raw/Reliable/RawReliableEndpointClientTransport.cs)
- [IRawReliableClient.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/Client/IRawReliableClient.cs)
- [RawReliableAdmissionServerTransport.cs](../../../Pontifex/Implementations/Raw/Reliable/RawReliableAdmissionServerTransport.cs)
- [RawReliableHandshake.cs](../../../Pontifex/Implementations/Raw/Reliable/Handshake/RawReliableHandshake.cs)
- [IRawReliableServer.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/Server/IRawReliableServer.cs)
- [IRawReliableServerHandlerFactory.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/Server/IRawReliableServerHandlerFactory.cs)
- [RawUnreliableTransport.cs](../../../Pontifex/Implementations/Raw/Unreliable/RawUnreliableTransport.cs)
- [IRawUnreliableTransport.cs](../../../Pontifex/Abstractions/Transports/Raw/Unreliable/IRawUnreliableTransport.cs)
- [IRawUnreliableDebugReliability.cs](../../../Pontifex/Implementations/Raw/Unreliable/IRawUnreliableDebugReliability.cs)
- [RawUnreliableTransportConformanceControl.cs](../../../Pontifex/Implementations/Raw/Unreliable/RawUnreliableTransportConformanceControl.cs)
- [RawUnreliableClientTransport.cs](../../../Pontifex/Implementations/Raw/Unreliable/RawUnreliableClientTransport.cs)
- [IRawUnreliableClient.cs](../../../Pontifex/Abstractions/Transports/Raw/Unreliable/IRawUnreliableClient.cs)
- [IRawUnreliableHandler.cs](../../../Pontifex/Abstractions/Transports/Raw/Unreliable/IRawUnreliableHandler.cs)
- [RawUnreliableServerTransport.cs](../../../Pontifex/Implementations/Raw/Unreliable/RawUnreliableServerTransport.cs)
- [IRawUnreliableEndpoint.cs](../../../Pontifex/Abstractions/Transports/Raw/Unreliable/IRawUnreliableEndpoint.cs)
- [RawUnreliableServerEndpoint.cs](../../../Pontifex/Implementations/Endpoints/Raw/Unreliable/RawUnreliableServerEndpoint.cs)
- [SendBudget.cs](../../../Pontifex/Implementations/Endpoints/Raw/Unreliable/SendBudget.cs)
- [Unreliable Specification.md](../../../Pontifex/Abstractions/Transports/Raw/Unreliable/Specification.md)

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
| `RawReliableEndpointClientTransport` | `Pontifex.Raw.Reliable.RawReliableEndpointClientTransport<TClientHandler>` | `Tests/Pontifex.Tests/Transports/Raw/Reliable/Client/RawReliableEndpointClientTransport` | `RawReliableClientTransport<TClientHandler>` | [`Raw/Reliable/Client/RawReliableEndpointClientTransport/Coverage.md`](Raw/Reliable/Client/RawReliableEndpointClientTransport/Coverage.md) |
| `RawReliableAdmissionServerTransport` | `Pontifex.Raw.Reliable.RawReliableAdmissionServerTransport` | `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/RawReliableAdmissionServerTransport` | `RawReliableServerTransport<TServerHandler>` | [`Raw/Reliable/Server/RawReliableAdmissionServerTransport/Coverage.md`](Raw/Reliable/Server/RawReliableAdmissionServerTransport/Coverage.md) |
| `RawUnreliableTransport` | `Pontifex.Raw.Unreliable.RawUnreliableTransport` | `Tests/Pontifex.Tests/Transports/Raw/Unreliable` | `RawTransport` | [`Raw/Unreliable/Coverage.md`](Raw/Unreliable/Coverage.md) |
| `RawUnreliableClientTransport` | `Pontifex.Raw.Unreliable.RawUnreliableClientTransport` | `Tests/Pontifex.Tests/Transports/Raw/Unreliable/Client` | `RawUnreliableTransport` | [`Raw/Unreliable/Client/Coverage.md`](Raw/Unreliable/Client/Coverage.md) |
| `RawUnreliableServerTransport` | `Pontifex.Raw.Unreliable.RawUnreliableServerTransport<TFactory>` | `Tests/Pontifex.Tests/Transports/Raw/Unreliable/Server` | `RawUnreliableTransport` | [`Raw/Unreliable/Server/Coverage.md`](Raw/Unreliable/Server/Coverage.md) |

Directory nesting does not imply inheritance. The inheritance parent is the one declared in the
layer table above.

## Naming conventions

- Layer name = subject type name.
- Test file = `<Subject>Tests.cs`; fixture class = `<Subject>Tests`.
- Test methods = `Scenario_ExpectedResult`, for example
  `Start_WhenBeginConnectReturnsFalse_DestroysWithoutNotifyingHandler`.
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
    ├── Reliable/                                    RawReliableTransport layer
    │   ├── Coverage.md
    │   ├── RawReliableTransportTests.cs
    │   ├── Harness/
    │   │   └── TestRawReliableTransport.cs
    │   ├── Client/                                  RawReliableClientTransport layer
    │   │   ├── Coverage.md
    │   │   ├── RawReliableClientTransportTests.cs
    │   │   ├── Harness/
    │   │   │   ├── RecordingRawReliableClientHandler.cs
    │   │   │   └── TestRawReliableClientTransport.cs
    │   │   └── RawReliableEndpointClientTransport/  RawReliableEndpointClientTransport layer
    │   │       ├── Coverage.md
    │   │       ├── RawReliableEndpointClientTransportTests.cs
    │   │       └── Harness/
    │   │           ├── FakeRawReliableClientEndpoint.cs
    │   │           └── TestRawReliableEndpointClientTransport.cs
    │   └── Server/                                  RawReliableServerTransport layer
    │       ├── Coverage.md
    │       ├── RawReliableServerTransportTests.cs
    │       ├── Harness/
    │       │   ├── ManualPeriodicLogicDriver.cs
    │       │   ├── RecordingRawAdmissionServerHandler.cs
    │       │   ├── ScriptedSerialActionQueue.cs
    │       │   ├── TestRawReliableServerSession.cs
    │       │   └── TestRawReliableServerTransport.cs
    │       └── RawReliableAdmissionServerTransport/ RawReliableAdmissionServerTransport layer
    │           ├── Coverage.md
    │           ├── RawReliableAdmissionServerTransportTests.cs
    │           └── Harness/
    │               ├── RecordingRawReliableAdmissionServerHandlerFactory.cs
    │               └── TestRawReliableAdmissionServerTransport.cs
    └── Unreliable/                                  RawUnreliableTransport layer
        ├── Coverage.md
        ├── RawUnreliableTransportTests.cs
        ├── Harness/
        │   ├── TestRawUnreliableTransport.cs
        │   └── TestRawUnreliableTransportWithoutDebugReliability.cs
        ├── Client/                                  RawUnreliableClientTransport layer
        │   ├── Coverage.md
        │   ├── RawUnreliableClientTransportTests.cs
        │   └── Harness/
        │       ├── StopDuringStartEndpoint.cs
        │       └── TestRawUnreliableClientTransport.cs
        └── Server/                                  RawUnreliableServerTransport layer
            ├── Coverage.md
            ├── RawUnreliableServerTransportTests.cs
            └── Harness/
                └── TestRawUnreliableServerTransport.cs
```

## Harness layout and sharing

Harness contains stubs, recording doubles, test implementations of abstract types, and manual
drivers.

- The root `Harness/` holds the root layer's `TestAnyTransport`.
- `Raw/Harness/` holds `TestRawTransport`.
- `Raw/Reliable/Harness/` holds `TestRawReliableTransport`.
- Client-specific harness stays in `Raw/Reliable/Client/Harness/`.
- Server-specific harness stays in `Raw/Reliable/Server/Harness/`. `ManualPeriodicLogicDriver`
  is here because it is shared by the server layer and its admission child.
- Reliable-specific harness stays in the corresponding reliable layer's `Harness/`. The
  `RawReliableEndpointClientTransport` layer reuses `TestRawReliableHandshakeClientEndpoint` and
  `RecordingRawReliableHandshakeClientHandler`
  from `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/RawReliableHandshakeClientEndpoint/Harness`; the
  `RawReliableAdmissionServerTransport` layer
  reuses `TestRawReliableServerSession` from the server layer's `Harness/`. The endpoint-client layer
  also keeps its own `FakeRawReliableClientEndpoint` for the failed-start paths; it is local because
  those tests need a deliberately failing endpoint.
- The client layer reuses the endpoint double from
  `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/Harness`. Both suites live in the same test
  assembly and the endpoint double is covered by the `Endpoints` suite.
- `Raw/Unreliable/Harness/` holds `TestRawUnreliableTransport`.
- `Raw/Unreliable/Client/Harness/` holds `TestRawUnreliableClientTransport` and the dedicated
  `StopDuringStartEndpoint`. The client layer also reuses `TestRawUnreliableEndpoint`,
  `RecordingRawUnreliableHandler`, `RecordingLogConsumer`, and `TestBuffers` from
  `Tests/Pontifex.Tests/Endpoints/RawUnreliable/Harness` and `Tests/Pontifex.Tests/Endpoints/Harness`.
  Both suites live in the same test assembly and those doubles are covered by the `Endpoints` suite;
  they are not copied here.
- `Raw/Unreliable/Server/Harness/` holds `TestRawUnreliableServerTransport`, which builds
  `TestRawUnreliableServerEndpoint` sessions from the `Endpoints` suite and reuses the same recording
  handler and log consumer.

## Coverage

Coverage is recorded per layer, next to the layer's tests:

- [Root `Coverage.md`](Coverage.md) — `AnyTransport`
- [`Raw/Coverage.md`](Raw/Coverage.md) — `RawTransport`
- [`Raw/Reliable/Coverage.md`](Raw/Reliable/Coverage.md) — `RawReliableTransport`
- [`Raw/Reliable/Client/Coverage.md`](Raw/Reliable/Client/Coverage.md) — `RawReliableClientTransport`
- [`Raw/Reliable/Server/Coverage.md`](Raw/Reliable/Server/Coverage.md) — `RawReliableServerTransport`
- [`Raw/Reliable/Client/RawReliableEndpointClientTransport/Coverage.md`](Raw/Reliable/Client/RawReliableEndpointClientTransport/Coverage.md) — `RawReliableEndpointClientTransport`
- [`Raw/Reliable/Server/RawReliableAdmissionServerTransport/Coverage.md`](Raw/Reliable/Server/RawReliableAdmissionServerTransport/Coverage.md) — `RawReliableAdmissionServerTransport`
- [`Raw/Unreliable/Coverage.md`](Raw/Unreliable/Coverage.md) — `RawUnreliableTransport`
- [`Raw/Unreliable/Client/Coverage.md`](Raw/Unreliable/Client/Coverage.md) — `RawUnreliableClientTransport`
- [`Raw/Unreliable/Server/Coverage.md`](Raw/Unreliable/Server/Coverage.md) — `RawUnreliableServerTransport`

Each layer covers only the contract delta and invariants introduced by its subject. Ancestor
behaviour is not repeated. A layer's `Coverage.md` lists the current covered tests, planned
gaps, and deferred-by-design gaps for that layer.

## Categories

- Suite root category: `Transports`. Every fixture carries it.
- Additional filtering categories: `Transports.Concurrency`.

`Transports.Concurrency` marks the server-layer tests that use real threads. It covers the four
gated tests that prove the single-factory-call invariant of the admission lock and of the
handler-factory lock, and the one bounded parallel test that proves the lock-free admission
counters never exceed their limits. The gated threads are controlled by gates, joined with bounded
timeouts, and the gates are always released in `finally`; no test sleeps or polls. The bound test
completes synchronously and asserts an invariant, not a scheduling order. The tests still run in
the default suite because they are deterministic and fast. Any future non-default facet
(real I/O, load, timing stress) must go into a separate layer or group with its own declared
category, not the default suite run.

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
- **Cleanup hooks**: a valid stop before any start attempt runs `OnStopBeforeStart` on every such
  call; a running stop, a stop that wins inside `TryStart`, and a stop on an invalid transport do
  not. `OnStartFailed` runs exactly once after a plain failed `TryStart`, after a stop that won
  inside `TryStart`, or after a `Fail` raised inside it; a start rejected because the transport was
  already started or stopped, and a retry after a failed start, do not run it again.
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

The client transport owns a small lifecycle (`Constructed`, `Initialized`, `Connecting`,
`Disconnected`) plus the endpoint hand-off. It keeps no connected state of its own: connectivity
and connection callbacks belong to the attached endpoint. This layer covers that delta; the
inherited `AnyTransport` lifecycle and the `RawTransport`/`RawReliableTransport` facades are not
repeated.

- **Init**: from `Constructed`, `Init` runs `SetupHandler`, stores the returned handler, and moves
  to `Initialized`; a null handler, a null `SetupHandler` result, a wrong state, or an already
  started transport fails and invalidates. A second `Init` fails but keeps the first handler.
  `Init` after a failed start does not call `SetupHandler`; `Init` while connecting fails without
  stopping the transport or notifying the handler. The transport never calls `OnDisconnected`.
- **Start / connect**: `Start` works only from `Initialized`; it calls `BeginConnect` and, while
  the state is still `Initialized`, moves to `Connecting`. A `false` from `BeginConnect` fails and
  destroys, and does **not** notify the handler `OnStopped`; a `BeginConnect` that stops the
  transport synchronously makes `Start` return `false` and likewise notifies no handler.
- **Connection ownership**: a `BeginConnect` that returns `true` must attach exactly one endpoint
  through `AttachEndpoint`. The endpoint is the single source of truth for connectivity and delivers
  `OnConnected`, `OnDisconnected`, and `OnStopped` to the user handler once the transport enters
  `Connecting`; the transport keeps no connected state of its own and offers no
  connection-completion hook. A `BeginConnect` that fails destroys, detaches, and does not notify
  the handler, even when it already attached an endpoint; the transport is not left
  `Initialized`.
- **IsConnected**: mirrors the attached endpoint and is `false` when no endpoint is attached.
  Without an endpoint it stays `false` after `Start`; with an attached endpoint it follows that
  endpoint, so it can be `true` inside `DestroyTransport` until the base detaches.
- **Endpoint attach / detach ownership**: `AttachEndpoint` rejects `null` and a second endpoint
  (the first stays attached); an attached endpoint's terminal `Stopped` event stops the transport
  with the same reason instance. The base class owns detachment: `DestroyTransport` can still read
  `AttachedEndpoint`, and after `Stop` completes the exposed attached endpoint is `null`.
  `DetachEndpoint` is **private**; implementations must not detach.
- **Stop notification matrix**: stopping after `Init` but before `Start` is a no-op that keeps the
  `Initialized` state and still allows a later `Start`. While `Connecting` the order is
  `handler.OnStopped`, `DestroyTransport`, detach, start callback when no endpoint is attached;
  when an endpoint is attached the transport skips its own notification and the endpoint owns it.
  While `Initialized` it is `DestroyTransport`, detach, and no handler `OnStopped` — this also
  covers a failed `BeginConnect` that already attached an endpoint. The transport never calls
  `OnDisconnected`; endpoint-owned transports deliver both connection callbacks from the endpoint.
- **Handler exception isolation**: exceptions from `OnStopped` are caught, logged, and swallowed at
  this layer; `DestroyTransport`, detach, and the start callback still run, and the transport ends in
  the disconnected state.

### `RawReliableEndpointClientTransport<TClientHandler>` contract matrix

The endpoint client transport supplies the endpoint factory and the carrier-independent teardown
around the inherited reliable client lifecycle. Its delta is the creation/start hand-off and the
failed-start teardown; the inherited init, connection state machine, and handler-notification rules
are covered by the client layer and not repeated.

- **Facade**: `Type` is `TransportType.RawReliable`, and the transport is assignable to
  `IRawReliableClient`.
- **Successful connect**: `BeginConnect` reads the initialized handler, calls `CreateEndpoint`, attaches
  the returned endpoint, then calls `endpoint.Start`. The attached endpoint owns connectivity and the
  handler's connection callbacks.
- **Factory failure**: a throwing `CreateEndpoint` is logged and `BeginConnect` returns `false`. No
  endpoint is attached, the transport start fails and invalidates, and neither the handler nor the
  start callback is notified.
- **Endpoint start failure**: an endpoint that returns `false` or throws from `Start` is already
  attached. The inherited failed-start path invalidates the transport, calls `DestroyTransport`, which
  stops the attached endpoint exactly once with the failure reason, then detaches it. The handler is not
  notified because the start attempt never handed the lifecycle to a connected endpoint.

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
  still allows a later start. `Fail` runs the same stop delta and invalidates the transport: the carrier and
  every live route are stopped with the failure reason, each handler observes it once, and the start callback
  is invoked because the start had succeeded.
- **Ticker**: the ticker is created through the `LogicDriverFactory` seam and started exactly once
  after a successful carrier start. Each tick calls `PeriodicTick` on every registered session; a
  stopped session is skipped, a session added after start is picked up, and an exception from one
  session does not prevent the others from ticking. Ticks after the transport stops are ignored, and
  no ticker is created when the start fails or when a carrier overrides
  `DisconnectionByTimeoutCheck` to `false` (carriers without an idle-session timeout). The test
  double supplies a hand-driven `ILogicDriver<IPeriodicLogicDriverCtl>`, so no background thread or
  real time is involved.

### `RawReliableAdmissionServerTransport` contract matrix

The admission transport adds the client handshake and the factory-backed session hand-off on top of
the reliable server transport. Its delta is the handshake validation and admission outcome; the
inherited session admission, registry, stop cascade, and ticker are covered by the server layer and
not repeated.

- **Constructor**: `handshakeName` is required; `handshakeMaxByteSize` must be at least 1;
  `handshakeTimeout` must be positive or `Timeout.InfiniteTimeSpan`; `rejectLinger` must not be
  negative. Each rejection uses the matching parameter name. The defaults are a 10-second handshake
  timeout, a 2-second reject linger, and the protocol default frame cap (128 bytes).
- **Handshake structure**: a request is `version (short) + name (string) + messageMaxByteSize (int)`.
  An empty list, a wrong element count, or wrong element types is rejected before any factory call.
- **Handshake validation order**: structure, then version, then name, then capacity, then transport
  liveness and factory presence. A version mismatch reports `VersionMismatch`; a name mismatch
  reports `NameMismatch`; a request maximum below 1 or below `HandshakeMaxByteSize` reports
  `CapacityExceeded`. None of these paths calls the factory.
- **Admission prerequisite**: a handshake requires a valid, started transport and a bound factory.
  A handshake before start, after stop, without a factory, or with a null factory result is
  rejected. A throwing factory is logged and treated as a rejection.
- **Accepted handshake**: the factory's handler is wrapped in `StopOnceServerHandler`; the response
  is built with the shared version and name and with `AgreedMaxByteSize =
  min(request maximum, MessageMaxByteSize)`. A response-build failure is logged, the created
  handler receives one `OnStopped` with an `ExceptionFail`, and the outcome is a rejection with no
  response.

### `RawUnreliableTransport` contract matrix

`RawUnreliableTransport` is a thin marker facade over `RawTransport`, like the reliable marker, and adds
no lifecycle or carrier behavior. It exposes the unreliable transport contract.

- **Interface exposure**: an instance is assignable to `IRawUnreliableTransport` and `IRawTransport`.
- **Conformance control**: `GetControls` publishes one `RawUnreliableTransportConformanceControl`, and the
  typed `Conformance` property returns the same instance through
  `IRawUnreliableTransportConformanceControl`.
- **Debug reliability**: the control's `TryMakeReliable` forwards to
  `IRawUnreliableDebugReliability.TryMakeReliableForDebug` on the transport and returns its result. An
  owner that does not implement the interface is declined with `false`.

### `RawUnreliableClientTransport` contract matrix

The client transport owns a small lifecycle (`Constructed`, `Initialized`, `Started`, `Stopped`), the
carrier hooks, and the hand-off to one endpoint. It tracks no connectivity of its own. This layer covers
that delta; the inherited `AnyTransport` lifecycle and the unreliable marker facade are not repeated.

- **Init**: from `Constructed`, `Init` binds the handler and moves to `Initialized`; the call is one-shot
  and later calls return `false` without invalidating the transport. A null handler throws
  `ArgumentNullException` and leaves the transport untouched. `Init` after a start attempt (successful or
  not) returns `false` and does not bind the handler.
- **Start**: works only after `Init`. The sequence is `StartCarrier`, `CreateEndpoint`, attachment
  (subscription to the endpoint's terminal notification), then `endpoint.Start`; a second `Start` does
  nothing. A rejected or throwing `StartCarrier` fails the start, stops the carrier once, and — because no
  endpoint exists — notifies the handler with a `TextFail` or `ExceptionFail`. A rejected or throwing
  `endpoint.Start` fails the start and stops the carrier once, but the attached endpoint owns the handler
  notification and, because that endpoint never raised `OnStarted`, the handler receives nothing. No failed
  initial start invokes the user start callback.
- **Endpoint factory failure**: a throwing `CreateEndpoint` is logged, the handler receives an
  `ExceptionFail` carrying the exception, `StopCarrier` runs once, and `Start` returns `false`. This is the
  reachable "no endpoint created" fallback path: the transport owns the handler stop notification exactly
  while no endpoint exists.
- **Start/stop race**: an endpoint terminal notification observed while the start is still in progress is
  latched (the first reason only) and replayed from `OnStarted`. The transport is never stopped from inside
  `TryStart`, so `Start` returns `true` and the transport ends stopped with the endpoint's reason. The
  endpoint's own handler gate decides whether the handler sees `OnStopped`.
- **Endpoint terminal notification**: after a successful start, the attached endpoint stopping stops the
  transport with the same reason instance. The transport detaches the endpoint and does not duplicate the
  handler notification: the endpoint owns it.
- **Stop ordering**: `Stop` asks the attached endpoint to stop through the one-shot
  `IRawUnreliableEndpoint.Stop`, then calls `StopCarrier`, then detaches. The endpoint comes first because
  it owns the carrier resources. A second one-shot endpoint stop inside `StopCarrier` therefore returns
  `false`.

### `RawUnreliableServerTransport<TFactory>` contract matrix

The server transport owns the carrier hooks, the admission lock, the source-route registry, and the shared
send budget. This layer covers that delta; the inherited `AnyTransport` lifecycle and the unreliable marker
facade are not repeated.

- **Initialization**: `TryInitializeServer` binds the variant factory once. Later calls, calls after a start
  attempt, and calls on an invalid transport return `false` and keep the first factory. A null factory is
  accepted and makes every new route a silent rejection.
- **Start**: requires initialization. It creates the shared `SendBudget` from `SendBudgetBytes` and
  `SendBudgetMessages`, then starts the carrier. A rejected or throwing carrier start drops the budget,
  returns `false`, and does not call `StopCarrier`. The default limits are
  `MessageMaxByteSize * 100` bytes and 100 messages; carriers override the two properties to derive the
  caps from their queue capacity and configure each endpoint's local budget with the byte cap.
- **Admission**: `OnCarrierInbound` requires a non-null source and trigger. A message whose data size exceeds
  `MessageMaxByteSize` is discarded before the admission lock: it calls neither the factory nor a handler,
  publishes or refreshes no route, releases the trigger, and leaves the transport started. A message for an
  existing route
  is delivered to that endpoint outside the lock. For an unknown route, one admission lock serializes the
  `BeforeHandlerFactoryGate`, factory invocation, endpoint construction, and route publication. A null
  factory, a null handler, or a throwing factory releases the trigger exactly once, publishes no route, and
  logs only the exception. A throwing endpoint constructor behaves the same.
- **Creation order**: factory, endpoint construction with the trigger as its first inbox element, route
  publication, `BeforeHandlerStartedGate`, then `endpoint.Start`. After the lock is released the transport
  re-checks `IsStopping`/`IsStarted`; if it has started stopping, the route is removed and the endpoint is
  stopped unstarted, which releases the trigger without ever raising `OnStarted`.
- **Route removal**: a session endpoint's terminal notification removes only its own route, compared by
  reference. A stopped route can be recreated by the next trigger for the same source; other routes are
  unaffected.
- **Stop**: the admission is closed and the routes are cleared under the admission lock before
  `StopCarrier` runs. The carrier stops first, then every removed endpoint is stopped with the transport
  reason through the one-shot entry. Every session handler observes the same reason instance.
- **Concurrency**: factory invocation and route publication are serialized globally, across all source
  routes. Concurrent triggers for the same unknown source produce exactly one factory call, one endpoint,
  and one route; concurrent triggers for different sources still never overlap in the factory.

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
  `TryStart` result or supply behavior for the hooks. It also overrides the `OnStopBeforeStart` and
  `OnStartFailed` cleanup hooks and records their counts and event names.
- `TestRawTransport : RawTransport` supplies the abstract `MessageMaxByteSize` and `Type` values
  and implements the inherited lifecycle hooks with no additional behavior.
- `TestRawReliableTransport : RawReliableTransport` supplies the inherited abstract members and a
  trivial `RawReliableConformanceControl` constructor argument only.
- `TestRawReliableClientTransport : RawReliableClientTransport<IRawReliableClientHandler>` supplies
  the abstract `Type`, `MessageMaxByteSize`, `BeginConnect`, and `DestroyTransport` members. It
  records hook calls, lets each test choose the `BeginConnect`
  result and the `SetupHandler` transformation, exposes `AttachedEndpoint` and `AttachEndpoint`, and
  records the endpoint and `IsConnected` observed inside `DestroyTransport`.
- `RecordingRawReliableClientHandler : IRawReliableClientHandler, IDisposable` records
  `Received`/`Disconnected`/`Stopped` counts and the stop reason, can throw a configured exception
  from `OnStopped`, and optionally appends callback names to the shared event log. `OnReceived`
  does not release the delivered buffer; the test owns it (buffers still alive at `Dispose` are
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
- `TestRawUnreliableTransport : RawUnreliableTransport, IRawUnreliableDebugReliability` supplies the
  inherited abstract members, exposes the typed conformance control, and records the debug-reliability
  call.
- `TestRawUnreliableClientTransport : RawUnreliableClientTransport` supplies the carrier hooks and the
  endpoint factory, records their calls and stop ordering, and lets a test make the factory or the
  carrier fail. It reuses the endpoint-suite recording handler and manual-queue endpoint.
- `StopDuringStartEndpoint : RawUnreliableEndpoint` uses the production inline queue and stops itself in
  an action scheduled after its own flush, so its terminal notification arrives synchronously inside
  `endpoint.Start()` while the client transport's start is in progress.
- `TestRawUnreliableServerTransport : RawUnreliableServerTransport<Func<IEndPoint, UnionDataList,
  IRawUnreliableHandler?>>` implements the carrier hooks, the factory, and the endpoint constructor,
  records what the transport hands it, and exposes the admission entry point and the route count.
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
- **Do not use the conformance subsystem as a test mechanism.** Do not arm gates, and do not call
  `FailNextStart`/`InjectUnrecoverableFailure`. Do not inject a conformance control into a transport
  double. The single exception is the `RawUnreliableTransport` root layer, which asserts that
  `GetControls` publishes the transport's conformance control and that `TryMakeReliable` delegates to
  `IRawUnreliableDebugReliability`; it neither arms nor injects.
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

Filter to the concurrency-tagged tests:

```
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports.Concurrency
```

## Out of scope

- Endpoint behavior: `RawReliableEndpoint` and the raw-reliable endpoint family are covered by
  the `Endpoints` suite.
- `RawUnreliableEndpoint`, `RawUnreliableServerEndpoint`, and the endpoint-owned architecture of the
  unreliable path: the pre-start inbox, the one-shot stop, the local and shared budgets, and the carrier
  resource ownership are covered by the `Endpoints` suite.
- Carrier behavior: delivery, ordering, socket handling, and end-to-end topology behavior belong
  to the conformance suite in `Tests/Pontifex.Transports.Tests`.
- Concrete carrier transports in the `Transports` projects (Direct, TCP, UDP).
- Arming the conformance subsystem (gates and failure injection). The unreliable root layer only
  asserts that the control object is published and that the debug-reliability lookup is preferred.
- The public `Stopped` event on transports, where it is raised independently of the handler
  callbacks.
- Real time, threads, timers, and wall-clock scheduling, except the gated, joined, deterministic
  `Transports.Concurrency` test.

## Rules

This suite follows [Tests/RULES.md](../../RULES.md).
