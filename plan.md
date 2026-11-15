# RawUnreliable → RawReliable architecture conversion

Implementation plan. Incremental stages, each independently verifiable.

---

## 1. Goal

Convert `RawUnreliableTransport` and every derived class to the architectural relation used by
`RawReliableTransport`:

- the **endpoint** owns its lifecycle, its serial queue, its handler callbacks, and (where possible)
  its carrier;
- the **transport** is a factory + registry + carrier lifecycle owner;
- the transport observes `IRawEndpointImp.Stopped` to detach endpoints and remove routes.

## 2. Non-goals (locked)

- No connection semantics: no `OnConnected`/`OnDisconnected`, no connect/disconnect stages.
- No framing byte, no ping/pong, no keep-alive, no idle watchdog. **Wire format is unchanged.**
- No admission counters, no session ticker.
- No public API renames of the **concrete** transport classes (`RawUnreliableAckDirectClient`, …),
  their namespaces, transport names, or factory bindings.
- The public **abstract base types** are replaced or removed: `RawUnreliableTransport`,
  `RawUnreliableClientTransport`, `RawUnreliableServerTransport<TFactory>`, the delegate-proxy
  `RawUnreliableEndpoint`, and the two dead `RawUnreliableAckServerTransport` /
  `RawUnreliableNoAckServerTransport` classes. This API change is intentional and scoped to the
  abstract layer.
- No changes to `RawTransport`.

## 3. Locked decisions

| # | Decision |
|---|---|
| D1 | Structural only. |
| D2 | `RawUnreliableEndpoint : RawEndpoint`, plus a `RawUnreliableServerEndpoint` middle layer. |
| D3 | `IRawUnreliableEndpointImp`, `IRawUnreliableClientEndpointImp`, `IRawUnreliableServerEndpointImp` under `Pontifex/Implementations/Endpoints/Raw/Unreliable/`. No public client/server marker interfaces. |
| D4 | Client endpoint owns its carrier; server transport owns the shared socket and gives each session a carrier/send channel. |
| D5 | Per-endpoint serial queues; factory and route creation serialized by a transport lock. Callbacks for different sessions may run concurrently; callbacks within one session are serialized. |
| D6 | Endpoint inbox + published "ready" route slot. Trigger buffered in the endpoint. `OnStarted` raised by the endpoint's own flush action. Inbox is capped. |
| D7 | UDP client: endpoint binds in `BeginStart`; failure is reported asynchronously (reliable style). Direct client: channel acquired lazily in `DoSendRaw`. Both servers: transport binds, failure still fails `Start`. Carrier-level send errors are best effort: drop. |
| D8 | `RawUnreliableServerTransport<TFactory>` kept; `RawUnreliableClientTransport` non-generic; `Init` stays on the four server leaves; client `Init` moves into the base. |
| D9 | Conformance gates relocated as described in §7. |
| D10 | Public transport class names/namespaces unchanged. `RawUnreliableEndpoint.Stop` becomes a one-shot **request**: the latch and `IsValid` turn over immediately, but the call may execute the transition on the caller's thread and therefore may block at an armed gate (same `SerialCommandQueue` drain behaviour as `Send`). `IsValid => IsRunning`. |
| D11 | Byte budgets: **local** per endpoint for clients, **shared/global** per transport for servers; the global counter is maintained by `OnPendingSendChanged`. |
| D12 | In-place staged refactor with a temporary legacy shim. Every **stage** ends green; inside Stage 5 the two carriers are one behaviour flip (see §8.3), so intermediate commits within that stage are not independently green. |

---

## 4. Target architecture

### 4.1 Transports

```
RawTransport
└── RawUnreliableTransport                       thin: marker + conformance cast
    ├── RawUnreliableClientTransport             Init(IRawUnreliableHandler), Start/StopCarrier,
    │                                            TryStart: CreateEndpoint(handler) → AttachEndpoint → Start
    │                                            (with start-in-progress latch, mirroring
    │                                            RawReliableClientTransport), OnStarted replays a latched
    │                                            endpoint stop reason, Stopped → Stop, fallback OnStopped
    └── RawUnreliableServerTransport<TFactory>   TryInitializeServer(factory), Start/StopCarrier,
                                                 CreateEndpoint(handler, source), InvokeFactory(factory, source, trigger),
                                                 route registry + admission lock, Stopped → unroute, stop cascade
```

### 4.2 Endpoints

```
RawEndpoint
└── RawUnreliableEndpoint                        abstract; IRawUnreliableEndpointImp
    │                                            inbox (capped), Deliver, BeginStart→FinishStart, flush,
    │                                            BeginStop, Stop(reason?) one-shot, IsValid => IsRunning,
    │                                            CanSendNow (NotConnected→Error), DoSendRaw, GetControls
    ├── RawUnreliableDirectClientEndpoint        owns Channel (lazy)
    ├── RawUnreliableUdpClientEndpoint           owns Socket (bind in BeginStart)
    └── RawUnreliableServerEndpoint              abstract; shared SendBudget, pending trigger
        ├── RawUnreliableDirectServerEndpoint    holds Channel from the accepting transport
        └── RawUnreliableUdpServerEndpoint       holds UdpSessionChannel (shared sender + route)
```

No `RawUnreliableClientEndpoint` abstract class: it would hold no delta.

Implementation notes forced by the existing bases:

- `RawEndpoint._handler` is private, so `RawUnreliableEndpoint` keeps its own
  `protected IRawUnreliableHandler Handler` reference and uses it to raise `OnStarted`.
