# LowLevelTcpSocket test suite

Deterministic and loopback tests for the internal `LowLevelTcpSocket` transport in
`Transports/Pontifex.Transport.Net`.

- **Root:** `Tests/Pontifex.Transport.Net.Tests/LowLevelTcpSocket/`
- **Test namespace:** `Pontifex.Tests.NetSockets.LowLevelTcpSocketTests`
- **Harness namespace:** `Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness`
- **Suite category:** every fixture is tagged `[LowLevelTcpSocketSuite]`, so other suites in the
  same project can include or exclude this one with `--filter "TestCategory=LowLevelTcpSocket"`.

## Layout

```
LowLevelTcpSocket/
├── README.md
├── Harness/                 ns …LowLevelTcpSocketTests.Harness
│   ├── LowLevelSocketHarness.cs          wires LowLevelTcpSocket to all doubles
│   ├── FakeSocketAccessor.cs             scripted ISocketAccessor (strict contract checks)
│   ├── TestSocketAsyncEventArgs.cs       manually completed SocketAsyncEventArgs
│   ├── ControllableQueue.cs              SPSC source with pause/resume + strict overlap checks
│   ├── ControllablePool.cs               crafted buffer injection (queue/factory/throw)
│   ├── ReceiveSink.cs                    release/throw consumer policies
│   ├── TestBuffers.cs                    TrackingByteArray / ThrowingReleaseByteArray
│   ├── TestPools.cs                      ByteArrayConcurrentPoolGuard over the shared pool
│   ├── RealSocketFixture.cs              loopback TCP pair + ObservingSocketAccessor + consumer
│   ├── TestWait.cs / Threading.cs        bounded waits and background actions
│   ├── TestDeadlineScheduler.cs          deterministic deadline timer seam
│   ├── TestTeardownExecutor.cs           manually run ITeardownExecutor seam
│   ├── AllocationMeasurer.cs             calibrated per-thread allocation measurement
│   ├── NoCopyReceiveConsumer.cs          no-copy, release-in-finally measurement consumer
│   ├── ReusingReceivePool.cs             zero-allocation receive-owner pool for measuring
│   ├── StrictSingleWriterQueue.cs        strict SPSC double (rejects concurrent readers/writers)
│   ├── ApplicationByteLedger.cs          executable byte-budget composition example
│   ├── ApplicationLivenessWatchdog.cs    executable liveness composition example
│   ├── TestCategories.cs / LowLevelTcpSocketSuiteAttribute.cs
│   └── Tests/               harness self-tests (same Harness namespace)
│       ├── HarnessSanityTests.cs          wiring smoke through LowLevelSocketHarness
│       ├── FakeSocketAccessorTests.cs     accessor contract and completion semantics
│       ├── TestSocketAsyncEventArgsTests.cs
│       ├── ControllableQueueTests.cs      strict SPSC overlap detection
│       ├── TrackingByteArrayTests.cs      ref-count diagnostics
│       ├── AllocationMeasurerTests.cs     measurement calibration controls
│       └── NoCopyReceiveConsumerTests.cs
└── Tests/                   ns …LowLevelTcpSocketTests
    ├── Construction/        LowLevelTcpSocketConstructionTests, ConstructionRollbackTests,
    │                        DeadlineValidationTests, SystemSocketTests
    ├── Sending/             SenderPumpTests, SendFailureTests, SenderShutdownTests,
    │                        SenderShutdownDeadlineTests, WholeDrainDeadlineTests,
    │                        SynchronousSendTests
    ├── Receiving/           ReceiverTests, ReceiveFailureTests, PartialReceiveViewTests,
    │                        ReceivePoolTests, SynchronousReceiveTests
    ├── Draining/            SourceDrainedTests, DrainHandlerAdmissionTests,
    │                        DrainNotificationHandoffTests, DrainNotificationAllocationTests,
    │                        SourceQuiescedTests
    ├── Completion/          CompletionTests, ErrorPrecedenceTests
    ├── Teardown/            AccessorDisposalTests, BoundedDisposalTests, BoundedDisposalDeadlineTests,
    │                        ForcedPublicationTests, TeardownConcurrencyTests, DisposedStateTests,
    │                        DirectionalShutdownFaultTests
    ├── Ownership/           OwnershipTests, EventArgsStorageTests
    ├── Policies/            RemoteHalfClosePolicyTests
    ├── Composition/         ProducerCompositionTests, LoopbackSocketTests,
    │                        LoopbackHalfClosePolicyTests,
    │                        ApplicationByteLedgerTests, ApplicationLivenessWatchdogTests
    └── Probes/              AllocationBaselineTests, LoadProbeTests, TransportProfilingProbes
```

