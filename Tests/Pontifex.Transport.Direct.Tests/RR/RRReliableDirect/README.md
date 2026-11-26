# RRReliable Direct Test Suit

## Rationale

The in-process `RRReliable` carrier is the reliable counterpart of the Direct
RRUnreliable transport. It combines the reusable reliable RR core with the
shared lazy channel client and server carriers, the serialized server dispatch
queue, per-session duplicate suppression, and the process-wide server registry.
The suite pins the carrier-specific lifecycle, failure detection, binding
surface, and end-to-end request/response behaviour.

## Suite type

**Test Suit.** The subject is one small, tightly coupled group of non-abstract
types: `RRReliableDirectClient`, `RRReliableDirectServer`,
`RRReliableDirectBinding`, and the shared `DirectRrChannelClient` and
`DirectRrChannelServer` carriers they reuse.

## Root subject

- `RRReliableDirectClient` and `RRReliableDirectServer`;
- `RRReliableDirectBinding`;
- the shared Direct RR channel client and server carriers.

## Contract references

- [`RRReliable` specification](../../../../Pontifex/Abstractions/Transports/RR/Reliable/Specification.md)
  — the abstract contract.
- [Direct RRReliable specification](../../../../Transports/Pontifex.Transport.Direct/RR/Reliable/Specification.md)
  — the carrier choices and the three declared deviations.
- Production sources:
  `../../../../Transports/Pontifex.Transport.Direct/RR/Reliable/`.

## Layer

| Layer | Subject | Directory | Inheritance parent | Coverage |
| --- | --- | --- | --- | --- |
| Root | RR reliable Direct group | `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableDirect/` | none | [`Coverage.md`](Coverage.md) |

## Naming convention

Fixtures are `<Subject>Tests`. Test names use `Scenario_expected_result`, for
example `ChannelDisposalAfterALiveSession_FailsOnceAndFinishesPendingRequests`.

## Directory layout

```text
RR/RRReliableDirect/
  README.md
  Coverage.md
  Harness/
    RecordingReliableHandlers.cs
    TestMessages.cs
  Tests/
    RRReliableDirectBindingTests.cs
    RRReliableDirectLifecycleTests.cs
    Integration/
      RRReliableDirectIntegrationTests.cs
      RRReliableDirectRoundTripTests.cs
```

`Tests/Integration/` is a group, not a layer.

## Harness

- `RecordingReliableHandlers.cs` supplies a client request handler that records
  responses and completions and can act from inside a response callback, and a
  server handler with accept, respond, finish, and reject modes.
- `TestMessages.cs` builds integer payloads and payloads of an exact serialized
  size.

The binding tests use the shared loose
[`TestSupport/TestBuildContext.cs`](../../TestSupport/TestBuildContext.cs)
outside the suite. The deterministic timer is the shared loose
[`ManualTimerFactory`](../../TestSupport/ManualTimerFactory.cs); both are
shared because the Direct suites live in one test project.

There are no harness self-tests, so the `Direct.RRReliable.Harness` category is
not used.

## Coverage

Coverage is recorded per layer in [`Coverage.md`](Coverage.md). The root layer
covers every test in the suite, including the `Tests/Integration/` group.

## Categories and commands

- Suite root category: `Direct.RRReliable`.
- Additional category: `Direct.RRReliable.Integration`.

```bash
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRReliable&TestCategory!=Direct.RRReliable.Integration"
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRReliable.Integration"
```

## Adding tests

Add a new test file under `Tests/`. A new `*Tests.cs` file contains exactly one
fixture named `<Subject>Tests`, carries `[Category("Direct.RRReliable")]`, and
uses the `Pontifex.Tests.RR.RRReliableDirect` namespace. Record the test in
`Coverage.md` in the same change. Add an integration test under
`Tests/Integration/` and add the `Direct.RRReliable.Integration` category.

A Test Suit has exactly one root layer. Do not add a nested layer; if a test
needs a different subject, create a separate suite.

New harness types belong in `Harness/`; groups must not contain harness.

## Groups

| Group | Directory |
| --- | --- |
| Integration | `Tests/Integration/` |

## Out of scope

- The reusable reliable RR core, including the shared `Pontifex.RR.RRTransport`
  base: it is covered by `RRReliableCoreHelpers` and the abstract transport
  suite under `Tests/Pontifex.Tests/Transports/RR`.
- The shared Direct RR carriers as standalone subjects: the RRUnreliable Direct
  suite pins their carrier-level behaviour.
- The VirtualDelivery algorithms and netem statistics: they belong to the
  VirtualDelivery suite.
- Cross-process or socket behaviour: the Direct carrier is in-process only.

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

Integration categories in use: `Direct.ChannelManager.Integration`,
`Direct.RawReliable.Integration`, `Direct.RawUnreliable.Integration`,
`Direct.RRReliable.Integration`, and `Direct.RRUnreliable.Integration`.