- `IRawEndpointImp.Stop(reason, tryBeGentle)` is non-virtual and cannot be intercepted. The
  one-shot entry is therefore `IRawUnreliableEndpoint.Stop(reason?)`:
  1. return `false` if the endpoint is already invalidated (an `_invalidated` flag set in
     `BeginStop`, in a `FailHard` override, and in the `Stopped` handler) or if its CAS latch is
     already taken;
  2. otherwise take the latch, replace a null `reason` with `new Unknown(TransportName)`, call the
     base `Stop(mappedReason, false)`, return `true`. The mapping is mandatory: `RawEndpoint.Stop`
     uses `Interlocked.CompareExchange(ref _stopReason, reason, null)`, so a null reason would be
     latched as "no stop reason" and later delivered to the handler and `Stopped` subscribers as
     `null`.

  There is **no `IsRunning` guard**: `RawEndpoint.Stop` already implements the `Constructed` and
  `Starting` cases correctly (`RawEndpoint.cs:469-478`), and stopping a never-started or
  still-starting endpoint is required to release the inbox and remove the route. **The transport
  uses this method for its stop cascade** (never `IRawEndpointImp.Stop`). Because an external stop
  can be latched while `Starting` and be promoted by `FinishStart` before its queued action runs,
  the queued action is made idempotent (§5.2 item 4).
- A handler wrapper is passed to the base `RawEndpoint` constructor. It is a **three-state gate**
  that forwards `OnStopped` only when this endpoint's own `OnStarted` returned successfully. It
  forwards `OnReceived` unconditionally. This preserves `IRawHandler`'s documented rule
  ("provided `OnStarted()` completed successfully"), `Specification.md` §6.3/§7.1/§7.3, and the
  legacy `OnStartedCompleted` guard, without touching `RawEndpoint`. It covers both the throwing
  `OnStarted` and the stop-while-starting path where `OnStarted` never ran. `RaiseStopped` is
  unaffected, so route removal and transport stop still work.
- Internal failure paths (`BeginStart` carrier failure, `OnStarted` throw) use the base
  `Stop(reason, false)`/`FailHard`. The public one-shot entry is for callers and for the transport's
  stop cascade.
- `RawUnreliableServerEndpoint` overrides `Send` only for the best-effort global budget check; the
  corresponding release happens in `OnPendingSendChanged`, so `DoSendRaw` needs no sealing.

---

## 5. Shared-base changes (explicit review)

### 5.1 `Pontifex/Implementations/Raw/RawTransport.cs` — **no changes**

### 5.2 `Pontifex/Implementations/Endpoints/Raw/RawEndpoint.cs` — four edits, one new member

1. New field: `private int _pendingSendMessages;`
2. New member:

```csharp
/// <summary>
/// Reports the signed change of this endpoint's pending outbound reservation: +size/+1 when bytes
/// are reserved for a buffer, -size/-1 when the reservation is released (transmitted, dropped by a
/// stop, aborted by DoSendRaw throwing, or rolled back because admission failed). Emitted before
/// the buffer enters the serial queue, so a rejected admission appears as a matching +/− pair.
/// May be invoked from different threads; overrides must aggregate atomically and must not throw.
/// </summary>
protected virtual void OnPendingSendChanged(int deltaBytes, int deltaMessages) { }
```

3. Call sites: **every point where `_totalBuffersSize` is mutated**, so the invariant
   "sum of deltas == current pending bytes" holds on all paths. `_pendingSendMessages` moves in
   lockstep. The five sites in `ScheduleToSend`/`_sendAction` are:
   - reservation next to `Interlocked.Add(ref _totalBuffersSize, bufferSize)`:
     `+bufferSize, +1`;
   - overflow rollback (`_totalBuffersSize > _bufferCapacityBytes`): `-bufferSize, -1`;
   - `_actionQueue.Schedule` returning `false`: `-bufferSize, -1`;
   - the `onFailToRun` callback passed to `Schedule` (accepted but never executed):
     `-bufferSize, -1`;
   - `_sendAction`'s `finally` (transmitted, dropped by stop, or `DoSendRaw` threw):
     `-size, -1`.

   The reservation notification must be emitted **before** `_actionQueue.Schedule`, because
   `SerialCommandQueue.Schedule` drains the backlog on the calling thread
   (`Pontifex/Utils/SerialCommandQueue.cs:96-104`); emitting after it would deliver the negative
   delta first. A rejected admission therefore produces a matching `+/−` pair that nets to zero;
   the hook documents that it reports reservations, not only successful admissions.

4. Idempotent stop action. In `Stop(StopReason, bool)`'s queued action, change the
   `case State.Stopping: case State.Stopped:` branch from
   `throw new InvalidOperationException(...)` to `break`. Rationale: the `_stopReason` CAS
   guarantees at most one queued stop action. `Stopping` is reached legitimately when an external
   `Stop` is latched while the endpoint is `Starting` and `FinishStart` promotes it
   (`RawEndpoint.cs:304-311`) before that queued action runs; `Stopped` is reached when the endpoint
   failed hard. In both cases teardown has already been started and the queued action is a no-op.
   Without this edit, a transport stop that races a client/server endpoint start logs an
   `InvalidOperationException`, fails the endpoint hard and skips the handler's `OnStopped`.
   The two genuinely invalid paths (`FinishStart`/`FinishStop` out of order) keep throwing.

Calls are wrapped in `try { ... } catch (Exception ex) { Log.wtf(ex); }` so a faulty override cannot
break buffer accounting.

No other member of `RawEndpoint` changes. `OnStarted`/`OnStopping` hooks are **not** added.

---

## 6. Buffering and backpressure model

### 6.1 Outbound (send)

| Scope | Mechanism | Rejection |
|---|---|---|
| Client | `RawEndpoint`'s local byte budget (`bufferCapacityBytes`) | `BufferOverflow` |
| Server | shared `SendBudget` (bytes + messages) across all sessions, checked in the server endpoint's `Send` override; the endpoint's local budget uses the same cap so it never binds first | `BufferOverflow` |

