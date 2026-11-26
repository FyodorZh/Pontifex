# RawUnreliable Direct Test Suit

## Rationale

The in-process `RawUnreliable` carrier is the connectionless Direct transport. It
adds the process-wide channel registry, the lazy client connection, per-source
session routing, the bounded send budgets, the commit-queue admission window,
and the optional virtual delivery systems. The suite pins the carrier-specific
behaviour that the shared contract conformance suite cannot observe: budget
wiring, channel-registry ownership, delivery-system lifetime, commit admission,
inbound limits, and the server-side routing contract.

## Suite type

**Test Suit.** The subject is one small, tightly coupled group of non-abstract
types: `RawUnreliableDirectClientTransport`, `RawUnreliableDirectServerTransport`,
`RawUnreliableDirectBinding`, and their endpoint halves.

## Root subject

- `RawUnreliableDirectClientTransport` and its client endpoint;
- `RawUnreliableDirectServerTransport` and its session endpoints;
- `RawUnreliableDirectBinding` and `RawUnreliableDirectInfo`.

## Contract references

- [`RawUnreliable` specification](../../../../Pontifex/Abstractions/Transports/Raw/Unreliable/Specification.md)
  — the abstract contract.
- [Direct RawUnreliable specification](../../../../Transports/Pontifex.Transport.Direct/Raw/Unreliable/Specification.md)
  — the carrier choices, limits, ownership rules, and declared deviations.
- Production sources:
  `../../../../Transports/Pontifex.Transport.Direct/Raw/Unreliable/`.

## Layer

| Layer | Subject | Directory | Inheritance parent | Coverage |
| --- | --- | --- | --- | --- |
| Root | Raw unreliable Direct group | `Tests/Pontifex.Transport.Direct.Tests/Raw/RawUnreliableDirect/` | none | [`Coverage.md`](Coverage.md) |

## Naming convention

Fixtures are `<Subject>Tests`. Test names use `Scenario_expected_result`, for
example `ClientCommitWindow_RejectsThe101stMessageWithBufferOverflow`.

## Directory layout

```text
Raw/RawUnreliableDirect/
  README.md
  Coverage.md
  Tests/
    RawUnreliableDirectBudgetWiringTests.cs
    RawUnreliableDirectDeliverySystemTests.cs
    RawUnreliableDirectServerContractTests.cs
    Integration/
      RawUnreliableDirectChannelOwnershipTests.cs
      RawUnreliableDirectDrainIntegrationTests.cs
      RawUnreliableDirectInboundLimitTests.cs
      RawUnreliableDirectOwnershipTests.cs
      RawUnreliableDirectSendAdmissionTests.cs
```

`Tests/Integration/` is a group, not a layer.

## Harness

There is no suite-local `Harness/` directory. The suite uses two shared, loose
test-support types outside all suites:

- [`TestSupport/TestBuildContext.cs`](../../TestSupport/TestBuildContext.cs) is
  a hand-written `ITransportBuildContext` and struct value for the binding and
  ownership tests.
- [`TestSupport/BlockingDeliverySystem.cs`](../../TestSupport/BlockingDeliverySystem.cs)
  stalls the commit worker so the 100-message admission window can be filled
  deterministically.

They are shared loose support because the Direct suites live in one test
project. There are no harness self-tests, so the `Direct.RawUnreliable.Harness`
category is not used.

## Coverage

Coverage is recorded per layer in [`Coverage.md`](Coverage.md). The root layer
covers every test in the suite, including the `Tests/Integration/` group.

## Categories and commands

- Suite root category: `Direct.RawUnreliable`.
- Additional category: `Direct.RawUnreliable.Integration`.

```bash
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RawUnreliable&TestCategory!=Direct.RawUnreliable.Integration"
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RawUnreliable.Integration"
```

## Adding tests

Add a new test file under `Tests/`. A new `*Tests.cs` file contains exactly one
fixture named `<Subject>Tests`, carries `[Category("Direct.RawUnreliable")]`, and
uses the `Pontifex.Tests.Raw.RawUnreliableDirect` namespace. Record the test in
`Coverage.md` in the same change. Add a test that needs real scheduling or
bounded waits under `Tests/Integration/` and add the
`Direct.RawUnreliable.Integration` category as well.

A Test Suit has exactly one root layer. Do not add a nested layer; if a test
needs a different subject, create a separate suite.

New shared support types belong under `TestSupport/` only when more than one
suite needs them; otherwise keep them inside the test file or add a suite
`Harness/`.

## Groups

| Group | Directory |
| --- | --- |
| Integration | `Tests/Integration/` |

## Out of scope

- Contract-level lifecycle, delivery, callback, and route conformance:
  [`DirectRawUnreliableConformanceTests`](../../../Pontifex.Transports.Tests/Raw/Unreliable/DirectRawUnreliableConformanceAdapterTests.cs)
  in `Tests/Pontifex.Transports.Tests` runs the shared conformance suite against
  this carrier.
- `Channel`/`ChannelManager` registry and delivery-system replacement semantics:
  they belong to the ChannelManager suite.
- The `RawReliable` and RR carriers: they have their own suites.
- The shared `UnreliableDeliveryBinding` parser: it is shared with the RR
  unreliable binding and is covered by the loose `UnreliableDeliveryBindingTests`
  at the test-project root.
- The netem algorithms and loss models: they belong to the VirtualDelivery suite.
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