## Conventions

- One cohesive contract per fixture, one fixture per file, file name = class name (`<Subject>Tests`).
- Groups follow the connection lifecycle — construction → sending → receiving → draining →
  completion → teardown — with cross-cutting concerns (ownership, policies, composition, probes)
  as separate groups. A fixture belongs to exactly one group.
- Harness types are internal, live under `Harness/`, and are never used by production code. Tests of
  the harness itself live in `Harness/Tests/` and share the Harness namespace.
- Test method names are descriptive sentences.
- Class summaries describe the contract the fixture pins.

## Running the suite

```sh
# whole suite (use a configuration where the selected categories are calibrated)
dotnet test "Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj" \
  --filter "TestCategory=LowLevelTcpSocket"

# Debug correctness (excludes Release-only probes)
dotnet test "Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj" \
  -c Debug --filter "TestCategory=LowLevelTcpSocket&TestCategory!=LowLevelTcpSocket.Allocation&TestCategory!=LowLevelTcpSocket.Load"

# calibrated allocation budgets/baselines (Release only)
dotnet test "Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj" \
  -c Release --filter "TestCategory=LowLevelTcpSocket.Allocation"

# throughput/latency/retention probes (Release only)
dotnet test "Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj" \
  -c Release --filter "TestCategory=LowLevelTcpSocket.Load"
```

## Pinned dependency versions

Resolved by `Tests/Pontifex.Transport.Net.Tests` (see `obj/project.assets.json`):

| Package | Version | Git commit |
| --- | --- | --- |
| Actuarius.Memory | `0.1.9-dev.0` | `f7e79ba` |
| Actuarius.Collections | `0.1.5-dev.0` | `ac40df7` |
| Actuarius.Concurrent | `0.1.11-dev.0` | `6acfc2c` |

## Dependency-specific behavior

Claims about installed dependency behavior are pinned by executable assertions rather than prose:
`TrackingByteArrayTests` pins the non-clamped ref counter and `ReleaseOfReleasedObject` underflow
reporting, `PartialReceiveViewTests`/`ReceivePoolTests` pin partial-view lifetime and
recyclable-pool ownership, and `AllocationBaselineTests` records the wrapper-allocation baselines.
Re-verify a dependency-specific claim against the resolved versions above before adding a new
assertion for it.

## Transport contract

The behavior the suite asserts for `LowLevelTcpSocket`:

