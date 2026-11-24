# RRReliable Transport Abstract Test Suite

## Rationale

`Pontifex.RR.RRTransport` is the shared lifecycle base of both RR families:
carrier start and stop, payload validation, server-global id allocation, and the
frame-size formula.
`RRReliableTransport` adds the reliable transport type, the largest reliable
header, and the injectable TTL timer seam.
`RRReliableTransport<THandler>` adds one-time `Init` and the dispatch gate that
hides the server handler until `Start` succeeds.
`RRReliableClientTransport` adds the request lifecycle, ordered responses,
cancellation, TTLs, and the serialized callback pump.
`RRReliableServerTransport` adds per-client sessions, duplicate suppression,
response handles, the serialized `OnRequested` dispatch, and session closure.

## Suite type

**Abstract Test Suit.** Every subject is abstract, so the suite is one layer per
abstract type.

## Root subject

`Pontifex.RR.Reliable.RRReliableTransport`.

Selected inheritance tree:

```text
RRTransport  (shared with the unreliable family, outside the suite)
└── RRReliableTransport
    ├── RRReliableClientTransport         (client layer)
    └── RRReliableTransport<THandler>
        └── RRReliableServerTransport     (server layer)
```

`RRTransport` belongs to `Pontifex.RR` and is shared with the unreliable family,
so it lies outside the selected tree, like `AnyTransport`. The root layer keeps
the inherited lifecycle behaviour in scope and exercises it through its concrete
reliable subject.

The client layer declares the root as its inheritance parent; the server layer
declares the generic layer as its inheritance parent. The server layer is placed
beside the generic layer; directory nesting does not imply inheritance.

## Contract references

- [RRReliable specification](../../../../Pontifex/Abstractions/Transports/RR/Reliable/Specification.md)
  — sections 5, 6.3, 7, and 15.
- [Shared RR specification](../../../../Pontifex/Abstractions/Transports/RR/Specification.md)
  — sections 7 and 11.
- [RRTransport.cs](../../../../Pontifex/Implementations/RR/RRTransport.cs)
- [RRReliableTransport.cs](../../../../Pontifex/Implementations/RR/Reliable/RRReliableTransport.cs)
- [RRReliableClientTransport.cs](../../../../Pontifex/Implementations/RR/Reliable/RRReliableClientTransport.cs)
- [RRReliableServerTransport.cs](../../../../Pontifex/Implementations/RR/Reliable/RRReliableServerTransport.cs)
- [RRReliableResponse.cs](../../../../Pontifex/Implementations/RR/Reliable/RRReliableResponse.cs)
- [IRRReliableTransport.cs](../../../../Pontifex/Abstractions/Transports/RR/Reliable/IRRReliableTransport.cs)
- [IRRReliableTimerFactory.cs](../../../../Pontifex/Abstractions/Transports/RR/Reliable/IRRReliableTimerFactory.cs)

## Layers

The layer table is the authoritative declaration of layer name, subject,
directory, inheritance parent, and coverage file.

| Layer | Subject | Directory | Inheritance parent | Coverage |
| --- | --- | --- | --- | --- |
| `RRReliableTransport` | `Pontifex.RR.Reliable.RRReliableTransport` | `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport` | none (suite root); derives from `RRTransport` (outside the suite) | [`Coverage.md`](Coverage.md) |
| `RRReliableTransportGeneric` | `Pontifex.RR.Reliable.RRReliableTransport<THandler>` | `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport/Generic` | `RRReliableTransport` | [`Generic/Coverage.md`](Generic/Coverage.md) |
| `RRReliableClientTransport` | `Pontifex.RR.Reliable.RRReliableClientTransport` | `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport/Client` | `RRReliableTransport` | [`Client/Coverage.md`](Client/Coverage.md) |
| `RRReliableServerTransport` | `Pontifex.RR.Reliable.RRReliableServerTransport` | `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport/Server` | `RRReliableTransport<THandler>` | [`Server/Coverage.md`](Server/Coverage.md) |

Directory nesting does not imply inheritance. The inheritance parent is the one
declared in the table.

## Naming conventions

- Layer name = subject type name; the generic layer uses the `Generic` suffix
  because a file name cannot contain angle brackets.
- Test file = `<Subject>Tests.cs`; fixture class = `<Subject>Tests`.
- Test methods = `Scenario_ExpectedResult`, for example
  `Stop_SchedulesTerminalWorkBeforeOnStoppedBegins`.
- Test namespace = `Pontifex.Tests.RR.RRReliableTransport`; harness namespaces
  are `Pontifex.Tests.RR.RRReliableTransport.<Layer>.Harness`, so siblings do not
  share a harness namespace.
