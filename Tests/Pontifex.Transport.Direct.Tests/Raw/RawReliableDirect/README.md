# RawReliable Direct Test Suit

## Rationale

The in-process `RawReliable` carrier is the reliable Direct transport. It adds
the process-wide channel registry, the synchronous handshake, the zero-linger
reject path, the positive handshake deadline, and the channel teardown that the
session endpoint owns. The suite pins the carrier-specific handshake, lifecycle,
error mapping, and session-notification behaviour on top of the abstract
`RawReliable` contract.

## Suite type

**Test Suit.** The subject is one small, tightly coupled group of non-abstract
types: `RawReliableDirectClientTransport`, `RawReliableDirectServerTransport`,
`RawReliableDirectBinding`, and their endpoint halves.

## Root subject

- `RawReliableDirectClientTransport` and `RawReliableDirectClientEndpoint`;
- `RawReliableDirectServerTransport` and `RawReliableDirectServerEndpoint`;
- `RawReliableDirectBinding` and `RawReliableDirectInfo`.

## Contract references

- [`RawReliable` specification](../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md)
  — the abstract contract.
- [Direct RawReliable specification](../../../../Transports/Pontifex.Transport.Direct/Raw/Reliable/Specification.md)
  — the scheme, handshake, zero-linger reject, deadline, and pre-connect buffer
  choices.
- Production sources:
  `../../../../Transports/Pontifex.Transport.Direct/Raw/Reliable/`.

## Layer

| Layer | Subject | Directory | Inheritance parent | Coverage |
| --- | --- | --- | --- | --- |
| Root | Raw reliable Direct group | `Tests/Pontifex.Transport.Direct.Tests/Raw/RawReliableDirect/` | none | [`Coverage.md`](Coverage.md) |

## Naming convention

Fixtures are `<Subject>Tests`. Test names use `Scenario_expected_result`, for
example `Rejected_client_stops_with_HandshakeRejected`.

## Directory layout

```text
Raw/RawReliableDirect/
  README.md
  Coverage.md
  Tests/
    RawReliableDirectBindingParametersTests.cs
    Integration/
      RawReliableDirectTransportTests.cs
```

`Tests/Integration/` is a group, not a layer.

## Harness

There is no suite-local `Harness/` directory. The suite uses two shared, loose
test-support types outside all suites:

- [`TestSupport/TestBuildContext.cs`](../../TestSupport/TestBuildContext.cs) is
  a hand-written `ITransportBuildContext` for the binding-parameter tests.
- [`TestSupport/ManualTimerFactory.cs`](../../TestSupport/ManualTimerFactory.cs)
  is the deterministic `IRawReliableHandshakeTimerFactory`; it lets the
  handshake deadlines be fired explicitly instead of waiting on real time.

They are shared loose support because the Direct suites live in one test
project. There are no harness self-tests, so the `Direct.RawReliable.Harness`
category is not used.

## Coverage

Coverage is recorded per layer in [`Coverage.md`](Coverage.md). The root layer
covers every test in the suite, including the `Tests/Integration/` group.

## Categories and commands

- Suite root category: `Direct.RawReliable`.
- Additional category: `Direct.RawReliable.Integration`.

```bash
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RawReliable&TestCategory!=Direct.RawReliable.Integration"
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RawReliable.Integration"
```

## Adding tests

Add a new test file under `Tests/`. A new `*Tests.cs` file contains exactly one
fixture named `<Subject>Tests`, carries `[Category("Direct.RawReliable")]`, and
uses the `Pontifex.Tests.Raw.RawReliableDirect` namespace. Record the test in
`Coverage.md` in the same change. Add a test that starts real channel pairs,
uses bounded waits, or shares mutable channel state under `Tests/Integration/`
and add the `Direct.RawReliable.Integration` category as well.

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

- The abstract `RawReliable` handshake, framing, pre-connect buffer, and
  endpoint lifecycle: they are covered by
  `Tests/Pontifex.Tests/Endpoints/RawReliable` and
  `Tests/Pontifex.Tests/Transports/Raw/Reliable`.
- `Channel`/`ChannelManager` registry semantics: they belong to the
  ChannelManager suite.
- The `RawUnreliable` and RR carriers: they have their own suites.
- Virtual delivery and netem: the reliable binding exposes no delivery
  parameter.
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