| Decision | Resolution |
| --- | --- |
| Completion semantics | **Fully quiesced.** `Completion` resolves only once no `onReceived` callback is running. A slow/blocking callback delays completion; the shutdown deadline bounds it. |
| Error precedence | **First-terminal-wins.** A genuine fault arriving after an abort/dispose does not replace the primary reason; it is at most secondary and may be dropped. |
| Queued buffer ownership | **Producer owns** queued-but-unsent buffers. The transport releases only its in-flight buffer; the producer seals publication, requests shutdown, then drains (and releases) its own queue only after `ITcpSocketSender.SourceQuiesced` resolves. `Sender.Completion` is logical completion and may resolve earlier (possibly while a source read is still blocked); it is **not** a drain-safe handoff. Graceful shutdown sends the observed snapshot. |
| Callback ownership | **Consumer owns the buffer.** The transport transfers ownership to `onReceived` and **never releases it, even when the callback throws**; the callback must release it (e.g. in `finally`). A consumer that violates this leaks by its own fault, not a transport defect. |
| Partial-receive views | **Reusable, revivable, resettable.** A partial receive is delivered as an internal revivable view over the rented owner: exactly one source reference while any reference is live, reset (source/array/offset/count) and returned to a small bounded pool only at the final release. Valid-lifetime `IMultiRefByteArray` semantics/diagnostics (offset/array delegation, `RefCount` view, monitor, underflow report) are preserved; a released reference is invalid and may be recycled. A full-buffer receive still hands the owner directly. |
| Receive pools (rental) | **Opt-in, dependency-provided recycling.** The default `MemoryRental.Shared.ByteArraysPool` is non-recycling and is the allocating baseline (≈96 B/op warm). `ReceivePools.Shared` (`SharedByteArraysPool`, wrapper cache 1000) and `ReceivePools.Bounded(capacity)` (`ByteArrayConcurrentPool` over the shared raw-array pool) are allocation-free warm; callers opt in by passing the factory result as the existing `byteArraysPool` ctor argument (no signature change). `Count` is the **requested** capacity; the raw backing array is rounded up to the next power of two (`NextPow2`) and may be larger — the receiver arms `SetBuffer(Array, Offset, Count)`, so only the requested capacity is a receive window. Recycled wrappers reuse object identity, so the transport's exactly-once release is load-bearing: a stale released reference must never be released again. `ByteArrayConcurrentPoolGuard` does **not** exercise recycling (its own `Release` is a no-op), so recycling/double-release safety is tested against the real pools. |
| Immediate shutdown | **Directional + deadline.** Discard queued data, let the in-flight chunk finish, abort if it stalls past a deadline. |
| Shutdown deadlines | `DisposeAsync(TimeSpan)`/`Shutdown(bool, TimeSpan)` force-finalize on expiry, completing `Socket.Completion` **and both directional completions**. Deadline scope is the **entire shutdown/drain**: expiry is persistent sender lifecycle state that survives source-read and completion gaps, prevents any further send posting, and force-publishes unfinished logical termination. The `gracefulShutdownDeadline` ctor param is the default for the **first parameterless graceful `Shutdown(true)`** and for the remote-EOF `GracefulSendShutdown` policy. Explicit sender overloads override it; explicit `Timeout.InfiniteTimeSpan` cancels an active deadline; repeated parameterless requests are idempotent (no extension), and a deadline already established by a direct request is not restarted by remote EOF. Deadline callbacks carry a generation and validate the current absolute deadline, so an already-dispatched obsolete callback cannot abort a replacement. No `CancellationToken` overload. |
| Bounded teardown publication | `DisposeAsync(TimeSpan)` arms its budget after validation and non-blocking admission and requests **logical** abort without synchronously performing blocking physical teardown before its first asynchronous return. An already-completed connection returns without scheduling any timer/task state. The connection's single reserved physical teardown (both-direction shutdown, accessor disposal, finalization) is queued to an internal executor (`ITeardownExecutor`; production: one thread-pool work item per connection). Concurrent bounded waiters share one per-connection disposal deadline with earliest-expiration semantics (a shorter waiter may shorten it; a longer waiter never extends an earlier caller's bound); successful completion cancels the shared timer, and an obsolete dispatched callback cannot act. Normal completion still awaits physical cleanup and receive callbacks; deadline-forced completion resolves the aggregate and both directional tasks while actual cleanup and `SourceQuiesced` may remain pending. `Dispose()` keeps best-effort synchronous semantics; `DisposeAsync()` (infinite) queues and waits for quiesced completion. A disposal failure completing after the immutable forced result is reported once through the diagnostic callback. |
| `Close()` | Alias for `Dispose()` (best-effort synchronous teardown). |
| Receive buffer size | **Configurable** via the `receiveBufferSize` ctor param (default 16 KiB, must be positive). |
| Completion result detail | `SocketCompletionResult` exposes the directional `SenderCompletionResult?`/`ReceiverCompletionResult?` in addition to the aggregate reason/errors. |
| Remote vs local abort | A peer-driven abort (`AbortConnection`) reports `SocketCompletionReason.RemoteAborted`, distinct from a local `Disposed`. |
| Input validation | **Reject at boundaries.** Require positive receive capacity and a stream/TCP socket; classify null/invalid send items. |
| SAEA pooling | **Not pooled.** Each instance owns two `SocketAsyncEventArgs` for its lifetime; an external pool seam is a future option. |
| Byte pressure | **Not bounded by the transport.** The producer must bound the outbound source; consumers must bound receive-buffer retention. |
| Idle / stalled-I/O | **No transport-level idle deadline.** Enforce liveness externally (keepalive); bound teardown with a shutdown deadline. |
| WouldBlock | **Bounded retry, then fault.** Up to 100 retries (`MaxWouldBlockRetries`); a transiently back-pressured connection may fault at the limit. This is intentional; there is no re-post-as-pending behavior. |
| Benign shutdown errors | `TryShutdown` treats `ObjectDisposedException`, `OperationAborted`, `ConnectionAborted` (and `NotConnected`/`Shutdown`) as benign during teardown — never `Faulted`. |
| Operation-context errors | A zero-progress send faults with a **dedicated exception type** carrying direction, `SocketError`, buffer offset/count and bytes sent. The context assertion waits on the production type. |
| Callback throw | A thrown `onReceived` is routed to `onCallbackError` **and** faults the receiver (fail-fast). The transport still never releases the buffer (consumer owns it). |
| Cleanup failures | The transport's own cleanup is **best-effort**: it always publishes `Completion` (and the directional result) even if releasing its buffer or disposing args throws, attaching the cleanup failure as the secondary error. |
| Teardown gates | Buffer releases, event-args disposal, and directional `Shutdown` run **outside** the directional I/O gates: ownership/resources are reserved and detached under a short critical section, the external call runs detached, and completion/errors are recorded afterwards. Abort state admission never waits for a blocked pool return or accessor call; normal completion still waits for the reserved cleanup attempts. A directional shutdown reserves its ending state before the external call, so no new operation starts while it is in flight and a concurrent abort keeps first-terminal precedence. |
| SourceDrained | **Trampolined with per-handler atomic admission and immutable subscriber snapshots.** Non-recursive, serialized, at-most-one pending; every handler is admitted (or rejected) atomically against terminal state under the sender gate, and an admitted handler may enter/finish later. No handler starts after terminal. Subscriptions build an immutable snapshot at subscription time; delivery under stable subscriptions allocates no notification metadata. Duplicate subscription/removal follows standard .NET event semantics (a doubly subscribed delegate runs twice; one removal removes one occurrence), and a handler subscribed during a callback is owed the next edge, not the current one. |
| Quiescence scope | `Completion` covers `onReceived` callbacks only, not `SourceDrained` handlers. Drain handlers are excluded from completion quiescence; their admission is the per-handler atomic contract above (an admitted handler may outlive terminal transition). |