`SendBudget` (new, `Pontifex/Implementations/Endpoints/Raw/Unreliable/SendBudget.cs`):

```csharp
public sealed class SendBudget
{
    public SendBudget(long maxBytes, int maxMessages);
    public bool CanAccept(int size);                    // best-effort snapshot, race-tolerant
    public void Apply(int deltaBytes, int deltaMessages); // Interlocked aggregation
}
```

The check is best effort by design: concurrent senders may overshoot by a bounded amount. That is
accepted for unreliable mode.

Starting caps, per carrier, in the existing `*Info` classes:

```
SendBudgetBytes    = MessageMaxByteSize * QueueCapacity      // Direct: 1 MiB * 100; UDP: 490 B * 100
SendBudgetMessages = QueueCapacity                           // 100
```

Calibrate in Stage 5 against `ConcurrentTrySend_DeliversEveryAcceptedMessage` (32 concurrent sends,
all must return `Ok`) and `ReliableLink_AcceptsAndDeliversMessageAtExactSizeLimit`.

### 6.2 Inbound pre-start inbox

- Caps: `QueueCapacity` messages AND `SendBudgetBytes` bytes; excess is **dropped silently** (no log).
- Trigger: passed through the server endpoint constructor and stored as the **first** inbox element,
  so it is never subject to the cap. The exact creation order is:
  `factory → construct endpoint(with trigger) → publish route slot → BeforeHandlerStartedGate → Start()`.
  The route is not published before construction, so no message can overtake the trigger.
- The flush action emits the pending trigger first, then drains the inbox in FIFO order.
- The inbox is released on **every** terminal path by subscribing to `Stopped` in the endpoint
  constructor (covers stop-during-start, `OnStarted` throwing, `FailHard`, stop of a never-started
  endpoint, and transport stop).
- `Deliver` is driven by an explicit inbox state, **not** by `IsRunning`:
  - `Closed` (invalidated) → release the buffer;
  - `Buffering` (the flush has not run) → enqueue (subject to the caps);
  - `Open` (the flush has run) → `base.ProcessInboundMessage`.
  `IsRunning` is not a sufficient key: it becomes true when `FinishStart`'s action runs, which is
  before the flush action. A message delivered in that window would be scheduled directly, while
  the buffered messages would only be scheduled later by the flush, so the new message would
  overtake the trigger. The flush transitions `Buffering → Open` and drains the inbox **while
  holding the inbox lock**, which places every buffered message on the endpoint queue before any
  later `Deliver`. The `Stopped` release transitions any state to `Closed` under the same lock.

### 6.3 Carrier queues

`UdpAsyncSender` keeps its 10 000-slot queue. Its `BufferOverflow` cannot be reported after
admission; it is dropped silently.

**Logging policy: dropped packets are never logged.** Exception logging stays as is.

---

## 7. Conformance gates (relocated)

| Gate | New location |
|---|---|
| `BeforeHandlerFactoryGate` | server transport, before `InvokeFactory` |
| `BeforeHandlerStartedGate` | server transport, after route publication and before `endpoint.Start()` |
| `BeforeEndpointStopStateTransitionGate` | `RawUnreliableEndpoint.BeginStop` |
| `BeforeHandlerStoppedGate` | `RawUnreliableEndpoint.BeginStop`, immediately before `FinishStop()` |
| `BeforeSendCommitGate` / `AfterSendCommitGate` | concrete endpoint `DoSendRaw` around the carrier call |
| `AfterReceivedGate` | `RawUnreliableEndpoint` inbound override, before delivery |
| `TryMakeReliableForDebug` | extracted to a carrier-implemented `IRawUnreliableDebugReliability` interface; the conformance control prefers it and falls back to the legacy cast until Stage 6 |

---

## 8. Tests and documentation to add or change

### 8.1 `Tests/Pontifex.Tests/Endpoints` (Abstract Test Suit)

- Root `RawEndpoint` layer: new tests for `OnPendingSendChanged` (positive on reservation, negative
  on transmit, on drop-by-stop and on `DoSendRaw` throwing, and a matching `+`/`−` pair for each
  admission-rejection path: local budget overflow and `Schedule` returning false; a throwing
  override is swallowed and logged) and for the idempotent queued stop action (stop latched while
  `Starting`, promoted by `FinishStart` before the action runs). Update `Coverage.md`.
- New layer `RawUnreliableEndpoint` at `Tests/Pontifex.Tests/Endpoints/RawUnreliable`
  (parent `RawEndpoint`): inbox FIFO, trigger-first, inbox caps, drop on overflow, running delivery
  after the flush, delivery between `FinishStart` and the flush stays behind the trigger, delivery
  after invalidation releases, release on
  stop-before-start, `OnStarted` once, `OnStarted` throwing, stop-while-starting (no `OnStopped`),
  `Stop()` one-shot (the request latches immediately; the call may run the transition inline),
  a null `Stop` reason is mapped to `Unknown`, `IsValid => IsRunning`, `NotConnected → Error`,
  `InvalidMessage`, `MessageTooBig`,
  `BufferOverflow`, `GetControls` exposes the endpoint conformance control, `Stopped` raised once.
- New layer `RawUnreliableServerEndpoint` at `.../RawUnreliable/Server` (parent
  `RawUnreliableEndpoint`): pending trigger delivery order, shared budget accept/reject,
  budget release on completion, global counter accuracy with one endpoint.
