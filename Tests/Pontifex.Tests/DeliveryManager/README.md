# DeliveryManager Test Suite

Black-box contract tests for the `Pontifex.Delivery` delivery subsystem: the tightly coupled
group of concrete components that `DeliveryManager` composes to provide scheduling, packing,
chunking, serialization, deduplication, ordering, retry, and acknowledgement.

## Suite type

**Test Suit.** Every subject is a concrete type, so the suite has exactly one root layer. There
are no nested layers and no inheritance tree to model.

Root subject group (tightly coupled by composition):

- `Pontifex.Delivery.DeliveryManager`
- `Pontifex.Delivery.DeliveryReporter`
- `Pontifex.Delivery.DeliveryDispatcher`
- `Pontifex.Delivery.DeliveryInfoSerializer`
- `Pontifex.Delivery.Deduplicator`
- `Pontifex.Delivery.DeliverySorter<TParcel>`
- `Pontifex.Delivery.DeliverySortingManager`
- `Pontifex.Delivery.MessagePacker`
- `Pontifex.Delivery.MessageSplitter`
- `Pontifex.Delivery.MessageMerger`
- `Pontifex.Delivery.RetryDeliveryScheduler`

The group is one subject because the components have no independent life: `DeliveryManager`
constructs the serializer, deduplicator, dispatcher, splitter, merger, and packer in its own
constructor; `DeliverySortingManager` wraps a delivery manager; `DeliveryDispatcher` consumes an
`IDeliveryAttemptScheduler`; and `DeliverySorter<TParcel>` is the ordering primitive underneath
`DeliverySortingManager`. Testing one component's contract without the others would leave the
composed contract unproven.

## Contract references

- [how-it-works.md](../../../Pontifex/Utils/DeliveryManager/how-it-works.md)
- [how-to-use.md](../../../Pontifex/Utils/DeliveryManager/how-to-use.md)
- [IDeliveryManager.cs](../../../Pontifex/Utils/DeliveryManager/IDeliveryManager.cs)
- [DeliveryManager.cs](../../../Pontifex/Utils/DeliveryManager/DeliveryManager.cs)
- [DeliveryDispatcher.cs](../../../Pontifex/Utils/DeliveryManager/DeliveryDispatcher.cs)
- [DeliveryInfo.cs](../../../Pontifex/Utils/DeliveryManager/DeliveryInfo.cs)
- [DeliveryInfoSerializer.cs](../../../Pontifex/Utils/DeliveryManager/Serialization/DeliveryInfoSerializer.cs)
- [DeliveryReporter.cs](../../../Pontifex/Utils/DeliveryManager/DeliveryReporter.cs)
- [Deduplicator.cs](../../../Pontifex/Utils/DeliveryManager/Deduplicator.cs)
- [DeliverySorter.cs](../../../Pontifex/Utils/DeliveryManager/Sorting/DeliverySorter.cs)
- [DeliverySortingManager.cs](../../../Pontifex/Utils/DeliveryManager/Sorting/DeliverySortingManager.cs)
- [MessagePacker.cs](../../../Pontifex/Utils/DeliveryManager/MessagePacker.cs)
- [MessageSplitter.cs](../../../Pontifex/Utils/DeliveryManager/SplitAndMerge/MessageSplitter.cs)
- [MessageMerger.cs](../../../Pontifex/Utils/DeliveryManager/SplitAndMerge/MessageMerger.cs)
- [QueuedMessage.cs](../../../Pontifex/Utils/DeliveryManager/QueuedMessage.cs)
- [IDeliveryAttemptScheduler.cs](../../../Pontifex/Utils/DeliveryManager/Scheduler/IDeliveryAttemptScheduler.cs)
- [RetryDeliveryScheduler.cs](../../../Pontifex/Utils/DeliveryManager/Scheduler/RetryDeliveryScheduler.cs)
- [SendResult.cs](../../../Pontifex/Abstractions/SendResult.cs)
- [DeliveryId.cs](../../../Pontifex/Utils/DeliveryId.cs)

## Layers

The layer table is the authoritative declaration of layer name, subject, directory, and
coverage file.

| Layer | Subject | Directory | Coverage |
|---|---|---|---|
| `DeliveryManager` | `Pontifex.Delivery` delivery subsystem (eleven concrete types centered on `DeliveryManager`) | `Tests/Pontifex.Tests/DeliveryManager` | [`Coverage.md`](Coverage.md) |

A Test Suit has exactly one layer, placed at the suite root. Test files live under `Tests/`.
Groups, if added later, are `Tests/` subfolders, are not layers, and do not get their own
`Coverage.md` or `Harness/`.

## Naming conventions

- Layer name = the subsystem name, `DeliveryManager`.
- Test file = `<Subject>Tests.cs`; fixture class = `public sealed class <Subject>Tests`.
- Test methods = `Scenario_ExpectedResult`, for example
  `ScheduleDeliver_AtCapacity_ReturnsBufferOverflow`.