## Producer seal → shutdown → SourceQuiesced → drain

`Sender.Completion` and `Sender.SourceQuiesced` are two different lifetimes: `Completion` is
logical (it may resolve while a source read is still blocked, e.g. cancellation of a stalled
pop), while `SourceQuiesced` is the reader-quiescence acknowledgment. The single-writer producer
must seal publication before requesting shutdown and may only drain its own queued buffers after
`SourceQuiesced`:

```csharp
// Single writer: stop publishing new items.
producer.Sealed = true;

// Ask for a graceful send-side shutdown (explicit overloads override the ctor default deadline).
socket.Sender.Shutdown(true);

// Optional: await logical completion for result/diagnostics.
var result = await socket.Sender.Completion;

// Required before any other reader touches the source: reader quiescence.
await socket.Sender.SourceQuiesced;

// Safe now: drain and release the producer-owned queued-but-unsent buffers.
while (source.TryPop(out IMultiRefByteArray leftover))
{
    leftover.Release();
}
```

## Application composition contracts

The transport verifies ownership and progress, not application flow control. The executable
composition contracts in `ProducerCompositionTests`, `LoopbackSocketTests`, and
`RealSocketFixture` make these rules testable against loopback TCP, the real Actuarius SPSC
source, and the installed pool:

- **One serialized writer.** The source is `ISingleReaderWriterConcurrentQueue<IMultiRefByteArray>`:
  one writer, one reader (the transport). Multiple threads may call `Pump()`, but outbound
  publication must be serialized by the application or an adapter. `StrictSingleWriterQueue`
  rejects a second concurrent `Put`, and `ProducerHandoffHelper_WaitsForSourceQuiesced` proves a
  Completion-only drain overlaps the transport's reader on a strict source.
- **Application-owned queued/received buffers.** Queued-but-unsent buffers stay the producer's;
  buffers delivered to `onReceived` transfer to the consumer and must be released by it in a
  `finally` (the transport never releases them, even on a callback throw).
