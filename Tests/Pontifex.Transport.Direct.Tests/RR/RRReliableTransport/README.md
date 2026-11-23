# RRReliable Transport Abstract Test Suite

## Rationale

`RRReliableTransport` is the reusable lifecycle base of the RR reliable family:
carrier start and stop, payload validation, server-global id allocation, the
frame-size ceiling, and the injectable TTL timer seam.
`RRReliableTransport<THandler>` adds one-time `Init` and the dispatch gate that
hides the server handler until `Start` succeeds. The suite pins that contract
before the client and server layers are added.

## Suite type

**Abstract Test Suit.** Every subject is abstract, so the suite is one layer per
abstract type.

## Root subject

`Pontifex.RR.Reliable.RRReliableTransport`.

Selected inheritance tree:

```text
RRReliableTransport
└── RRReliableTransport<THandler>
    ├── RRReliableClientTransport     (planned, client layer)
    └── RRReliableServerTransport     (planned, server layer)
```

The two planned concrete branches belong to the later core-behaviour stage. The
client layer is recorded as planned in the root `Coverage.md`; the server layer
is recorded as planned in the generic layer `Coverage.md`.

## Contract references

- [RRReliable specification](../../../../Pontifex/Abstractions/Transports/RR/Reliable/Specification.md)
  — sections 5, 6.3, 7, and 15.
- [Shared RR specification](../../../../Pontifex/Abstractions/Transports/RR/Specification.md)
  — sections 7 and 11.
- [RRReliableTransport.cs](../../../../Pontifex/Implementations/RR/Reliable/RRReliableTransport.cs)
- [IRRReliableTransport.cs](../../../../Pontifex/Abstractions/Transports/RR/Reliable/IRRReliableTransport.cs)
- [IRRReliableTimerFactory.cs](../../../../Pontifex/Abstractions/Transports/RR/Reliable/IRRReliableTimerFactory.cs)

## Layers

The layer table is the authoritative declaration of layer name, subject,
directory, inheritance parent, and coverage file.

| Layer | Subject | Directory | Inheritance parent | Coverage |
| --- | --- | --- | --- | --- |
| `RRReliableTransport` | `Pontifex.RR.Reliable.RRReliableTransport` | `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport` | none (root) | [`Coverage.md`](Coverage.md) |
| `RRReliableTransportGeneric` | `Pontifex.RR.Reliable.RRReliableTransport<THandler>` | `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport/Generic` | `RRReliableTransport` | [`Generic/Coverage.md`](Generic/Coverage.md) |

Directory nesting does not imply inheritance. The inheritance parent is the one
declared in the table.

## Naming conventions

- Layer name = subject type name; the generic layer uses the `Generic` suffix
  because a file name cannot contain angle brackets.
- Test file = `<Subject>Tests.cs`; fixture class = `<Subject>Tests`.
- Test methods = `Scenario_ExpectedResult`, for example
  `Stop_SchedulesTerminalWorkBeforeOnStoppedBegins`.
- Namespace = `Pontifex.Tests.RR.RRReliableTransport` for every file, including
  harness files.
- Every fixture carries `[Category("Direct.RRReliableTransport")]`.

## Directory layout

```text
RR/RRReliableTransport/
  README.md
  Coverage.md                                        RRReliableTransport layer
  RRReliableTransportTests.cs
  Harness/
    TestRRReliableTransport.cs
  Generic/                                           RRReliableTransport<THandler> layer
    Coverage.md
    RRReliableTransportGenericTests.cs
    Harness/
      TestRRReliableTransportGeneric.cs
```

In an Abstract Test Suit, test files sit directly in their layer directory;
there is no `Tests/` folder.

## Harness layout

All harness types are hand-written and test-only:

- `Harness/TestRRReliableTransport.cs` is a concrete carrier-free root subject
  that records carrier start/stop/clear calls, can fail or throw on start, and
  exposes the protected frame-size ceiling, payload validation, id allocation,
  and timer factory for assertions.
- `Generic/Harness/TestRRReliableTransportGeneric.cs` is a concrete
  carrier-free generic subject that exposes `DispatchHandler` and records carrier
  calls; it also holds the empty `TestReliableHandler`.

Each harness belongs to its own layer. The generic layer does not reference the
root harness, and the root layer does not reference the generic harness.

There are no harness self-tests, so the `Direct.RRReliableTransport.Harness`
category is not used.

The layer also uses the loose
[`ManualTimerFactory`](../TestSupport/ManualTimerFactory.cs) shared with the
helpers suite.

## Coverage

Coverage is recorded per layer:

- [`Coverage.md`](Coverage.md) — `RRReliableTransport`
- [`Generic/Coverage.md`](Generic/Coverage.md) — `RRReliableTransport<THandler>`

## Categories and commands

- Suite root category: `Direct.RRReliableTransport`. Every fixture carries it.
- Harness category: not used (no harness self-tests).

```bash
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRReliableTransport"
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

- The client and server transport behaviour: the planned client and server
  layers of the later stage.
- Concrete carriers, bindings, and in-process Direct channels.
- Real timers and real time: the manual factory drives scheduling assertions.
- The frame codec, request state, and request table: the
  `Direct.RRReliableCoreHelpers` suite owns them.

## Rules

This suite follows [`Tests/RULES.md`](../../../RULES.md).
