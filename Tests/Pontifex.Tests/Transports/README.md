# Transport Test Suite

Black-box unit tests for the transport class hierarchy in `Pontifex`.

The suite lives under `Tests/Pontifex.Tests/Transports/` and tests the transport classes
directly (no carriers, sockets, threads, timers, or sleeping), one test-set per class. It is the
transport-level counterpart of the `Endpoints` suite: the same deterministic and
public-plus-protected-contract approach, applied one layer up.

Currently the suite covers `AnyTransport`, the root that owns the common transport lifecycle, and
`RawTransport`, the raw typed facade over it. Tests for the raw reliable and raw unreliable
transport families will be added incrementally, following the same layout and rules.

> **Conformance controls are explicitly out of scope.**
> Do not test, arm, inject, assert, or otherwise use anything from the conformance subsystem:
> `Conformance`, `IConformanceControl`, `GetControls` in its conformance role, checkpoint gates,
> `FailNextStart`, `InjectUnrecoverableFailure`, and related controls. That subsystem is planned
> for a significant rewrite and may be removed. Tests in this suite must not depend on it.
>
> Some production constructors currently require a conformance-control instance. The harness
> passes a trivial one only to satisfy the signature; it is never armed, inspected, or asserted.

```
Transports/
├── README.md                          (this file)
├── Harness/                           test doubles shared by all test-sets
│   └── TestAnyTransport.cs            test double for AnyTransport
├── AnyTransportTests.cs               test-set for AnyTransport
└── Raw/                               test-sets for the raw transport family
    ├── Harness/
    │   └── TestRawTransport.cs        test double for RawTransport
    └── RawTransportTests.cs           test-set for RawTransport
```

Layout rule: each transport family lives in its own directory (`Raw/`, then
`Raw/<SubFamily>/` as the hierarchy grows) with the family-specific test doubles under
`<Family>/Harness/`, one `<ClassName>Tests.cs` file per class. Test doubles shared across
families stay in the top-level `Harness/`. The folder structure does not change the namespace:
every file in this suite uses `namespace Pontifex.Tests.Transports;`.

## Goals

- Exercise each transport class through its **public + protected contract**.
- Assert only **observable effects**, never private state.
- Be deterministic and fast: no real carriers, no real time, no background work, no sleeping.
- Test the class's own delta, not the inherited layers again.

## Coverage rule

Each test-set covers only the behavior its class introduces: its own delta and invariants.
Inherited behavior is covered once, by the test-set for the layer that owns it, and is not
repeated in derived test-sets. A derived test-set may use inherited machinery as setup, but its
assertions stay on the class's own contract.

## Non-goals

- Re-testing endpoint behavior: `RawUnreliableEndpoint` and the raw-reliable endpoint family are
  covered by the `Endpoints` suite.
- Re-testing carrier behavior: delivery, ordering, socket handling, and end-to-end topology
  behavior belong to the conformance suite in `Tests/Pontifex.Transports.Tests`.
- Testing conformance controls (see the note above).

## What is covered

| Transport class | Test-set | Status | Focus |
|---|---|---|---|
| `AnyTransport` | `AnyTransportTests` | covered (32 tests) | lifecycle, start/stop/fail, callbacks, validity |
| `RawTransport` | `RawTransportTests` | covered (6 tests) | raw transport contract, message size, constructor forwarding |

Planned families:

| Family | Status |
|---|---|
| `Raw/Reliable*` transports | not started |
| `Raw/Unreliable*` transports | not started |

### `AnyTransport` contract matrix

- **Constructor / observables**: `Name`, `Memory`, and `Type` are captured; `Log` is a non-null
  wrapped logger; `IsValid` is `true`; `IsStarted` is `false`; `HasStartBeenAttempted` is
  `false`.
- **Start**: null callback throws `ArgumentNullException` without attempting a start; the happy
  path calls `TryStart` then `OnStarted` and returns `true`; a `TryStart` that returns `false`
  invalidates the transport, returns `false`, and raises no stop notification; a second `Start`
  or a `Start` after a stop/fail returns `false` without calling the hooks again; a `Start` after
  a stop that happened *before* the first start still succeeds.
- **Start / stop race**: if `TryStart` stops the transport synchronously, `Start` returns `false`,
  `OnStarted` is not called, and the stop callback is delivered once.
- **Stop**: before start is a no-op that returns `true` and does not consume the one-shot start;
  while running it stops the transport, invokes `OnStopped` and then the `Start` callback, and
  keeps `IsValid` true; a null reason becomes `Unknown` sourced from `Name`; repeated stops return
  `true` without a second notification; a stop on an invalid transport returns `false`.
- **Failure visibility**: during `OnStopped` from `Stop`, `IsStarted` is already false and
  `IsValid` is still true; during `OnStopped` from `Fail`, both are false.
- **Fail**: while running it invalidates the transport and notifies `OnStopped` then the start
  callback; while not started it only invalidates; after a stop it invalidates without a second
  notification; repeated failures do the first one only.
- **Fail overloads**: `Fail(string, string, params object[])` creates a `TextFail` sourced from
  `Name`; `Fail(StopReason, string)` creates a `ChainFail`; `FailException` creates an
  `ExceptionFail`. The reason delivered to `OnStopped` and the start callback is the same instance.
