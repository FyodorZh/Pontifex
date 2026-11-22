# ChannelManager Test Suit

## Rationale

`Channel` and `ChannelManager` are the in-process carrier core. They own peer
pairing, message hand-off, registry lifetime, and delivery-system replacement.
Every Direct transport depends on them, so their behaviour is pinned here before
the Raw and RR suites build on top.

`ChannelManager` is `internal`; this suite depends on the test assembly's
`InternalsVisibleTo` entry.

## Suite type

**Test Suit.** The subject is one small, tightly coupled group of non-abstract
types.

## Root subject

- `Channel` and its delivery-system ownership;
- `ChannelManager` and its process-wide server registry.

## Contract references

- [`RawReliable` Direct specification §6](../../../Transports/Pontifex.Transport.Direct/Raw/Reliable/Specification.md)
  — lifecycle and channel teardown.
- [`RawUnreliable` Direct specification §6](../../../Transports/Pontifex.Transport.Direct/Raw/Unreliable/Specification.md)
  — registry removal and delivery-system ownership.
- Production sources: `../../../Transports/Pontifex.Transport.Direct/Channel/`.

## Layer

| Layer | Subject | Directory | Inheritance parent | Coverage |
| --- | --- | --- | --- | --- |
| Root | `Channel` and `ChannelManager` group | `Tests/Pontifex.Transport.Direct.Tests/ChannelManager/` | none | [`Coverage.md`](Coverage.md) |

## Naming convention

Fixtures are `<Subject>Tests`. Test names use `Scenario_expected_result`, for
example `Connect_WithoutARegisteredServer_ReturnsNull`.

## Directory layout

```text
ChannelManager/
  README.md
  Coverage.md
  Harness/
    TrackingDeliverySystem.cs
  Tests/
    ChannelTests.cs
    ChannelManagerTests.cs
```

There are no groups and no nested layers.

## Harness

`Harness/TrackingDeliverySystem.cs` is a test-only `IDeliverySystem` that records
`Clear` and `Dispose` calls. There are no harness self-tests, so the
`Direct.ChannelManager.Harness` category is not used.

## Coverage

Coverage is recorded per layer in [`Coverage.md`](Coverage.md).

## Categories and commands

- Suite root category: `Direct.ChannelManager`.
- Additional categories: none.

```bash
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.ChannelManager"
```

## Adding tests

Add a new test file under `Tests/`. A new `*Tests.cs` file contains exactly one
fixture named `<Subject>Tests`, carries `[Category("Direct.ChannelManager")]`,
and uses the `Pontifex.Tests.Channels` namespace. Record the test in
`Coverage.md` in the same change.

A Test Suit has exactly one root layer. Do not add a nested layer; if a test
needs a different subject, create a separate suite.

The registry is a process-wide singleton. Every test uses unique `StringEndPoint`
names and unregisters its servers in `TearDown`; keep that isolation rule for new
tests. Dispose any detached channel in the test that detached it.

## Groups

None.

## Out of scope

- Raw transport routing, budgets, and session lifecycle: those belong to the Raw
  suites.
- Netem behavior: it belongs to the VirtualDelivery suite.
- Cross-process or socket behavior: `ChannelManager` is in-process only.

## Rules

This suite follows [`Tests/RULES.md`](../../RULES.md).
