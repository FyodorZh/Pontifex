# RRReliable Helpers Test Suit

## Rationale

The reusable RR reliable core is built from a frame codec, a lock-protected
request table, a per-request state machine, and a TTL timer seam. These types
are internal and tightly coupled: the state machine consumes the frame kinds, the
table stores the state, and the timer factory drives both TTLs. The suite pins
their wire encoding, transitions, buffering, and ownership before the client and
server transports are built on them.

## Suite type

**Test Suit.** The subject is one small, tightly coupled group of non-abstract
types: `RRReliableFrame`, `RRReliableRequestState`, `RRReliableRequestTable`,
and `RRReliableServerSession`.

## Root subject

- `Pontifex.RR.Reliable.RRReliableFrame` and its header types;
- `Pontifex.RR.Reliable.RRReliableRequestState`;
- `Pontifex.RR.Reliable.RRReliableRequestTable`;
- `Pontifex.RR.Reliable.RRReliableServerSession`.

Deterministic timer assertions use the production
`Pontifex.Utils.ManualTimerFactory`; its own contract is owned by the
[`TimerFactory` suite](../../../Pontifex.Tests/TimerFactory/README.md).

## Contract references

- [`RRReliable` specification](../../../../Pontifex/Abstractions/Transports/RR/Reliable/Specification.md)
  — sections 6.4, 6.6, 8, 10, and 15.
- Production sources:
  [`Pontifex/Implementations/RR/Reliable/`](../../../../Pontifex/Implementations/RR/Reliable/).

## Layer

| Layer | Subject | Directory | Inheritance parent | Coverage |
| --- | --- | --- | --- | --- |
| Root | RR reliable helper group | `Tests/Pontifex.Tests/Misc/RRReliableHelpers/` | none | [`Coverage.md`](Coverage.md) |

## Naming convention

Fixtures are `<Subject>Tests`. Test names use `Scenario_expected_result`, for
example `ResponseFrame_RoundTripsUIntMaxOrdinal`.

## Directory layout

```text
Misc/RRReliableHelpers/
  README.md
  Coverage.md
  Harness/
    RecordingReliableRequestHandler.cs
  Tests/
    RRReliableFrameTests.cs
    RRReliableRequestStateTests.cs
    RRReliableRequestTableTests.cs
    Integration/
      RRReliableServerSessionTests.cs
```

## Groups

| Group | Directory |
| --- | --- |
| Integration | `Tests/Integration/` |

## Harness

- `RecordingReliableRequestHandler.cs` is a `RRRequestHandler` that records
  response values and completion types and releases every delivered payload.

There are no harness self-tests, so the `RRReliableHelpers.Harness`
category is not used.

The suite uses the production `Pontifex.Utils.ManualTimerFactory` to drive every
deterministic timer assertion. It is production code, not harness or loose support of
this layer.

## Coverage

Coverage is recorded in [`Coverage.md`](Coverage.md).

## Categories and commands

- Suite root category: `RRReliableHelpers`.
- Additional filtering category: `RRReliableHelpers.Integration` for the parallel
  `RRReliableServerSession` stress methods in `Tests/Integration/`.

```bash
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj \
  --filter "TestCategory=RRReliableHelpers"
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj \
  --filter "TestCategory=RRReliableHelpers.Integration"
```

## Adding tests

Add a new `*Tests.cs` file under `Tests/`. Each file contains exactly one
fixture named `<Subject>Tests`, carries
`[Category("RRReliableHelpers")]`, and uses the
`Pontifex.Tests.Misc.RRReliableHelpers` namespace. Record the test in
`Coverage.md` in the same change.

A Test Suit has exactly one root layer. Do not add a nested layer; a different
subject needs a separate suite. Test files sit directly in `Tests/`, except the
parallel `RRReliableServerSession` stress tests, which live in the
`Tests/Integration/` group and carry `RRReliableHelpers.Integration`.

New harness types belong in `Harness/`; groups must not contain harness.

## Out of scope

- The client and server transport behaviour: the abstract RR transport suite
  under `Tests/Pontifex.Tests/Transports/RR/Reliable` and its client and server
  layers own it.
- The timer factory contract and its real-timer integration: the
  [`TimerFactory` suite](../../../Pontifex.Tests/TimerFactory/README.md) owns it.
- Real timer firing: the production factory is tested for synchronous
  scheduling and disposal only here; deterministic TTL tests use the manual factory.
- The Direct and Net carriers and bindings.
- Payload serialization internals of `UnionDataList`.

## Rules

This suite follows [`Tests/RULES.md`](../../../RULES.md).

## Default and integration runs

The default run of `Tests/Pontifex.Tests` excludes every fixture that carries an
`*.Integration` category. The integration run executes only those fixtures.

```bash
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj \
  --filter "TestCategory!~Integration"
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj \
  --filter "TestCategory~Integration"
```

The suite's integration category is `RRReliableHelpers.Integration`.
