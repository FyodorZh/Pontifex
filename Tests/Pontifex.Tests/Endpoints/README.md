# Endpoint Test Suite

Black-box contract tests for the abstract endpoint hierarchy rooted at
`Pontifex.Raw.RawEndpoint`. The suite tests endpoints directly: no transports, carriers,
sockets, threads, timers, or wall-clock time.

## Suite type

**Abstract Test Suit.** Every subject in the selected inheritance tree is abstract, so the suite
is structured as one layer per abstract type rather than one Test Suit per class.

Root subject: `Pontifex.Raw.RawEndpoint`.

Selected inheritance tree:

```
RawEndpoint
└── RawReliableEndpoint
    ├── RawReliableClientEndpoint
    │   └── RawReliableAckClientEndpoint
    └── RawReliableServerEndpoint
        └── RawReliableAckServerEndpoint
```

## Contract references

- [RawEndpoint.cs](../../../Pontifex/Implementations/Raw/RawEndpoint.cs)
- [IRawEndpoint.cs](../../../Pontifex/Abstractions/Transports/Raw/IRawEndpoint.cs)
- [IBaseEndpoint.cs](../../../Pontifex/Abstractions/Transports/IBaseEndpoint.cs)
- [IRawHandler.cs](../../../Pontifex/Abstractions/Transports/Raw/IRawHandler.cs)
- [RawReliableEndpoint.cs](../../../Pontifex/Implementations/Raw/Reliable/RawReliableEndpoint.cs)
- [IRawReliableEndpoint.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableEndpoint.cs)
- [RawReliableClientEndpoint.cs](../../../Pontifex/Implementations/Raw/Reliable/RawReliableClientEndpoint.cs)
- [RawReliableServerEndpoint.cs](../../../Pontifex/Implementations/Raw/Reliable/RawReliableServerEndpoint.cs)
- [RawReliableAckClientEndpoint.cs](../../../Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckClientEndpoint.cs)
- [IRawReliableAckClientHandler.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Client/IRawReliableAckClientHandler.cs)
- [RawReliableAckServerEndpoint.cs](../../../Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckServerEndpoint.cs)
- [IRawReliableAckServerHandler.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Server/IRawReliableAckServerHandler.cs)
- [RawReliableAckInfo.cs](../../../Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckInfo.cs)

## Layers

The layer table is the authoritative declaration of layer name, subject, directory, inheritance
parent, and coverage file.

| Layer | Subject | Directory | Inheritance parent | Coverage |
|---|---|---|---|---|
| `RawEndpoint` | `Pontifex.Raw.RawEndpoint` | `Tests/Pontifex.Tests/Endpoints` | — (root) | [`Coverage.md`](Coverage.md) |
| `RawReliableEndpoint` | `Pontifex.Raw.Reliable.RawReliableEndpoint` | `Tests/Pontifex.Tests/Endpoints/RawReliable` | `RawEndpoint` | [`RawReliable/Coverage.md`](RawReliable/Coverage.md) |
| `RawReliableClientEndpoint` | `Pontifex.Raw.Reliable.RawReliableClientEndpoint` | `Tests/Pontifex.Tests/Endpoints/RawReliable/Client` | `RawReliableEndpoint` | [`RawReliable/Client/Coverage.md`](RawReliable/Client/Coverage.md) |
| `RawReliableServerEndpoint` | `Pontifex.Raw.Reliable.RawReliableServerEndpoint` | `Tests/Pontifex.Tests/Endpoints/RawReliable/Server` | `RawReliableEndpoint` | [`RawReliable/Server/Coverage.md`](RawReliable/Server/Coverage.md) |
| `RawReliableAckClientEndpoint` | `Pontifex.Raw.Reliable.Ack.RawReliableAckClientEndpoint` | `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/Ack` | `RawReliableClientEndpoint` | [`RawReliable/Client/Ack/Coverage.md`](RawReliable/Client/Ack/Coverage.md) |
| `RawReliableAckServerEndpoint` | `Pontifex.Raw.Reliable.Ack.RawReliableAckServerEndpoint` | `Tests/Pontifex.Tests/Endpoints/RawReliable/Server/Ack` | `RawReliableServerEndpoint` | [`RawReliable/Server/Ack/Coverage.md`](RawReliable/Server/Ack/Coverage.md) |

## Naming conventions

- Layer name = subject type name.
- Test file = `<Subject>Tests.cs`; fixture class = `<Subject>Tests`.
- Test methods = `Scenario_ExpectedResult`, for example
  `Send_WhenNotStarted_ReturnsNotConnected`.
- Namespace = `Pontifex.Tests.Endpoints` for every file in the suite, including harness files.
- Every fixture carries `[Category("Endpoints")]`.

Directory nesting does not imply inheritance. The inheritance parent is the one declared in the
layer table above.

## Directory layout