- Every fixture carries `[Category("Direct.RRReliableTransport")]`; the client
  and server layers add `Direct.RRReliableTransport.Client` and
  `Direct.RRReliableTransport.Server` respectively.

## Directory layout

```text
RR/RRReliableTransport/
  README.md
  Coverage.md                                        RRReliableTransport layer
  RRReliableTransportTests.cs
  Harness/
    TestRRReliableTransport.cs
    ScriptedReliableCarrier.cs
  Generic/                                           RRReliableTransport<THandler> layer
    Coverage.md
    RRReliableTransportGenericTests.cs
    Harness/
      TestRRReliableTransportGeneric.cs
  Client/                                            RRReliableClientTransport layer
    Coverage.md
    RRReliableClientTransportTests.cs
    Harness/
      TestReliableClientTransport.cs
  Server/                                            RRReliableServerTransport layer
    Coverage.md
    RRReliableServerTransportTests.cs
    Harness/
      TestReliableServerTransport.cs
```

In an Abstract Test Suit, test files sit directly in their layer directory;
there is no `Tests/` folder.

## Harness layout

All harness types are hand-written and test-only:

- `Harness/TestRRReliableTransport.cs` is a concrete carrier-free root subject
  that records carrier start/stop/clear calls, can fail or throw on start, and
  exposes the inherited frame-size ceiling, payload validation, and id
  allocation plus the timer factory for assertions.
- `Generic/Harness/TestRRReliableTransportGeneric.cs` is a concrete
  carrier-free generic subject that exposes `DispatchHandler` and records carrier
  calls; it also holds the empty `TestReliableHandler`.
- `Harness/ScriptedReliableCarrier.cs` is the deterministic scripted carrier
  shared by the client and server layers. It records and queues frames, exposes
  optional per-direction send overrides, and releases undelivered frames. It
  lives in the root layer because that is the nearest common ancestor of the two
  sibling layers; neither sibling references the other's harness.
- `Client/Harness/TestReliableClientTransport.cs` is the concrete client subject
  and its recording request handler.
- `Server/Harness/TestReliableServerTransport.cs` is the concrete server subject
  and its recording server handler.

Each harness belongs to its own layer. The generic layer does not reference the
root harness, and the root layer does not reference the generic harness. The
client and server layers reference only the root scripted carrier and their own
harness.

There are no harness self-tests, so the `Direct.RRReliableTransport.Harness`
category is not used.

The suite also uses the loose
[`ManualTimerFactory`](../TestSupport/ManualTimerFactory.cs) shared with the
helpers suite.

## Coverage

Coverage is recorded per layer:

- [`Coverage.md`](Coverage.md) — `RRReliableTransport`
- [`Generic/Coverage.md`](Generic/Coverage.md) — `RRReliableTransport<THandler>`
- [`Client/Coverage.md`](Client/Coverage.md) — `RRReliableClientTransport`
- [`Server/Coverage.md`](Server/Coverage.md) — `RRReliableServerTransport`

## Categories and commands

- Suite root category: `Direct.RRReliableTransport`. Every fixture carries it.
- Additional client filter: `Direct.RRReliableTransport.Client`.
- Additional server filter: `Direct.RRReliableTransport.Server`.
- Harness category: not used (no harness self-tests).

```bash
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRReliableTransport"
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRReliableTransport.Client"
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRReliableTransport.Server"
```

## Adding a layer

Add the layer after its parent exists: create the directory, `Coverage.md`, one
or more `<Subject>Tests.cs` files directly in the layer, an optional `Harness/`,
and a row in the layer table above. A concrete type belongs to a Test Suit, not
to this suite.

## Adding tests

Add a test to the file of its layer. It must cover only the delta the layer's
subject introduces, must carry the suite root category, and must be recorded in
that layer's `Coverage.md` in the same change.

## Out of scope

- Concrete carriers, bindings, and in-process Direct channels.
- Real timers and real time: the manual factory drives scheduling assertions.
- The frame codec, request state, and request table: the
  `Direct.RRReliableCoreHelpers` suite owns them.
- A live client-server round trip: the Direct carrier integration belongs to the
  later Direct stage.

## Rules

This suite follows [`Tests/RULES.md`](../../../RULES.md).

## Default and integration runs

The default Direct run excludes every fixture that carries an `*.Integration`
category. The integration run executes only those fixtures.

```bash
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory!~Integration"
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory~Integration"
```

Integration categories in use: `Direct.RRReliable.Integration`,
`Direct.RRUnreliable.Integration`, `Direct.RawUnreliable.Integration`, and
`Direct.Integration`.