- Namespace = `Pontifex.Tests.Delivery` for test files; harness files use
  `Pontifex.Tests.Delivery.Harness`. The root namespace is intentionally not
  `...DeliveryManager`, because that would shadow the `DeliveryManager` type inside its own tests.
- Every fixture carries `[TestFixture]` and `[Category("DeliveryManager")]`.

## Directory layout

```
DeliveryManager/
├── README.md
├── Coverage.md                              root layer
├── Harness/
│   ├── TrackedFixture.cs
│   └── TrackedResources.cs
└── Tests/
    ├── DeduplicatorTests.cs
    ├── DeliveryDispatcherTests.cs
    ├── DeliveryInfoSerializerTests.cs
    ├── DeliveryManagerTests.cs
    ├── DeliveryReporterTests.cs
    ├── DeliverySorterTests.cs
    ├── DeliverySortingManagerTests.cs
    ├── MessageMergerTests.cs
    ├── MessagePackerTests.cs
    ├── MessageSplitterTests.cs
    └── RetryDeliverySchedulerTests.cs
```

There are currently no groups under `Tests/`. Any future groups are subfolders of `Tests/` and
are not layers.

## Harness layout and sharing

Harness is test-only: stubs, recording doubles, test buffers, and manual drivers. It contains no
production code and no mocking library is used; doubles are hand-written.

- `Harness/TrackedResources.cs` wraps the per-test byte-array and collectable pools and attaches a
  `MultiRefResourceMonitor` to every rented resource. `TrackedFixture` creates it per test and
  asserts at teardown that every resource was released exactly once.
- `Harness/TrackedFixture.cs` is the base class for fixtures that use the pools. It creates a
  fresh `MemoryRental` per test, exposes `Pool` and `CPool`, lets a test register cleanup actions
  (`AddCleanup`) and captured messages (`TrackCaptured`), and fails the test if a rented resource
  is still alive or was released twice.
- Double classes that are used by one fixture stay nested in that fixture (for example
  `RetryDeliverySchedulerTests.StubDeliveryTask`); they use no pools and do not derive from
  `TrackedFixture`.
- When new shared test-only helpers are needed, add them under `Harness/` with the
  `Pontifex.Tests.Delivery.Harness` namespace.
- A Test Suit has no nested layers, so harness is never shared between sibling layers. If the
  suite is ever split, shared harness moves to the nearest common ancestor layer.

## Coverage

Coverage is recorded per layer in [`Coverage.md`](Coverage.md). It lists the covered tests,
planned gaps, and gaps deferred by design. The README does not duplicate coverage status.

## Categories

- Suite root category: `DeliveryManager`. Every fixture carries it.
- Additional filtering categories: none currently.

Any non-default facet (real I/O, concurrency stress, timing, load) must go into a separate group
or suite with its own declared category, not the default suite run.

## Running

Run the whole test project:

```
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj
```

Filter to this suite:

```
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=DeliveryManager
```

## Adding a layer

A Test Suit has exactly one layer, so adding a layer is not applicable. If the suite ever needs
nested layers, or if the subject becomes an inheritance tree, split it into multiple Test Suits
or convert it to an Abstract Test Suit as required by [Tests/RULES.md](../../RULES.md).

To add coverage for another concrete component of the same subsystem, add a
`<Subject>Tests.cs` file under `Tests/` (or a group subfolder of `Tests/`), give it one
`public sealed class <Subject>Tests`, and place it in the root layer's `Coverage.md`.

## Adding tests

1. Put the test in the file of the component whose contract it proves under `Tests/`.
2. Exercise the public or internal contract only; do not inspect private state or use reflection.
3. Pass explicit `DateTime` values into scheduling entry points; never sleep or depend on the
   wall clock for a pass/fail decision.
4. Assert observable results: return values, event callbacks, captured wire messages, ordering,
   counts, and contract-visible buffer state.
5. Derive pool-using fixtures from `TrackedFixture`, register manager `Clear` methods with
   `AddCleanup`, and let the fixture verify release; update [`Coverage.md`](Coverage.md) in the
   same change.

## Out of scope

- Real transports, carriers, sockets, networks, threads, timers, and wall-clock time. Those
  belong to the transport and endpoint suites.
- Thread safety and concurrent use. The delivery subsystem documents that callers must provide
  external synchronization; that contract belongs to a categorised concurrency layer or suite.
- End-to-end integration with a live transport or a remote peer.
- The loose tests at the project root for shared primitives (`DeliveryIdTests`,
  `UnionDataListTests`, `ZigZagVarIntSerializerTests`). They are not part of this suite.

## Rules

This suite follows [Tests/RULES.md](../../RULES.md).
