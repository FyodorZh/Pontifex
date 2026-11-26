# VirtualDelivery Test Suit

## Rationale

The virtual-delivery component group is the deterministic core of the Direct
transport's impairment layer. Its algorithms (correlated randomness, empirical
distributions, loss models, rate calculation) are pure and can be verified
without a transport, a thread, or real time. Keeping those checks in a dedicated
suite pins the arithmetic before the asynchronous `NetemDeliverySystem` is
exercised.

## Suite type

**Test Suit.** The subject is one small, tightly coupled group of non-abstract
types.

## Root subject

- `CorrelatedRandom`, `DistributionTable`, `RateLimiter`, `MonotonicTime`;
- `RandomLossModel`, `FourStateLossModel`, `GilbertElliotLossModel`;
- `NetemDeliverySystem` and `SlotScheduler`, including their deterministic manual clock and pump.

## Contract references

- [`help.md`](../../../Transports/Pontifex.Transport.Direct/VirtualDelivery/Netem/help.md)
  — the netem configuration and pipeline reference.
- Production sources: `../../../Transports/Pontifex.Transport.Direct/VirtualDelivery/`.

## Layer

| Layer | Subject | Directory | Inheritance parent | Coverage |
| --- | --- | --- | --- | --- |
| Root | Virtual-delivery component group | `Tests/Pontifex.Transport.Direct.Tests/VirtualDelivery/` | none | [`Coverage.md`](Coverage.md) |

## Naming convention

Fixtures are `<Subject>Tests`. Test names use
`Scenario_expected_result` with underscores between words, for example
`Probability_zero_never_drops`.

## Directory layout

```text
VirtualDelivery/
  README.md
  Coverage.md
  Harness/
    ScriptedRandom.cs
  Tests/
    Netem/
      CorrelatedRandomTests.cs
      DistributionTableTests.cs
      FourStateLossModelTests.cs
      GilbertElliotLossModelTests.cs
      NetemDeliverySystemTests.cs
      NetemDeterministicTests.cs
      RandomLossModelTests.cs
      RateLimiterTests.cs
```

`Tests/Netem/` is a group, not a layer.

## Harness

`Harness/ScriptedRandom.cs` is a test-only 32-bit source that returns a fixed
script. It makes the loss-model transition tests exact instead of statistical.
There are no harness self-tests, so the `Direct.VirtualDelivery.Harness`
category is not used.

## Coverage

Coverage is recorded per layer in [`Coverage.md`](Coverage.md). The root layer
covers every test in the suite, including the `Tests/Netem/` group.

## Categories and commands

- Suite root category: `Direct.VirtualDelivery`.
- Additional categories: none.

```bash
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.VirtualDelivery"
```

## Adding tests

Add a new test file under `Tests/` or, for algorithm-specific additions, under
`Tests/Netem/`. A new `*Tests.cs` file contains exactly one fixture named
`<Subject>Tests`, carries `[Category("Direct.VirtualDelivery")]`, and uses the
`Pontifex.Tests.VirtualDelivery` namespace. Record the test in `Coverage.md` in
the same change.

The latency, jitter, rate, slot, duplication, queue-limit, and dispose pipeline
tests in `NetemDeterministicTests` use the internal manual clock and pump, so no
test waits on real netem timing.

A Test Suit has exactly one root layer. Do not add a nested layer; if a test
needs a different subject, create a separate suite.

To add a group, create a subfolder under `Tests/` that contains only test files
or nested groups, and list it in `Directory layout` and `Groups` below.

## Groups

| Group | Directory |
| --- | --- |
| Netem | `Tests/Netem/` |

## Out of scope

- Wall-clock delivery through the `NetemDeliverySystem` background thread: the
  default run uses the internal manual clock and pump instead.
- Carrier framing, message limits, and delivery-system ownership: those belong
  to the Raw test suites.

## Rules

This suite follows [`Tests/RULES.md`](../../RULES.md).

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