- Update `Tests/Pontifex.Tests/Endpoints/README.md`: selected inheritance tree, contract-reference
  links, layer table rows, directory layout, and remove the current exclusion of
  `RawUnreliableEndpoint` from scope. Each new `Coverage.md` links its contract (COVERAGE-03).

### 8.2 `Tests/Pontifex.Tests/Transports` (Abstract Test Suit)

- New layer `RawUnreliableTransport` at `Tests/Pontifex.Tests/Transports/Raw/Unreliable`
  (parent `RawTransport`). Its delta is small but real and must be tested, otherwise the layer is
  empty (STRUCT-06): `RawUnreliableTransport_ExposesUnreliableRawTransportContract` (assignability to
  `IRawUnreliableTransport`) and `GetControls_ExposesRawUnreliableTransportConformanceControl`
  (typed `Conformance` property yields `IRawUnreliableTransportConformanceControl`), mirroring
  `Tests/Pontifex.Tests/Transports/Raw/Reliable/RawReliableTransportTests.cs`.
- New layer `RawUnreliableClientTransport` at `.../Raw/Unreliable/Client`.
- New layer `RawUnreliableServerTransport` at `.../Raw/Unreliable/Server`.
- Update `Tests/Pontifex.Tests/Transports/README.md`: selected inheritance tree, contract-reference
  links, layer table, directory layout, out-of-scope section, categories. Each new `Coverage.md`
  links its contract (COVERAGE-03).
- Update `Tests/Pontifex.Tests/Transports/Raw/Coverage.md`: move the `RawUnreliableTransport`,
  `RawUnreliableClientTransport` and `RawUnreliableServerTransport<TFactory>` rows out of
  "Planned" (`:23-27`) when their layers land; remove the two dead Ack/NoAck rows when those
  classes are deleted.
- Coverage: client init gate, endpoint creation/attach/start in `TryStart`, endpoint stop during
  start replayed after start, endpoint `Stopped` → transport stop, fallback `OnStopped` when no
  endpoint was created, carrier stop ordering; server route creation (factory null/throw),
  single factory call per route under concurrency, trigger ordering, `Stopped` → unroute,
  route recreation, stop cascade, transport-stop during creation.

### 8.3 Conformance renegotiation

| Test | Change |
|---|---|
| `ServerCallbacks_AreGloballySerializedAcrossClients` | **Over-asserts, not a contract change.** `Specification.md` §11 already permits callbacks of different server endpoints to run concurrently and only requires per-endpoint serialization. Relax the test to per-endpoint serialization and rename it. |
| `Client_OnStartedThrows_StopsClientTransport_WithoutHandlerOnStopped` | **No change.** `Specification.md` §6.3 MUST NOT ("the throwing handler MUST NOT receive `OnStopped`") is preserved by the handler wrapper; the endpoint stops the transport as before. |
| `Server_HandlerOnStartedThrows_DropsTrigger_AndRecreatesRouteLater` | **No change.** §7.3 MUST NOT is preserved; `throwingHandler.StoppedReason` stays `null`, the route is recreated. |
| `BeforeEndpointStopStateTransitionGate_BlocksEndpointInvalidation` | `Stop()` no longer waits for the transition **by contract**, but it may still run the transition on the caller's thread when it starts a queue drain, so it can block at an armed gate (the `SerialCommandQueue` behaviour cited in §5.2). Rewrite as: arm the gate → `Task.Run(() => endpoint.Stop())` → await `gateReached` → assert `IsValid` is false and a second `Stop()` returns false → reset → assert the task returns `true` and the endpoint is terminal. |
| `BeforeHandlerStoppedGate_BlocksOnStopped` | Re-read and re-document the ordering. |
| `TrySend_AfterNormalStop_ReturnsError` | Keep; `NotConnected` must be mapped to `Error`. |
| `TryMakeReliable_*` | Keep working; add a guard test that fails if the suite starts ignoring instead of running. |
| Logging | Record in `Tests/Pontifex.Transports.Tests/Raw/Unreliable/Coverage.md` (and the `Ack`/`NoAck` variant coverage files) that dropped packets are silent by design. There is no suite `README.md` in `Tests/Pontifex.Transports.Tests` and none should be added. |

**Shared-suite constraint.** `RawUnreliableConformanceTests<TServer>` is one generic fixture
instantiated by four adapters (Ack/NoAck × Direct/UDP); the same assertions run against both
carriers. A behavioural expectation cannot be "new" for Direct and "legacy" for UDP while the suite
is green. Consequently carrier conversion is a **single stage** (Stage 5), and §8.3 must land with
it. A per-adapter expectation seam would keep the stages smaller but introduces two expectation
sets for a transient state; rejected.

**Normative specification.** `Pontifex/Abstractions/Transports/Raw/Unreliable/Specification.md`
(adopted by `Ack/Specification.md` and `NoAck/Specification.md`) is the contract reference for this
suite. The plan deliberately preserves three of its rules: per-endpoint callback serialization with
cross-endpoint concurrency (§11), and the two MUST-NOT `OnStopped` rules for a handler whose
`OnStarted` threw (§6.3, §7.1, §7.3). It must still be re-read in Stage 5c/7 for wording that names
the actor (transport vs endpoint) and for the relocated conformance gates (§12). `Tests/RULES.md`
EVOL-09 requires any contract edit to land before or with the affected tests.

---

## 9. Stages

Verification commands (run at the end of every stage):

```
dotnet build Pontifex.sln -v m
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj
dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj
dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj
dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj
```

---

### Stage 0 — Baseline

- [x] Run all verification commands; record pass/ignore counts.
- [x] Confirm the unreliable conformance suite is **not** ignoring (`TryMakeReliable` returns true
      for both Direct and UDP today).

**Exit:** green baseline; counts recorded in this file or the PR description.