- **Exception isolation**: an exception thrown by `OnStopped` or by the start callback is caught;
  the other callback is still invoked and the lifecycle call completes.
- **HasStartBeenAttempted**: becomes true when `Start` is accepted for processing, including a
  failed `TryStart`; a stop before the first start leaves it false; it never resets.
- **ToString** returns the runtime type name.

### `RawTransport` contract matrix

`RawTransport` is a thin typed facade over `AnyTransport`; its delta is small, so this test-set
stays small and deliberately does not repeat the full `AnyTransport` matrix.

- **Constructor**: `Name` and `Memory` are forwarded to the base; `Log` is non-null.
- **Message size**: `MessageMaxByteSize` is supplied by the derived transport, reflects the
  configured value, and is exposed through `IRawTransport`.
- **Interface exposure**: an instance is assignable to `IRawTransport` and `ITransport`.

This test-set asserts only the `RawTransport`-specific items above. Inherited behavior is not
repeated; see the coverage rule above.

Not covered here: `GetControls`, because its only current contribution is the conformance
control, which this suite deliberately does not use.

## How it works

### Black-box boundary

The boundary of the test-set is **public + protected**. Tests reach the abstract hooks through a
**test double** that derives from the class under test; they never inspect private fields and
never use reflection. Only these are asserted:

- method return values,
- the `Start`-supplied stop callback and its reason,
- the abstract hook invocations (`TryStart`, `OnStarted`, `OnStopped`) and their order,
- read-only observables (`Name`, `Type`, `Memory`, `Log`, `IsValid`, `IsStarted`,
  `HasStartBeenAttempted`),
- the state observed from inside `OnStopped`.

### Test double

`TestAnyTransport : AnyTransport` is the seam for the class under test. It:

- implements `Type` and the three abstract lifecycle hooks;
- records `TryStartCalls`, `OnStartedCalls`, `OnStoppedCalls`, and `LastOnStoppedReason`;
- records whether `IsStarted`/`IsValid` were already updated when `OnStopped` ran;
- records hook names in invocation order (`tryStart`, `started`, `stopped`);
- lets each test choose the `TryStart` result or supply behavior for `OnStarted`/`OnStopped`;
- exposes `HasStartBeenAttempted`.

`TestRawTransport : RawTransport` supplies the abstract `MessageMaxByteSize` and `Type` values and
implements the inherited lifecycle hooks with no additional behavior. It passes a trivial
`RawConformanceControl` only because the constructor currently requires one; the control is never
armed, inspected, or asserted.

No conformance control is supplied, armed, or inspected by the harness for testing purposes.

### Typical test shape

```csharp
var transport = new TestAnyTransport("TestTransport");
int stopCallbackCount = 0;

Assert.That(transport.Start(_ => stopCallbackCount++), Is.True);
Assert.That(transport.IsStarted, Is.True);

Assert.That(transport.Stop(new UserIntention("test")), Is.True);
Assert.That(transport.IsStarted, Is.False);
Assert.That(transport.OnStoppedCalls, Is.EqualTo(1));
Assert.That(stopCallbackCount, Is.EqualTo(1));
```

Guard cases are written the same way: configure the double, call the public lifecycle method,
and assert the observable result.

## Conventions

- NUnit 4. `using NUnit.Framework;` is global (declared in the test project).
- `namespace Pontifex.Tests.Transports;`, `public sealed class <ClassName>Tests`.
- One test class per file; file named after the class.
- Test methods: `Scenario_ExpectedResult` (e.g. `Stop_WhenRunning_StopsNotifiesInOrderAndKeepsValid`).
- Hand-written fakes only — the repo has **no mocking library**.
- Add `[Category("Transports")]` to each test class.
- **Do not use the conformance subsystem in this suite.** Do not obtain it through
  `GetControls`, do not inject a conformance control, do not arm gates, and do not call
  `FailNextStart`/`InjectUnrecoverableFailure`.
- If a test allocates `UnionDataList` or byte arrays, add
  `[TearDown] public void TearDown() => GC.Collect();` (see `UnionDataListTests`). Lifecycle
  tests that do not rent pooled buffers do not need it.

## How to extend: add a test-set for a transport class

1. Add a test double under the family's `Harness/` (or the top-level `Harness/` when shared),
   deriving from the class under test:
   - implement the class's `abstract` hooks;
   - expose the `protected` members the test-set needs;
   - record hook invocations and capture observable results;
   - provide helpers that complete or trigger the class's protected transitions explicitly.
2. Add any shared fakes the class needs (recording handlers, endpoint doubles, and so on). The
   existing `Endpoints/Harness` doubles may be reused when a transport test needs a real endpoint
   object, because both suites live in the same test assembly.
3. Add `<Family>/<ClassName>Tests.cs` and cover **that class's delta** plus its invariants.
4. Follow the conventions above.
5. Update the coverage table in this README.
6. Run the suite.

If a class is not deterministic because it hard-wires a background component, prefer a small
production test seam (the same approach used by `ISerialActionQueue` in the endpoint suite) over
timing-based tests. Do not add conformance controls as the workaround.

## Running

```
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj
```

Filter to this suite:

```
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports
```