- **Bounded publication / late reclamation.** Logical publication (`Completion`, both directional
  results) is separate from physical cleanup. A shutdown deadline force-publishes while a native
  operation may still be in flight; the late native completion keeps an exactly-once cleanup path,
  so borrowed storage is returned only after the native operation finishes.
- **Byte-based admission/retention budgeting.** The transport deliberately does **not** bound
  queued or consumer-retained bytes, and it is not an implicit credit protocol. Applications must
  bound outbound publication and consumer retention by bytes (item counts are insufficient when
  buffer sizes vary) and track queued, native-pending, consumer-held, and cleanup bytes separately.
  `ApplicationByteLedger` demonstrates this; it adds no transport flow control.
- **One-reader handoff.** Producers drain only after `SourceQuiesced`; a producer that drains after
  `Completion` alone can create two simultaneous SPSC readers.
- **External liveness.** Normal I/O has no transport idle/read/write deadline. Enforce liveness
  externally (application keepalive/ping) and bound teardown with
  `DisposeAsync(TimeSpan)`/`Shutdown(bool, TimeSpan)`.
- **Native backpressure vs scripted `WouldBlock`.** Real TCP backpressure presents as a
  pending native operation (`SendAsync`/`ReceiveAsync` return `true`), not as a synchronous
  `WouldBlock`; the fake accessor can script repeated `WouldBlock`, which is a different path.
  `StalledPeer_ObservedPendingSend_DeadlineBoundsTeardown` establishes backpressure by observed
  pending status. The 100-retry policy is intentionally not exercised as real behavior here.

Guarded-pool ownership assertions are `AssertNoOutstanding()` **plus** `ErrorCount == 0`; note that
asserting while a lease is still outstanding records a diagnostic error, so balance is asserted
only after the contract's release point (or through a per-buffer `TrackingByteArray` signal for
late completions).

## Application byte ledger and liveness example

`ApplicationByteLedger` and `ApplicationLivenessWatchdog` are test-local executable composition
examples for the two responsibilities the transport deliberately leaves to the application. They
are never used by the transport hot path and add no production API:

- **Byte-weighted admission.** `TryAdmitQueued` charges the item's `Count` against a byte budget
  (not an item count) across `Queued`/`Pending`/`ConsumerHeld`/`DeferredCleanup`; a rejection
  leaves the producer's reference and every counter untouched.
- **Two distinct metrics.** `LogicalBytes` is application bookkeeping (sum of phase byte counts);
  `PhysicalRetainedBytes` is deduplicated raw-array capacity (a partial view and its owner share
  the same backing array and count once). `PhysicalRootCount` tracks distinct physical roots.
- **Credit lifetime.** A delivered lease stays in `ConsumerHeld` (and consumes the budget) after
  logical `Completion`/`Socket` publication; only the application's final `ConsumerRelease`
  releases it, exactly once (a second call is recorded as misuse and does not change counters).
- **External liveness.** `ApplicationLivenessWatchdog` polls a caller-supplied monotonic clock and
  runs one callback per stall episode (e.g. `ForceAbortFinalize` or `DisposeAsync(deadline)`).
  Disabling it leaves normal transport I/O intentionally pending: there is no transport-level
  idle/read/write deadline.

The transport remains intentionally unbounded for application-queued/retained bytes: the ledger is
the application's account, not transport flow control, and no implicit credit protocol exists.

## Profiling matrix

`TransportProfilingProbes` (Release-only `Load` category) plus `LoadProbeTests` cover: cold
connection churn vs long-lived steady state (bytes/connection, args setup/disposal), ambient-context
sync/async cost, idle-after-large-send retention, delayed consumer retention with bounded cache
misses, many pending receives sized by a documented 4 MiB storage budget, shutdown storms with
shared-deadline timer entries, and competing connections under the synchronous pumps with no
fairness budget (latency percentiles, `Monitor.LockContentionCount`, GC counts/pause). Assertions
are sanity/balance checks only; each probe prints its warmed measurements through `TestContext.Out`.

## Driving socket completion

`SocketAsyncEventArgs.BytesTransferred` is read-only and `OnCompleted` is protected. The transport
builds its args through `ISocketAccessor.CreateEventArgs()`; tests inject `FakeSocketAccessor`, which
returns `TestSocketAsyncEventArgs` (writes the transfer count into the private backing field and raises
`Completed`).