**Recorded baseline (2026-11-15, `f850b75`):** build 0 errors / 21 warnings;
`Pontifex.Tests` 610 passed / 0 skipped; `Pontifex.Transports.Tests` 238 passed / 67 skipped
(the skips are the `TryAck_*` group, unrelated to unreliable), `--filter Unreliable` runs
226 / 0 skipped; `Pontifex.Transport.Direct.Tests` 50 passed; `Pontifex.Transport.Net.Tests`
280 passed; `Pontifex.Factory.Tests` 99 passed. Unreliable conformance suite is not ignoring.

---

### Stage 1 — `RawEndpoint`: pending-send counter and hook

Files:
- `Pontifex/Implementations/Endpoints/Raw/RawEndpoint.cs` (see §5.2)
- `Tests/Pontifex.Tests/Endpoints/RawEndpointTests.cs`
- `Tests/Pontifex.Tests/Endpoints/Coverage.md`

Tasks:
- [x] Add `_pendingSendMessages`, `OnPendingSendChanged`, and the **five** notification sites
      enumerated in §5.2.
- [x] Make the queued stop action idempotent for `Stopping`/`Stopped` (§5.2 item 4).
- [x] Add tests: positive delta on reservation; negative delta on transmit; negative delta on
      drop-by-stop; negative delta on `DoSendRaw` throwing; matching `+`/`−` pairs for the local
      budget overflow and `Schedule`-returned-false paths; net-zero after drain; a throwing override
      is swallowed and logged; a stop latched while `Starting`, promoted by `FinishStart` before the
      queued stop action runs, yields no exception, no `FailHard`, one `BeginStop` and one handler
      notification.
- [x] Update the `RawEndpoint` coverage table.

**Exit:** build green; existing endpoints/transports/conformance suites unchanged; new tests green.

---

### Stage 2 — Unreliable endpoint layer (additive)

Files (new):
```
Pontifex/Implementations/Endpoints/Raw/Unreliable/IRawUnreliableEndpointImp.cs
Pontifex/Implementations/Endpoints/Raw/Unreliable/IRawUnreliableClientEndpointImp.cs
Pontifex/Implementations/Endpoints/Raw/Unreliable/IRawUnreliableServerEndpointImp.cs
Pontifex/Implementations/Endpoints/Raw/Unreliable/RawUnreliableEndpoint.cs
Pontifex/Implementations/Endpoints/Raw/Unreliable/RawUnreliableServerEndpoint.cs
Pontifex/Implementations/Endpoints/Raw/Unreliable/SendBudget.cs
```
Renamed:
```
Pontifex/Implementations/Raw/Unreliable/RawUnreliableEndpoint.cs
  -> Pontifex/Implementations/Raw/Unreliable/LegacyRawUnreliableEndpoint.cs   (public)
```
The renamed type stays **public**: `RawUnreliableWorkItem` exposes it in public fields and factory
methods, and the four carrier `_Common` bases declare
`protected override SendResult SendToCarrier(RawUnreliableEndpoint endpoint, …)` in separate
assemblies. `Pontifex.csproj` grants `InternalsVisibleTo` only to `Pontifex.Tests`, so an internal
type would not compile. It is deleted in Stage 6.
Tests (new):
```
Tests/Pontifex.Tests/Endpoints/RawUnreliable/Coverage.md
Tests/Pontifex.Tests/Endpoints/RawUnreliable/RawUnreliableEndpointTests.cs
Tests/Pontifex.Tests/Endpoints/RawUnreliable/Harness/...
Tests/Pontifex.Tests/Endpoints/RawUnreliable/Server/Coverage.md
Tests/Pontifex.Tests/Endpoints/RawUnreliable/Server/RawUnreliableServerEndpointTests.cs
Tests/Pontifex.Tests/Endpoints/RawUnreliable/Server/Harness/...
Tests/Pontifex.Tests/Endpoints/README.md                                   (layer table, layout, scope)
```
Tasks:
- [x] `IRawUnreliableEndpointImp` adds `void Deliver(UnionDataList message)`.
- [x] `RawUnreliableEndpoint`: inbox + caps, `Deliver` driven by the `Buffering`/`Open`/`Closed`
      inbox state with a shared inbox lock,
      `BeginStart` → `FinishStart` + scheduled flush, `OnStarted` raised from flush,
      `BeginStop` (gate) → `FinishStop` (gate), `Stop(reason?)` one-shot with the `_invalidated`
      check and **no `IsRunning` guard**, `IsValid => IsRunning`, `CanSendNow` mapping,
      `GetControls` adds the endpoint conformance control, `DoSendRaw` abstract,
      own `protected IRawUnreliableHandler Handler` reference, a three-state handler wrapper that
      forwards `OnStopped` only after a successful `OnStarted`, and inbox release from a `Stopped`
      subscription installed in the constructor.
- [x] `RawUnreliableServerEndpoint`: trigger through the constructor (first inbox element), shared
      `SendBudget`, `Send` override with the best-effort global check.
- [x] `SendBudget`: `CanAccept(size)` (bytes and messages), `Apply(deltaBytes, deltaMessages)`
      (Interlocked).
- [x] Rename the legacy proxy to public `LegacyRawUnreliableEndpoint`; update every reference:
      `RawUnreliableTransport`, `RawUnreliableWorkItem`, and the four carrier `SendToCarrier`
      signatures (plus their `SerializedCallbackQueue<...>` field types).
- [x] Endpoint tests and coverage as in §8.1. Extend `Tests/Pontifex.Tests/Endpoints/Harness/
      ManualSerialActionQueue.cs` with a single-step drain and expose it on the new
      `TestRawUnreliableEndpoint`, so a `Deliver` between `FinishStart`'s action and the flush action
      can be produced deterministically (the `Buffering`/`Open` ordering case).

