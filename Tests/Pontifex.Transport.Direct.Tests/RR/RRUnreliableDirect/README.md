# RRUnreliable Direct Test Suit

## Rationale

The in-process `RRUnreliable` carrier is the first RR transport over
`Channel`/`ChannelManager`. It combines the reusable RR unreliable core with a
lazy channel client, a bounded server dispatch queue, virtual delivery systems,
and a process-wide server registry. The suite pins the carrier-specific
behaviour before the reliable RR Direct carrier reuses the same core.

## Suite type

**Test Suit.** The subject is one small, tightly coupled group of non-abstract
types: `RRUnreliableDirectClient`, `RRUnreliableDirectServer`,
`RRUnreliableDirectBinding`, and their shared `DirectRrChannelClient` and
`DirectRrChannelServer` carriers.

## Root subject

- `RRUnreliableDirectClient` and `RRUnreliableDirectServer`;
- `RRUnreliableDirectBinding`;
- the shared Direct RR channel client and server carriers.

## Contract references

- [`RRUnreliable` specification](../../../../Pontifex/Abstractions/Transports/RR/Unreliable/Specification.md)
  — the abstract contract.
- [Direct RRUnreliable specification](../../../../Transports/Pontifex.Transport.Direct/RR/Unreliable/Specification.md)
  — the carrier choices and the four declared deviations.
- Production sources:
  `../../../../Transports/Pontifex.Transport.Direct/RR/`.

## Layer

| Layer | Subject | Directory | Inheritance parent | Coverage |
| --- | --- | --- | --- | --- |
| Root | RR unreliable Direct group | `Tests/Pontifex.Transport.Direct.Tests/RR/RRUnreliableDirect/` | none | [`Coverage.md`](Coverage.md) |

## Naming convention

Fixtures are `<Subject>Tests`. Test names use `Scenario_expected_result`, for
example `DuplicateResponse_YieldsExactlyOneCallback`.

## Directory layout

```text
RR/RRUnreliableDirect/
  README.md
  Coverage.md
  Harness/
    ManualClock.cs
    RecordingHandlers.cs
    ScriptedDeliverySystem.cs
    TestBuildContext.cs
    TestMessages.cs
    TrackingDeliverySystem.cs
  Tests/
    RRUnreliableDirectBindingTests.cs
    RRUnreliableDirectCarrierTests.cs
    RRUnreliableDirectLifecycleTests.cs
    RRUnreliableDirectRoundTripTests.cs
    Integration/
      RRUnreliableDirectIntegrationTests.cs
```

`Tests/Integration/` is a group, not a layer.

## Harness

- `ManualClock.cs` is a deterministic `IDateTimeProvider` for both TTLs.
- `TrackingDeliverySystem.cs` records delivery, clear, and dispose calls and can
  throw on delivery.
- `ScriptedDeliverySystem.cs` applies a deterministic script of deliver, drop,
  duplicate, and throw actions.
- `RecordingHandlers.cs` supplies a recording request handler and a server
  handler with respond, reject, silent-accept, and blocking modes.
- `TestBuildContext.cs` is a hand-written `ITransportBuildContext` for the
  binding tests.
- `TestMessages.cs` builds integer payloads and payloads of an exact serialized
  size.

There are no harness self-tests, so the `Direct.RRUnreliable.Harness` category
is not used.

## Coverage

Coverage is recorded per layer in [`Coverage.md`](Coverage.md). The root layer
covers every test in the suite, including the `Tests/Integration/` group.

## Categories and commands

- Suite root category: `Direct.RRUnreliable`.
- Additional category: `Direct.RRUnreliable.Integration`.

```bash
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRUnreliable&TestCategory!=Direct.RRUnreliable.Integration"
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRUnreliable.Integration"
```

## Adding tests

Add a new test file under `Tests/`. A new `*Tests.cs` file contains exactly one
fixture named `<Subject>Tests`, carries `[Category("Direct.RRUnreliable")]`, and
uses the `Pontifex.Tests.RR.RRUnreliableDirect` namespace. Record the test in
`Coverage.md` in the same change. Add an integration test under
`Tests/Integration/` and add the `Direct.RRUnreliable.Integration` category.

A Test Suit has exactly one root layer. Do not add a nested layer; if a test
needs a different subject, create a separate suite.

New harness types belong in `Harness/`; groups must not contain harness.

## Groups

| Group | Directory |
| --- | --- |
| Integration | `Tests/Integration/` |

## Out of scope

- The reusable RR unreliable core: it is covered by the core suites.
- The VirtualDelivery algorithms and netem statistics: they belong to the
  VirtualDelivery suite.
- The RRReliable core and the RRReliable Direct carrier: they have their own
  suites.
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

Integration categories in use: `Direct.RRReliable.Integration`,
`Direct.RRUnreliable.Integration`, `Direct.RawUnreliable.Integration`, and
`Direct.Integration`.