## Harness catalog

All in `Harness/` (tests of the harness live in `Harness/Tests/`):

- `FakeSocketAccessor` / `TestSocketAsyncEventArgs` — scripted send/receive/shutdown outcomes.
- `ControllableQueue` — SPSC source with `Pause`/`Resume`, pop/put counters, `PeekAll`, and a
  scripted-pop hook (`OnEmptyObservation`, `ForceEmptyCount`) for deterministic race tests.
- `Threading` / `BackgroundAction` — run an action on a background thread, capture its exception and
  join with a timeout (for the lifecycle tests that need two logical threads).
- `ControllablePool` — crafted buffer injection (queue, factory, `AcquireException`, `OnAcquire`).
- `TestPools.GuardedRealPool()` — `ByteArrayConcurrentPoolGuard` over the shared real pool for leak /
  double-release assertions.
- `ManualTeardownExecutor` (`TestTeardownExecutor.cs`) — manually started, joined
  `ITeardownExecutor` for deterministic bounded teardown tests (the production adapter queues to
  the thread pool).
- `TrackingByteArray`, `ThrowingReleaseByteArray`, `TestBuffers` — buffer ownership fixtures.
  `TrackingByteArray.OnFinalRelease` is a per-instance hook for modelling a blocked or reentrant
  pool return.
- `ReceiveSink` — records delivered buffers and models consumer release/throw policies.
- `RealSocketFixture` — loopback TCP pair: real `SystemSocket`-backed transport, real
  `SingleReaderWriterConcurrentQueue<IMultiRefByteArray>` source, guarded real pool, and a
  `RealReceiveConsumer`. `ObservingSocketAccessor` records native-pending send/receive status.
- `StrictSingleWriterQueue` — strict SPSC double that rejects concurrent readers/writers.
- `AllocationMeasurer` / `NoCopyReceiveConsumer` — calibrated per-thread allocation measurement and
  a no-copy, release-in-`finally`, counter/validator consumer.
- `ReusingReceivePool` — zero-allocation receive-owner pool for measuring windows (keeps one
  reference per preallocated owner across reuses).
- `ReceivePools` (production, internal) — the documented opt-in receive-pool factory:
  `Shared`/`Bounded(capacity)` use the dependency's recyclable wrappers over the shared raw-array
  pool; `NonRecycling` is the default for comparison.
- `ApplicationByteLedger` / `ApplicationLivenessWatchdog` — test-local executable composition
  examples (byte-weighted admission, phase/physical accounting, consumer credit lifetime, external
  normal-I/O liveness).
- `LowLevelSocketHarness` — assembles all of the above.
- `TestWait` — bounded waits so hanging paths fail instead of deadlocking the runner.
- `LowLevelTcpSocketSuiteAttribute` — fixture-level suite category (see below).

## Categories

Categories are hierarchical by naming convention: the suite root is `LowLevelTcpSocket` and every
facet nests under it as `LowLevelTcpSocket.<facet>` (NUnit categories are flat strings, so this is
what makes explorers and filters present them together). A facet-tagged test carries the root
through its fixture and the facet on the method.

- `TestCategories.LowLevelTcpSocket` (via `[LowLevelTcpSocketSuite]`) — suite root, inherited by
  every fixture; include or exclude the whole suite with `--filter "TestCategory=LowLevelTcpSocket"`.
- `TestCategories.Allocation` (`LowLevelTcpSocket.Allocation`) and `TestCategories.Load`
  (`LowLevelTcpSocket.Load`) — calibrated managed-allocation probes/baselines and longer
  throughput/latency/retention probes. Both are calibrated for Release and must be run there; the
  Debug correctness run excludes them.

Real loopback-socket checks (real TCP, real SPSC source, installed pool) are the methods of
`Composition/LoopbackSocketTests`; they run in the normal suite and need no dedicated category.

Measurement fixtures: `AllocationMeasurer` (warmup + per-thread counter + thread-migration check),
`NoCopyReceiveConsumer` (counts bytes/ops without copying or storing payload, releases in
`finally`). Baselines/probes print warmed per-operation bytes and throughput/latency percentiles;
exact byte figures are environment-specific, not universal budgets.