**Exit:** all suites green; new endpoint layers green; nothing in production references the new
endpoint types yet.

---

### Stage 3 — Rename the legacy transport hierarchy (mechanical)

Files:
- `Pontifex/Implementations/Raw/Unreliable/RawUnreliableTransport.cs` → `LegacyRawUnreliableTransport.cs`
- `Pontifex/Implementations/Raw/Unreliable/RawUnreliableClientTransport.cs` → `LegacyRawUnreliableClientTransport.cs`
- `Pontifex/Implementations/Raw/Unreliable/RawUnreliableServerTransport.cs` → `LegacyRawUnreliableServerTransport.cs`
- `Pontifex/Implementations/Raw/Unreliable/RawUnreliableTransportConformanceControl.cs` (cast target)
- `Pontifex/Implementations/Raw/Unreliable/Ack/RawUnreliableAckServerTransport.cs` (base type)
- `Pontifex/Implementations/Raw/Unreliable/NoAck/RawUnreliableNoAckServerTransport.cs` (base type)
- `Transports/Pontifex.Transport.Direct/Raw/Unreliable/_Common/RawUnreliableDirect*Transport.cs`
- `Transports/Pontifex.Transport.Net/Udp/Raw/Unreliable/_Common/RawUnreliableUdp*Transport.cs`

Tasks:
- [x] Rename all three legacy bases. **Public rename only** — the four carrier `_Common` bases are
      public and derive from the client/server bases, so `internal` is not viable (CS0060).
- [x] Update `RawUnreliableTransportConformanceControl.TryMakeReliable` to cast to
      `LegacyRawUnreliableTransport` (it currently casts to `RawUnreliableTransport`).
- [x] Repoint the two dead abstract Ack/NoAck server bases at
      `LegacyRawUnreliableServerTransport<TFactory>` (one token each); they are deleted in Stage 6.
- [x] Free the canonical names `RawUnreliableTransport`, `RawUnreliableClientTransport`,
      `RawUnreliableServerTransport<TFactory>` for Stage 4.
- [x] No behaviour change; no test change.

**Exit:** build green; every suite green; only `Legacy*` renames and their references are in the diff.

---

### Stage 4 — New thin transport bases (additive)

Files (new):
```
Pontifex/Implementations/Raw/Unreliable/RawUnreliableTransport.cs          (thin marker, canonical name)
Pontifex/Implementations/Raw/Unreliable/RawUnreliableClientTransport.cs
Pontifex/Implementations/Raw/Unreliable/RawUnreliableServerTransport.cs
Pontifex/Implementations/Raw/Unreliable/IRawUnreliableDebugReliability.cs  (public; carrier-implemented)
```
Changed:
```
Pontifex/Implementations/Raw/Unreliable/RawUnreliableTransportConformanceControl.cs
```
Tests (new):
```
Tests/Pontifex.Tests/Transports/Raw/Unreliable/Coverage.md
Tests/Pontifex.Tests/Transports/Raw/Unreliable/RawUnreliableTransportTests.cs
Tests/Pontifex.Tests/Transports/Raw/Unreliable/Harness/...
Tests/Pontifex.Tests/Transports/Raw/Unreliable/Client/Coverage.md
Tests/Pontifex.Tests/Transports/Raw/Unreliable/Client/RawUnreliableClientTransportTests.cs
Tests/Pontifex.Tests/Transports/Raw/Unreliable/Client/Harness/...
Tests/Pontifex.Tests/Transports/Raw/Unreliable/Server/Coverage.md
Tests/Pontifex.Tests/Transports/Raw/Unreliable/Server/RawUnreliableServerTransportTests.cs
Tests/Pontifex.Tests/Transports/Raw/Unreliable/Server/Harness/...
Tests/Pontifex.Tests/Transports/README.md                                   (tree, layer table, layout)
```
Tasks:
- [x] Declare `RawUnreliableTransport : RawTransport, IRawUnreliableTransport`. The legacy class omits
      the interface; the new marker must expose it, because the layer test asserts the assignability
      that `RawReliableTransport` has (`RawReliableTransport.cs:6`).
- [x] Client base: `Init`, `TryStart` (init gate → `StartCarrier` → `CreateEndpoint` → attach →
      `endpoint.Start`) with a start-in-progress latch and a latched pending stop reason, mirroring
      `RawReliableClientTransport`; `OnStarted` only replays a latched stop; `Stopped` → transport
      `Stop`; detach and fallback `handler.OnStopped` when no endpoint was created; carrier stop
      ordering. `CreateEndpoint` throwing is caught and logged, the handler receives
      `OnStopped(ExceptionFail)`, and `TryStart` returns false — this is the reachable
      "no endpoint created" path for the fallback coverage item. Creating the endpoint in
      `OnStarted` is forbidden: `AnyTransport.Start` has no `try/catch` around `OnStarted`, so a
      throw there would leave the transport half-started.
- [x] Server base: `TryInitializeServer`, `TryStart`, route registry with admission lock and
      published ready slot (creation order per §6.2), factory null/throw handling, gate placement,
      trigger hand-off, stop cascade, `Stopped` → unroute, global `SendBudget` ownership and limits
      hook. After the admission lock is released and before `Start()`, re-check
      `IsStopping`/`IsStarted`; if the transport is stopping, remove the slot and stop the new
      endpoint so the trigger is released.