```
Endpoints/
├── README.md
├── Coverage.md                              RawEndpoint layer
├── Harness/
│   ├── ManualSerialActionQueue.cs
│   ├── RecordingRawHandler.cs
│   ├── TestBuffers.cs
│   └── TestRawEndpoint.cs
├── RawEndpointTests.cs
└── RawReliable/
    ├── Coverage.md                          RawReliableEndpoint layer
    ├── Harness/
    │   ├── RecordingRawReliableHandler.cs
    │   └── TestRawReliableEndpoint.cs
    ├── RawReliableEndpointTests.cs
    ├── Client/
    │   ├── Coverage.md                      RawReliableClientEndpoint layer
    │   ├── Harness/
    │   │   ├── RecordingRawReliableClientHandler.cs
    │   │   └── TestRawReliableClientEndpoint.cs
    │   ├── RawReliableClientEndpointTests.cs
    │   └── Ack/
    │       ├── Coverage.md                  RawReliableAckClientEndpoint layer
    │       ├── Harness/
    │       │   ├── RecordingRawReliableAckClientHandler.cs
    │       │   └── TestRawReliableAckClientEndpoint.cs
    │       └── RawReliableAckClientEndpointTests.cs
    └── Server/
        ├── Coverage.md                      RawReliableServerEndpoint layer
        ├── Harness/
        │   ├── RecordingRawReliableServerHandler.cs
        │   └── TestRawReliableServerEndpoint.cs
        ├── RawReliableServerEndpointTests.cs
        └── Ack/
            ├── Coverage.md                  RawReliableAckServerEndpoint layer
            ├── Harness/
            │   ├── RecordingRawReliableAckServerHandler.cs
            │   └── TestRawReliableAckServerEndpoint.cs
            └── RawReliableAckServerEndpointTests.cs
```

## Harness layout and sharing

Harness contains stubs, recording doubles, test implementations of abstract types, and manual
drivers.

- The root `Harness/` holds the suite-wide doubles (`ManualSerialActionQueue`,
  `RecordingRawHandler`, and `TestBuffers`) and the root layer's `TestRawEndpoint`.
- `RawReliable/Harness/` holds the layer's `TestRawReliableEndpoint` and the handler double
  `RecordingRawReliableHandler`, which is shared by both client/server branches.
- Client-specific harness stays in `RawReliable/Client/Harness/`; server-specific harness stays
  in `RawReliable/Server/Harness/`.
- Ack-specific harness stays in the corresponding ack layer's `Harness/`.

## Coverage

Coverage is recorded per layer, next to the layer's tests:

- [Root `Coverage.md`](Coverage.md) — `RawEndpoint`
- [`RawReliable/Coverage.md`](RawReliable/Coverage.md) — `RawReliableEndpoint`
- [`RawReliable/Client/Coverage.md`](RawReliable/Client/Coverage.md) — `RawReliableClientEndpoint`
- [`RawReliable/Server/Coverage.md`](RawReliable/Server/Coverage.md) — `RawReliableServerEndpoint`
- [`RawReliable/Client/Ack/Coverage.md`](RawReliable/Client/Ack/Coverage.md) — `RawReliableAckClientEndpoint`
- [`RawReliable/Server/Ack/Coverage.md`](RawReliable/Server/Ack/Coverage.md) — `RawReliableAckServerEndpoint`

Each layer covers only the contract delta and invariants introduced by its subject. Ancestor
behaviour is not repeated. A layer's `Coverage.md` lists the current covered tests, planned gaps,
and deferred-by-design gaps for that layer.

## Categories

- Suite root category: `Endpoints`. Every fixture carries it.
- Additional filtering categories: none currently.

Any non-default facet (real I/O, load, concurrency stress, timing) must go into a separate layer
or group with its own declared category, not the default suite run.

## Running

Run the whole test project:

```
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj
```

Filter to this suite:

```
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Endpoints
```

## Adding a layer

1. Ensure the parent layer already exists.
2. Create the layer directory and add `Coverage.md` with layer identity, subject, directory,
   inheritance parent, contract link, covered tests, planned gaps, and deferred-by-design gaps.
3. Add `Harness/` only if the layer needs new doubles; reuse ancestor harness where possible.
4. Add `<Subject>Tests.cs` with `[Category("Endpoints")]`.
5. Add the layer row to the table and the directory layout in this README.
6. Run the suite and record it in `Coverage.md`.

## Adding tests

1. Put the test in the layer that owns the behaviour (the subject's delta), not in an ancestor
   or sibling layer.
2. Use the public and protected contract.
3. Drive time, queues, and completion explicitly through the harness seams.
4. Assert return values, handler callbacks and reasons, captured buffers, ordering, counts, and
   contract-visible state.
5. Record the test in the owning layer's `Coverage.md`.

## Out of scope

- Transports, carriers, sockets, networks, threads, timers, and real time. Those belong to the
  transport suite in `Tests/Pontifex.Tests/Transports` and the conformance project
  `Tests/Pontifex.Transports.Tests`.
- `RawUnreliableEndpoint`, which is a separate concrete hierarchy, not a descendant of
  `RawEndpoint`.
- Wrapper endpoints (`RawAckBaseEndpointWrapper` and its descendants) and converter endpoints.
- The conformance subsystem: `IConformanceControl`, gates, failure injection, and the
  conformance role of `GetControls`.
- The background `Task.Run`/`Task.Delay` keep-alive loop wrapper. The synchronous
  `KeepAliveTick` body is covered; the scheduling wrapper is not.
- End-to-end reliability, ordering, delivery, and reconnection behaviour.

## Rules

This suite follows [Tests/RULES.md](../../RULES.md).
