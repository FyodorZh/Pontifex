# TimerFactory Test Suite

## Rationale

The timer seam is the only clock in several transport layers: every request TTL, response TTL,
handshake deadline, and terminal deferral is driven by a callback scheduled through
`Pontifex.Utils.ITimerFactory`. The production factory and the deterministic manual factory share
one contract, and a regression in either silently changes every reliable transport. This suite pins
that contract before it is consumed by the carriers.

## Suite type

**Test Suit.** The subject is one small, tightly coupled group of concrete types around the
`ITimerFactory` contract.

## Root subject

- `Pontifex.Utils.ITimerFactory` and its contract;
- `Pontifex.Utils.SystemTimerFactory`;
- `Pontifex.Utils.ManualTimerFactory` (a manual factory with a caller-driven virtual clock)
  and `Pontifex.Utils.ManualTimerHandle`.

## Contract references

- [ITimerFactory.cs](../../../Pontifex/Utils/TimerFactory/ITimerFactory.cs)
- [SystemTimerFactory.cs](../../../Pontifex/Utils/TimerFactory/SystemTimerFactory.cs)
- [ManualTimerFactory.cs](../../../Pontifex/Utils/TimerFactory/ManualTimerFactory.cs)

## Layer

| Layer | Subject | Directory | Coverage |
| --- | --- | --- | --- |
| Root | `ITimerFactory` group | `Tests/Pontifex.Tests/TimerFactory/` | [`Coverage.md`](Coverage.md) |

## Naming convention

- Test file = `<Subject>Tests.cs`; fixture = `public sealed class <Subject>Tests`.
- Test methods = `Scenario_ExpectedResult`, for example `Schedule_WithInfiniteDelay_ReturnsAHandleThatNeverFires`.
- Namespace = `Pontifex.Tests.TimerFactory`.
- Every fixture carries `[TestFixture]` and `[Category("TimerFactory")]`.

## Directory layout

```text
TimerFactory/
  README.md
  Coverage.md
  Tests/
    ManualTimerFactoryTests.cs
    SystemTimerFactoryTests.cs
```

There are no groups under `Tests/` and no `Harness/` directory: the manual factory is production
code, so it is a subject rather than a test double.

## Coverage

Coverage is recorded per layer in [`Coverage.md`](Coverage.md). It lists the covered tests, planned
gaps, and gaps deferred by design.

## Categories and commands

- Suite root category: `TimerFactory`.
- Additional filtering category: `TimerFactory.Integration` for the methods that need a real timer.
  The default deterministic run excludes them.

```bash
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter "TestCategory=TimerFactory"
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter "TestCategory=TimerFactory.Integration"
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter "TestCategory!~Integration"
```

## Adding a layer

This suite has exactly one root layer; adding a layer is not applicable. A different subject needs
a separate suite.

## Adding tests

Add a `<Subject>Tests.cs` file under `Tests/` with exactly one fixture named `<Subject>Tests`,
carrying `[Category("TimerFactory")]`. Keep the default run deterministic: no real timers, sleeps,
or wall-clock waits. A test that must observe a real timer carries `[Category("TimerFactory.Integration")]`
on the method and lives in the same file as the rest of its fixture. Record the test in
[`Coverage.md`](Coverage.md) in the same change.

## Out of scope

- Transport TTL and handshake behaviour: the reliable transport suites own how the seam is used.
- Alternative timer implementations.
- Real-time accuracy, drift, and scheduling fairness: only eventual delivery is asserted.

## Rules

This suite follows [Tests/RULES.md](../../RULES.md).