- [x] `IRawUnreliableDebugReliability` is public with a public `bool TryMakeReliableForDebug()`.
      Stage 4 keeps **both** lookup paths in `RawUnreliableTransportConformanceControl`
      (`_owner is IRawUnreliableDebugReliability d ? d.TryMakeReliableForDebug() :
      ((LegacyRawUnreliableTransport)_owner).TryMakeReliableForDebug()`), because the four carriers
      still implement the legacy `protected internal` abstract member. The fallback is deleted in
      Stage 6. Without the dual path every `EnableReliable(...)` call throws `InvalidCastException`
      for all four adapters in Stage 4.
- [x] Transport tests and coverage as in §8.2; update `Tests/Pontifex.Tests/Transports/README.md`
      and `Tests/Pontifex.Tests/Transports/Raw/Coverage.md`.

**Exit:** build green; new transport layers green; legacy carriers untouched and all suites green.

---

### Stage 5 — Carrier conversion (Direct + UDP, single behaviour flip)

**Why one stage.** `RawUnreliableConformanceTests<TServer>` is a single generic fixture instantiated
by four adapters (Ack/NoAck × Direct/UDP), so the same assertions run against both carriers. A
behaviour flip is visible to the shared suite; a "Direct new / UDP legacy" intermediate state cannot
be green (§8.3). The work is listed in execution order for reviewability but verified as one stage.

**5a — Direct carrier**
Files:
- `Transports/Pontifex.Transport.Direct/Raw/Unreliable/_Common/RawUnreliableDirectClientTransport.cs`
- `Transports/Pontifex.Transport.Direct/Raw/Unreliable/_Common/RawUnreliableDirectServerTransport.cs`
- `Transports/Pontifex.Transport.Direct/Raw/Unreliable/Ack/...` (leaves: base type, `Init` removal)
- `Transports/Pontifex.Transport.Direct/Raw/Unreliable/NoAck/...`
- New: `.../_Common/RawUnreliableDirectClientEndpoint.cs`, `.../_Common/RawUnreliableDirectServerEndpoint.cs`
- `.../RawUnreliableAckDirectInfo.cs`, `.../RawUnreliableNoAckDirectInfo.cs` (budget constants)

Tasks:
- [ ] Client transport: configuration only; `CreateEndpoint` returns the client endpoint. Delete
      `_callbackQueue` and lazy-connect logic from the transport (moves into the endpoint).
- [ ] Keep `SetDeliverySystem` state on the transport and pass the effective client/server delivery
      systems (honouring `_askedForReliableDelivery`) to `CreateEndpoint`; the endpoint applies them
      when it creates the channel. `TryMakeReliableForDebug` keeps its current rule
      (`IsStarted == false`).
- [ ] Client endpoint: owns the `Channel`, lazy connect inside `DoSendRaw`, missing server → drop,
      channel disposal → `Stop`.
- [ ] Server transport: keep the channel registry; `CreateEndpoint(handler, source)` passes the
      channel; `SendToCarrier` hook replaced by the endpoint's `DoSendRaw`. If the channel is no
      longer registered, `CreateEndpoint` declines (trigger released, no route published).
- [ ] Leaves derive from the new bases; client `Init` removed from leaves.

**5b — UDP carrier**
Files:
- `Transports/Pontifex.Transport.Net/Udp/Raw/Unreliable/_Common/RawUnreliableUdpClientTransport.cs`
- `Transports/Pontifex.Transport.Net/Udp/Raw/Unreliable/_Common/RawUnreliableUdpServerTransport.cs`
- New: `.../_Common/RawUnreliableUdpClientEndpoint.cs`, `.../_Common/RawUnreliableUdpServerEndpoint.cs`,
  `.../_Common/UdpSessionChannel.cs`
- `.../Ack/RawUnreliableAckUdpInfo.cs`, `.../NoAck/RawUnreliableNoAckUdpInfo.cs` (budget constants)
- `.../Ack/RawUnreliableAckUdpClient.cs`, `.../Server.cs`, NoAck equivalents (base types)

Tasks:
- [ ] Client: socket moves into the endpoint; bind in `BeginStart`; failure → base
      `Stop(reason, false)` + `FinishStart(...)`; receiver filters the configured remote endpoint.
- [ ] Server: transport keeps socket/receiver/sender and the route → `IpEndPoint` map;
      `CreateEndpoint` builds a `UdpSessionChannel` and a server endpoint.
- [ ] `DoSendRaw` per endpoint; remove the transport-level `SendToCarrier`/`OnCarrierInbound` plumbing.
- [ ] `TryMakeReliableForDebug` implemented through `IRawUnreliableDebugReliability` in all four
      carriers; the legacy abstract member is no longer overridden.

**5c — Conformance and calibration**
- [ ] Apply every §8.3 delta in this stage.
- [ ] Review `Pontifex/Abstractions/Transports/Raw/Unreliable/Specification.md` and the
      `Ack`/`NoAck` variant specs against the new architecture; update wording that names the actor
      (transport vs endpoint) and the §12 gate placement. Do not weaken §11 or the
      §6.3/§7.1/§7.3 MUST-NOT rules — the implementation preserves them.
- [ ] Update `Tests/Pontifex.Transports.Tests/Raw/Unreliable/Coverage.md` and the
      `Ack`/`NoAck` variant coverage files (renamed tests, changed expectations, silent drops).
- [ ] Calibrate §6.1 budget caps against `ConcurrentTrySend_DeliversEveryAcceptedMessage` (32 sends,
      all `Ok`) and `ReliableLink_AcceptsAndDeliversMessageAtExactSizeLimit`.

**Exit:** all four adapters green on the new architecture; no carrier derives from `Legacy*`;
§8.3 deltas applied; no `Assert.Ignore`.

---

### Stage 6 — Delete legacy

