# FSM Test Suite

## Rationale

The state-machine seam is a small, tightly coupled family around
`IFSMBase<TState>`. The Api and transport layers compose the implementations:
`FSM` owns the transition graph, `RatchetFSM` restricts it to forward moves,
`SerializedFSM` serializes callbacks, and `AsyncFSM` queues transitions on a
worker. A regression in the base contract, the veto path, or the async queue
silently changes lifecycle behavior in those callers, so this suite pins all
four implementations together.

## Suite type

**Test Suit.** The subject is one small, tightly coupled group of non-abstract
types implementing `IFSMBase<TState>`.

## Root subject

- `Pontifex.Utils.FSM.IFSMBase<TState>`, `IFSM<TState>`, `IConcurrentFSM<TState>`,
  and `IFSM_Ctl<TState>` (contract exercised through the implementations);
- `Pontifex.Utils.FSM.FSM<TState, TStateValue>`;
- `Pontifex.Utils.FSM.SerializedFSM<TState>`;
- `Pontifex.Utils.FSM.RatchetFSM<TState>`;
- `Pontifex.Utils.FSM.AsyncFSM<TState>`.

## Contract references

- [IFSM.cs](../../../Pontifex/Utils/FSM/IFSM.cs)
- [IFSM_Ctl.cs](../../../Pontifex/Utils/FSM/IFSM_Ctl.cs)
- [FSM.cs](../../../Pontifex/Utils/FSM/Implementations/FSM.cs)
- [SerializedFSM.cs](../../../Pontifex/Utils/FSM/Implementations/SerializedFSM.cs)
- [RatchetFSM.cs](../../../Pontifex/Utils/FSM/Implementations/RatchetFSM.cs)
- [AsyncFSM.cs](../../../Pontifex/Utils/FSM/Implementations/AsyncFSM.cs)

## Layer

| Layer | Subject | Directory | Coverage |
| --- | --- | --- | --- |
| Root | FSM family around `IFSMBase<TState>` | `Tests/Pontifex.Tests/FSM/` | [`Coverage.md`](Coverage.md) |

## Naming convention

- Test file = `<Subject>Tests.cs`; fixture = `public sealed class <Subject>Tests`.
- Test methods = `Scenario_ExpectedResult`, for example
  `SetState_onStateChanging_VetoesTransition`.
- Namespace = `Pontifex.Tests.StateMachine`. It is intentionally not
  `Pontifex.Tests.FSM`: a namespace segment named `FSM` would shadow the
  production `FSM<TState, TStateValue>` type inside its own tests. This mirrors
  the `DeliveryManager` suite, which avoids shadowing its subject the same way.
- Every fixture carries `[TestFixture]` and `[Category("FSM")]`.

## Directory layout

```text
FSM/
  README.md
  Coverage.md
  Tests/
    AsyncFSMTests.cs
    FSMTests.cs
    RatchetFSMTests.cs
    SerializedFSMTests.cs
```

There are no groups under `Tests/` and no `Harness/` directory: every subject is
public production code and the tests need no doubles.

## Coverage

Coverage is recorded per layer in [`Coverage.md`](Coverage.md). It lists the
covered tests, planned gaps, and gaps deferred by design.

## Categories and commands

- Suite root category: `FSM`.
- Additional filtering categories: none.

```bash
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter "TestCategory=FSM"
```

## Adding a layer

This suite has exactly one root layer; adding a layer is not applicable. A
different subject needs a separate suite.

## Adding tests

Add a `<Subject>Tests.cs` file under `Tests/` with exactly one fixture named
`<Subject>Tests`, carrying `[TestFixture]` and `[Category("FSM")]`, in the
`Pontifex.Tests.StateMachine` namespace. Record the test in
[`Coverage.md`](Coverage.md) in the same change. The suite is deterministic and
has no integration category; do not add real timers, sleeps, or wall-clock
dependence.

## Out of scope

- End-to-end Api and transport lifecycle behavior driven by these state
  machines: the Api and transport suites own it.
- Concurrency stress beyond the serialized-queue guarantee already asserted for
  `SerializedFSM` and `AsyncFSM`.
- Allocation, profiling, and performance characteristics.
- Interface-only tests: the shared contract is proven through every concrete
  implementation.

## Rules

This suite follows [Tests/RULES.md](../../RULES.md).