Tasks:
- [ ] Delete `LegacyRawUnreliableTransport`, `LegacyRawUnreliableClientTransport`,
      `LegacyRawUnreliableServerTransport<TFactory>`, `LegacyRawUnreliableEndpoint`,
      `RawUnreliableWorkItem`, the dispatcher field and helpers.
- [ ] Delete the dead `RawUnreliableAckServerTransport` and `RawUnreliableNoAckServerTransport`;
      remove their two rows from `Tests/Pontifex.Tests/Transports/Raw/Coverage.md`.
- [ ] Remove the legacy dispatcher plumbing: `OnCarrierInbound`, `SendToCarrier`, `TryInitialize`,
      `DispatchWork`, and every work-item switch arm. The new route registry and its lock stay.
- [ ] Remove the legacy branch from `RawUnreliableTransportConformanceControl`; keep only the
      `IRawUnreliableDebugReliability` lookup.
- [ ] Grep gate: no `Legacy`, no `RawUnreliableWorkItem`, no `SerializedCallbackQueue` in the
      unreliable path.

**Exit:** build green; all suites green; no legacy symbols.

---

### Stage 7 — Contract docs and final conformance sweep

Tasks:
- [ ] XML docs: `IRawUnreliableEndpoint` (`Stop`, `IsValid`), `IRawUnreliableHandler.OnStarted`,
      `IRawUnreliableTransport` (start/failure semantics), `IRawUnreliableClient`/`Server` `Init`.
- [ ] Update `Tests/Pontifex.Transports.Tests/Raw/Unreliable/Coverage.md` and the `Ack`/`NoAck`
      variant coverage files for the final behaviour (silent drops, per-session serialization,
      budget scope). There is no suite `README.md` in `Tests/Pontifex.Transports.Tests`; do not add
      one.
- [ ] `Pontifex/TODO.md`: tick the conversion, add follow-ups (empty-message count cap if desired,
      D8 option E if a third variant appears).
- [ ] Full verification run; record counts; confirm no `Assert.Ignore`.
- [ ] Grep for leftovers: global serialization claims, "separate concrete hierarchy" wording in
      `Endpoints/README.md`, stale `QueueCapacity` references, and `Specification.md` /
      `Ack/Specification.md` / `NoAck/Specification.md` statements that name the deleted
      dispatcher or the old gate placement.

**Exit:** all suites green; no ignores; docs match the code.

---

## 10. Risks and rollback

| Risk | Mitigation | Rollback |
|---|---|---|
| Global budget drifts | Deltas are symmetric; the hook fires at every `_totalBuffersSize` mutation; a `RawEndpoint` test asserts equality after drain and after every rollback path | Revert Stage 1 and use per-endpoint budgets only |
| Trigger ordering | Trigger is the first inbox element, route published after construction, and `Deliver` keys on the inbox state (not `IsRunning`); covered by the existing creation-race test and new endpoint tests | Revert Stage 5 |
| Stop racing start | Idempotent queued stop action (§5.2 item 4) + endpoint test; transport stop paths use the one-shot entry | Revert Stage 1 + Stage 5 |
| Conformance weakened | Every changed test is listed in §8.3 with the new expected contract; a guard test prevents silent ignores | Revert Stage 5 |
| `Legacy*` shim forgotten | Stage 6 grep gate | — |
| `UdpSessionChannel` leaks | Inbox/carrier released from the `Stopped` subscription; transport stop cascade covered by tests | Revert Stage 5 |

---

## 11. Decision log

| Item | Decision |
|---|---|
| `IsValid` | `IsRunning` (rewrite the gate test accordingly) |
| One-shot `Stop` | `_invalidated` check + CAS latch + base `Stop(reason, false)`; **no `IsRunning` guard**, so pre-start and `Starting` stops work |
| `OnStarted` / `OnStopped` pairing | Three-state handler wrapper forwards `OnStopped` only after a successful `OnStarted`; covers throwing `OnStarted` and stop-while-starting. `IRawHandler`, `Specification.md` §6.3/§7.1/§7.3 and the legacy `OnStartedCompleted` guard are preserved |
| `Stop` blocking | Latch and `IsValid` turn over immediately, but the call may execute the transition inline and block at an armed gate (queue-drain behaviour); the rewritten gate test uses `Task.Run` |
| Queued stop action | `Stopping`/`Stopped` become no-ops instead of throwing (§5.2 item 4); required because an external stop can be latched while `Starting` and promoted by `FinishStart` first |
| Inbound dispatch key | Explicit `Buffering`/`Open`/`Closed` inbox state, not `IsRunning`; `IsRunning` turns true before the flush, which would let a new message overtake the trigger |
| Callback concurrency | Per-endpoint serialization with cross-endpoint concurrency — already the normative `Specification.md` §11 contract; only the over-asserting test changes |
| Budget hook signature | `OnPendingSendChanged(int deltaBytes, int deltaMessages)` — signed deltas, not absolute snapshots |
| Delta notification placement | Next to **every** `_totalBuffersSize` mutation: reservation, overflow rollback, `Schedule` failure, `onFailToRun`, and `_sendAction`'s `finally` (five sites) |
| Dropped packets | Never logged |
| Legacy proxy | Public `LegacyRawUnreliableEndpoint` in Stage 2 (carriers and `RawUnreliableWorkItem` reference it), deleted in Stage 6 |
| Legacy transport bases | Public renames in Stage 3; `internal` is impossible for types public carriers derive from |
| Budget caps | `MessageMaxByteSize * QueueCapacity` bytes, `QueueCapacity` messages; calibrated in Stage 5 |
| Conformance staging | Carrier conversion is one stage: the conformance fixture is shared by four adapters, so a per-carrier intermediate state cannot be green |
| Stage granularity | Stages 0–7; Stages 1–4 individually verifiable; Stage 5 is one verified behaviour flip |
