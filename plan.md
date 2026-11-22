# Implement Direct RRReliable and RRUnreliable transports

## Goal

Add two in-process RR transports to `Transports/Pontifex.Transport.Direct`: one
`RRUnreliable` carrier and one `RRReliable` carrier, both over
`Channel`/`ChannelManager`. Reuse the existing reusable `RRUnreliable` core, add
a reusable `RRReliable` core under `Pontifex/Implementations/RR/Reliable`, add
factory bindings and implementation specifications, and prove both transports
with deterministic tests.

After the plan is complete, a caller can compile
`direct-rr-unreliable("id", ...)` and `direct-rr-reliable("id", ...)` for the
client and server roles, start them in process, exchange requests and
responses, and observe the family-specific completion, cancellation, and TTL
semantics defined by the RR specifications.

## Context

The repository is on branch `pontifex-2.0-features/Direct-implement-for-all`.
Direct currently implements `RawReliable` (`direct`) and `RawUnreliable`
(`direct-raw-unreliable`) over an in-process `Channel`; the Net transport
implements `RRUnreliable` over UDP. The RR family has:

- shared interfaces in `Pontifex/Abstractions/Transports/RR`;
- a shared specification and one specification per family;
- a reusable `RRUnreliable` core in `Pontifex/Implementations/RR/Unreliable`;
- **no `RRReliable` implementation**; only interfaces and the spec exist.

The Direct carrier is an in-process `Channel` pair managed by the process-wide
`ChannelManager`. A channel is ordered and lossless, can install virtual
delivery systems (`perfect` or `netem`), and is the natural session for a
reliable RR request stream.

Agreed decisions:

- use Direct `Channel`/`ChannelManager` directly, not the Raw Direct transports;
- reuse the `RRUnreliable` core and add reusable `RRReliable` base classes;
- schemes `direct-rr-reliable` and `direct-rr-unreliable`;
- client and server TTLs default to 3 seconds;
- `RRUnreliable` accepts an optional `delivery` struct with `perfect`/`netem`;
- `RRUnreliable` channel loss leaves the transport running and pending requests
  expire; a later request reconnects with fresh delivery systems;
- `RRReliable` `Start` succeeds without a server; a `Request` with no server
  returns `Error` and delivers no callbacks;
- `RRUnreliable` `Start` succeeds without a server; a `Request` with no server
  returns `Ok` and the frame is dropped, so the request expires silently;
- the RRReliable type specification is corrected where it contradicts the shared
  specification (correlation header and handler serialization) before the core
  is implemented;
- terminal stop notifications are **scheduled** before `onStopped`; they need
  not finish before `onStopped` begins, per shared RR §7.4.

The plan consolidates the earlier five-phase proposal into four review
checkpoints. Integration and factory registration are folded into the stage that
introduces each transport.

## Constraints

- Follow `AGENTS.md` for every project: explicit `Nullable`, `LangVersion`,
  `ImplicitUsings`, and `TreatWarningsAsErrors`; libraries use
  `TreatWarningsAsErrors=true`.
- Read the matching specifications before changing a transport abstraction or
  implementation. The RR specifications are normative.
- New test suites follow `Tests/RULES.md`. Test Suites need `README.md`,
  `Coverage.md`, a root category, tests under `Tests/`, and no nested layers.
  Abstract Test Suits use one layer per abstract type and add layers
  parent-first; planned layers are recorded in the nearest existing ancestor
  layer.
- Tests must be deterministic. Do not use real time, sleeps, or uncontrolled
  background tasks for TTL tests; use the injectable timer/clock seams.
- Do not change the existing Raw transports or the existing public behavior of
  the `RRUnreliable` core. Additive internal seams are allowed.
- The reusable RR core classes are public abstract types in `Pontifex`, matching
  the `RRUnreliable` core. Direct carrier classes are internal; bindings are
  public.
- The RRReliable core follows the shared RR serialization MUST: the single
  server handler's `OnRequested` callbacks are serialized. The RRReliable type
  specification is corrected to match this before implementation.
- Stop must never wait for a running handler callback. Mark terminal state and
  schedule the callback through the request gate; `onStopped` may run before the
  scheduled finish executes.
- No conformance controls are defined for RR; do not add any.
- Every stage must leave the solution buildable and the relevant tests green.
- Keep one commit per stage; do not commit unrelated changes.

## Non-goals

- RR implementations over TCP, UDP, or any other carrier.
- A shared RR conformance suite or conformance controls.
- Reconnection, persistence, or restart recovery for `RRReliable`.
- Changes to the `RawReliable`/`RawUnreliable` transports.
- Protocol-layer, converter, TestUI, or API-layer integration.
- Authentication, confidentiality, integrity, or replay protection.

## Assumptions

| # | Assumption | Evidence |
| --- | --- | --- |
| A1 | The existing RRUnreliable core is reusable through `StartCarrier`, `StopCarrier`, `SendFrame`, and `OnFrameReceived`. | `Pontifex/Implementations/RR/Unreliable/*`; `Transports/Pontifex.Transport.Net/Udp/RR/Unreliable/*`. |
| A2 | RRReliable has no implementation. | `Pontifex/Implementations/RR/Reliable` does not exist; `Pontifex/TODO.md` lists it as open. |
| A3 | `Channel` is ordered and lossless; `Dispose` releases installed handlers and delivery systems, and does not remove the `ChannelManager` entry. | `Channel.cs:48-78,122-161`; `ChannelManager.cs:23-84`. |
| A4 | `ChannelManager` is a process-wide registry keyed by server `id` and client endpoint. | `ChannelManager.cs`. |
| A5 | Scheme canonical names are globally unique. | `TransportRegistryBuilder.cs` rule 3. |
| A6 | The RR specs do not require a start-time server check; `NotConnected` means the client is not running. | RR common §6.1, §6.2, §7.2, §9. |
| A7 | TTL defaults are 3 s/3 s. | User decision. |
| A8 | `UnreliableDeliveryBinding` builds delivery systems and is internal to Direct. | `Raw/Unreliable/UnreliableDeliveryBinding.cs`. |
| A9 | Direct and test assemblies are friends of `Pontifex`. | `Pontifex.csproj` `InternalsVisibleTo`. |
| A10 | `SerializedCallbackQueue<T>` is available; its `Dispose` is non-blocking. | `Pontifex/Utils/SerializedCallbackQueue.cs`. |
| A11 | Test projects target .NET 10 and use NUnit. | Test csproj files. |
| A12 | The plan file is committed; the executor deletes it after completion. | Plan revisions; `plan-executor` Phase 5. |
| A13 | `RR/Reliable/Specification.md` contradicts shared §8/§11 on headers and serialization. | `RR/Reliable/Specification.md` §4/§6.3/§6.4/§8; `RR/Specification.md` §8/§11. |
| A14 | `AnyTransport.Fail(StopReason, string)` wraps the reason in `ChainFail`; `TextFail` is an `AnyFail`. | `AnyTransport.cs:172-209`; `StopReasons.cs:103-190`. |

## Prerequisites

- .NET 10 SDK installed and `dotnet build Pontifex.sln` passing.
- Working tree clean before stage 1; the plan file is tracked at the latest
  revision commit.
- Read the RR common, reliable, and unreliable specifications, the
  implementation-specification profile, and `Tests/RULES.md`.

## Stages

### Stage 1 — Shared Direct RR channel carrier and RRUnreliable Direct

**Objective.** Deliver a working in-process `RRUnreliable` client/server pair
with binding, specification, and tests, built on the existing RRUnreliable core
and a new shared Direct channel carrier.

**Inputs.** Existing `RRUnreliable` core, `Channel`, `ChannelManager`,
`UnreliableDeliveryBinding`, RR specifications.

**Actions.**

1. Add `RR/Common/RRDirectInfo.cs` (internal) with the two scheme names,
   `MessageMaxByteSize = 1024 * 1024` (payload only), 3 s TTL defaults, and
   `DispatchQueueCapacity = 100`.
2. Add an internal `DirectRrChannelClient`:
   - constructor takes server endpoint, a client endpoint, logger, inbound
     `Action<UnionDataList>`, optional owner-disposal `Action`, and two
     `Func<IDeliverySystem>?` factories;
   - serialize the connect/install path with a private lock;
   - `TrySend(frame, acceptWithoutPeer)`:
     - if no channel or the channel is disposed, call
       `ChannelManager.Connect(server, client)`;
     - subscribe to `Disposed` before installing, install a fresh
       `ClientHandler` and fresh systems, then re-check `IsDisposed`; if
       disposed, release the just-installed systems with
       `DeliverySystemLifetime.Release`, clear the channel, and treat as
       no-peer;
     - the `Disposed` handler calls
       `ChannelManager.Disconnect(server, client)` to purge the registry entry,
       clears the channel, and invokes the owner callback; this is required so a
       later reconnect is not refused by a stale registry entry;
     - if no peer, release the frame and return `Ok` for
       `acceptWithoutPeer: true`, otherwise `Error`;
     - send with `SendToServer`; translate `NotConnected` to the no-peer result
       and clear the channel (the channel already released the frame);
     - expose an internal `CurrentChannel` seam for tests.
   - `Stop()` unsubscribes, disconnects, disposes, and releases systems created
     but never installed.
3. Add an internal `DirectRrChannelServer`:
   - constructor takes server id, logger, inbound
     `Action<IEndPoint, UnionDataList>`, and optional
     `Action<IEndPoint>? onClientDisconnected`;
   - `_stopping` flag; `TryStart` registers and fails on a duplicate id;
   - `OnChannelCreated` rejects when stopping, disposes on failed `TryAdd`,
     sets `channel.ServerHandler = (clientEp, message) => inbound(clientEp, message)`,
     subscribes to `Disposed`, and re-checks the stopping state and
     `channel.IsDisposed` after adding;
   - `Disposed` removes the matching channel and invokes
     `onClientDisconnected(clientEp)`;
   - `TrySend(destination, frame)` sends `SendToClient`; missing destination
     releases and returns `Error`; `NotConnected` maps to `Error`;
   - `Stop` unregisters, disposes all channels, clears, and releases callbacks;
   - internal `RegisteredClientCount`.
4. Add `RR/Unreliable/RRUnreliableDirectClient` (internal sealed), deriving
   `RRUnreliableClientTransport`:
   - constructor takes server endpoint, request TTL, logger, memory, two
     delivery factories, and optional `IDateTimeProvider`;
   - `MessageMaxByteSize` from `RRDirectInfo`;
   - `StartCarrier` returns true; `StopCarrier` stops the carrier;
   - `SendFrame` uses `acceptWithoutPeer: true`;
   - channel disposal is a loss, not a failure; inbound frames go to
     `OnFrameReceived(ServerEndPoint, frame)`.
5. Add `RR/Unreliable/RRUnreliableDirectServer` (internal sealed), deriving
   `RRUnreliableServerTransport`:
   - constructor takes server id, response TTL, logger, memory, optional
     `IDateTimeProvider`, and queue capacity;
   - create the `SerializedCallbackQueue<InboundFrame>` in `StartCarrier` only
     after `DirectRrChannelServer.TryStart` succeeds; dispose in `StopCarrier`
     so a failed start cannot leak a thread;
   - `InboundFrame` is an internal readonly struct of source and message;
   - `SendFrame` delegates to the server carrier;
   - inbound frames are serialized before `OnFrameReceived`; a full queue
     releases the frame (loss is permitted); the server handler is never
     concurrent.
6. Add `RR/Unreliable/RRUnreliableDirectBinding`:
   - scheme `direct-rr-unreliable`, `TransportType.RRUnreliable`;
   - `id` required positional, `request_ttl` client-only, `response_ttl`
     server-only, `delivery` optional client-only;
   - validate `delivery` eagerly during `Create` using a new internal
     `UnreliableDeliveryBinding.Validate` helper that parses and checks the
     loss-model requirements (including `fourState`/`gilbertElliot` parameters)
     without constructing a threaded netem system;
   - build a fresh system per connect through a factory; absent `delivery`
     means fresh perfect systems per connect;
   - reject wrong-role TTLs and `delivery` on the server.
7. Add `RR/Unreliable/Specification.md` with all nine sections and the open
   areas: wire correlation, TTL values, pending storage, reconnect policy,
   capacity, dispatch serialization, payload-only max size.
8. Add the RRUnreliable Direct Test Suit under `RR/RRUnreliableDirect/` with
   README, Coverage, and tests covering:
   - round trip, route, request id, single-use, rejection, cancel, silent expiry
     with a manual clock (advance the clock and trigger the sweep with a later
     request or late response), invalid/oversized payloads;
   - `Init`/start rules, stop clears pending, no-server `Ok` + expiry while the
     client remains valid, a server started later serves the next request;
   - channel loss without a server restart: use the internal `CurrentChannel`
     seam to dispose the channel, then the next request reconnects and receives
     a response; the registry entry is purged;
   - a channel disposed during connect/install releases the just-installed
     systems and returns the no-peer result;
   - reconnecting with `delivery = netem` creates fresh systems, not released
     ones;
   - a scripted `IDeliverySystem` drops/duplicates; a duplicate response yields
     exactly one callback; a dropped request expires silently;
   - a system created before a failed connect is released; a full dispatch queue
     releases the frame without blocking;
   - two clients cannot enter the server handler concurrently;
   - registry removal, `TrySend` to a removed destination returns `Error`, stop
     clears the count; send-after-dispose returns the no-peer policy, never
     `NotConnected`;
   - binding-created transports expose 3 s defaults and explicit TTLs through a
     hand-written `ITransportBuildContext` harness; a binding-created netem
     client starts/stops without real-time assertions.
   - category `Direct.RRUnreliable`.
9. Register the binding in `FactoryBindingsTests` and `TransportRegistry`; add
   compile cases for both roles, TTLs, delivery, invalid delivery, wrong roles,
   and canonicalization. In `FactoryBindingsTests`, cast compiled transports to
   the RR interfaces and assert TTL defaults/values.
10. Run the verification.

**Verification.**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRUnreliable"
dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj \
  --filter "FullyQualifiedName~FactoryBindingsTests"
dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj
```

Expected: 0 build errors; RRUnreliable, factory, full transport, and full Direct
tests pass.

**Definition of done.** `direct-rr-unreliable` compiles for both roles;
response, cancellation, silent expiry, rejection, and stop behave as specified;
the server handler is serialized; the registry purges disposed clients and
reconnects work with fresh systems; the suite, Coverage, and nine-section spec
exist; factory and transport tests pass.

**Risks / notes.** The server dispatch queue is created only after a successful
carrier start. The client must purge the registry entry on disposal, or the next
connect is refused. `SendFrame` never returns `NotConnected` for a missing peer.

### Stage 2 — RRReliable core foundation

**Objective.** Provide the correctly corrected type-specification statements,
frame encoding including response ordinals, request state/table, lifecycle base
classes, and a public timer seam, with deterministic tests in correctly typed
suites.

**Inputs.** Stage 1 complete; RRReliable interfaces and specification.

**Actions.**

1. Correct `RR/Reliable/Specification.md`:
   - §4, §6.4, §8: the type requires no correlation header, but an
     implementation MAY add an implementation-defined header under §12;
     `MessageMaxByteSize` excludes transport framing and that header;
   - §6.3/§11: `OnRequested` callbacks are serialized and non-reentrant,
     matching shared §11.
   Record the correction in the commit message.
2. Add `RRReliableFrame` (internal static):
   - Request `[type][requestId]` + payload;
   - Response `[type][requestId][ordinal uint]` + payload; ordinal starts at 1;
   - Finish `[type][requestId][completion byte]`;
   - Cancel `[type][requestId]`;
   - encode/decode, malformed handling, ownership, header constant, and a
     maximum-header boundary test.
3. Add `RRReliableRequestState` (internal sealed): client-local id, handler,
   pending/accepted/terminal transition, cancel flag, callback-running flag,
   timer handle, last delivered ordinal; serialized, idempotent transitions;
   `Stop`/`Cancel` terminal finishes are deferred while a callback runs.
4. Add `RRReliableRequestTable` (internal, lock-protected): add, get without
   removing, take exact, remove, clear; entries hold handler + state; no
   deadline/clock; a response does not remove an entry; tests for
   add/duplicate/get-without-remove/take/clear and concurrent access.
5. Add `RRReliableTransport` (public abstract `AnyTransport`,
   `IRRReliableTransport`): `Type`, abstract `MessageMaxByteSize`,
   `FrameMaxByteSize`, `TryValidatePayload`, `NextServerRequestId`,
   `StartCarrier`, `StopCarrier`, `ClearPendingRequests`, `TryStart`,
   `OnStopped`; stop clears pending without waiting.
6. Add `RRReliableTransport<THandler>` (public abstract): one-time `Init`,
   `DispatchHandler`, start/stop gating.
7. Add a **public** `IRRReliableTimerFactory` (`IDisposable Schedule(TimeSpan,
   Action)`); internal `SystemRRReliableTimerFactory` using
   `System.Threading.Timer`; core constructors accept
   `IRRReliableTimerFactory? = null`; timer is the only TTL mechanism (no
   clock).
8. Add suites:
   - `RR/RRReliableCoreHelpers/` Test Suit for `RRReliableFrame`,
     `RRReliableRequestTable`, `RRReliableRequestState`, and the production
     timer factory; category `Direct.RRReliableCoreHelpers`;
   - `RR/RRReliableTransport/` Abstract Test Suit: root `RRReliableTransport`
     and child `RRReliableTransport<THandler>`; category
     `Direct.RRReliableTransport`; record the planned client layer in the root
     Coverage and the planned server layer in the generic layer Coverage.
9. Add a single loose `RR/TestSupport/ManualTimerFactory.cs` outside all
   suites; the helpers, abstract, and Direct suites use it. Do not put a manual
   factory inside a suite `Harness/`.
10. Tests:
   - helpers: every frame type/ordinal, malformed frames, header boundary,
     table non-removing lookup/terminal removal/concurrency, state transitions,
     manual timer fire/suppress/dispose, production timer schedule/dispose
     without waiting; record real-fire as deferred-by-design;
   - root layer: lifecycle, payload validation, id allocation, stop scheduling;
   - generic layer: `Init` rules and dispatch gating.
11. Run the verification.

**Verification.**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj
dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj
```

Expected: 0 errors; full Direct, core, and Net tests pass.

**Definition of done.** Type-spec contradictions corrected; frame/ordinal/header,
table, state, timer, root and generic layers covered; planned layers recorded in
the correct ancestor Coverage; no Direct RRReliable transport yet.

**Risks / notes.** The manual timer factory is shared as loose test support, not
a suite harness. The production real-fire path is deferred by design.

### Stage 3 — RRReliable core client/server behaviour

**Objective.** Deliver the complete reusable RRReliable core: client lifecycle,
server sessions/handles, exactly-once `Finish`, cancellation, TTLs, duplicate
suppression, serialized server callbacks, deadlock-free stop, and scripted
tests.

**Inputs.** Stage 2 foundation and suites.

**Actions.**

1. Add `RRReliableClientTransport` (public abstract,
   `IRRReliableClientTransport`):
   - ctor: type name, server endpoint, request TTL, logger, memory, optional
     timer factory;
   - `protected abstract SendResult SendFrame(UnionDataList)` and
     `protected void OnFrameReceived(IEndPoint, UnionDataList)`; both own and
     release the frame on every path;
   - `protected virtual ushort CreateRandomRequestId()` default crypto; tests
     override;
   - `Request`: handler checks, payload validation, reserve/bind, publish
     `RequestId` before `SendFrame`, add pending, send, arm the TTL **under the
     state lock only if still pending** (dispose immediately otherwise), remove
     and unbind on every non-`Ok` path;
   - inbound `Response`: get without removing, reject duplicate/lower ordinal,
     invoke `Response` through the per-request callback gate, keep binding;
   - inbound `Finish`: remove, invoke one `Finish` through the gate, unbind;
   - `Cancel`: suppress responses, best-effort `Cancel` frame, one
     `Finish(Canceled)`, deferred inside a callback;
   - `Stop` from a callback also defers `Finish(Failed)`;
   - TTL timer delivers one `Finish(Failed)` through the gate and is disposed
     on every terminal close;
   - `ClearPendingRequests`: for each pending request, mark terminal and
     **schedule** the `Finish(Failed)` through the gate; do not wait for a
     running callback. `onStopped` may begin before the scheduled finish runs;
     the finish is scheduled before `onStopped`.
2. Add `RRReliableServerTransport` (public abstract):
   - ctor: type name, response TTL, logger, memory, optional timer factory,
     optional dispatch capacity (default 1024);
   - `protected abstract SendResult SendFrame(IEndPoint, UnionDataList)` and
     `protected void OnFrameReceived(IEndPoint, UnionDataList)`; ownership as
     above;
   - `protected bool StartDispatch()` creates the serialized `OnRequested`
     queue; `RRReliableDirectServer.StartCarrier` must call it before carrier
     registration; dispose it on stop. This hook is required because the core
     has no pre-carrier hook after the sealed `TryStart`;
   - `Init` rules mirror RRUnreliable;
   - per-client sessions keyed by source; a lock-protected session map;
   - a bounded completed-id window (1024, FIFO) records **every terminal id**
     (Finish, Reject, Cancel), so duplicates after finish/reject/cancel are
     suppressed;
   - inbound Request: payload check, ignore duplicate live/completed id, assign
     server-global id from 1, create handle with response timer, dispatch
     `OnRequested` through the queue; full queue closes the handle, releases the
     request, client TTL fails; throwing `OnRequested` is logged and rejects an
     undecided handle;
   - inbound Cancel closes the matching live request, records the id terminal,
     and suppresses later responses;
   - `OnSessionClosed(source)` atomically marks handles closed without waiting
     for an in-flight `Response`; later calls return `Error`;
   - response-TTL callback sends `Finish(Failed)` and closes the handle;
   - `internal SendResponse(RRReliableResponse, UnionDataList)` bridges the
     handle to `SendFrame`.
3. Add `RRReliableResponse` (internal sealed):
   - undecided/accepted/rejected/closed; timer handle disposed on every close;
   - `Response` allocates the next `uint` ordinal, sends, keeps open; `Finish`
     sends Normal and closes; `Reject` sends Rejected and closes;
   - precedence: while running, a handle closed by `Reject` throws
     `InvalidOperationException` on later `Response`/`Finish`; repeated
     `Reject` returns `Ok`; `Reject` after acceptance throws; `Finish` after
     `Finish` returns `Error`; after stop all calls return `Error`; a handle
     closed by Finish/TTL/session close returns `Error`; null/invalid →
     `InvalidMessage`; too big → `MessageTooBig`; carrier results pass through;
   - calls serialized; terminal exactly once.
4. Add client and server layers to the ATS parent-first; update README and
   Coverage; assign each test to its layer.
5. Tests with a scripted carrier and the loose manual timer:
   - ordered responses + `Finish(Normal)`;
   - duplicate response ordinal → one callback; response after `Finish` dropped;
   - duplicate request before `Finish` presented once; after `Finish`/`Reject`/
     `Cancel` suppressed by the completed window;
   - Reject → `Finish(Rejected)`; lost notification → TTL `Failed`;
   - cancel → one `Finish(Canceled)`, including inside a callback; late
     responses suppressed; server cancel closes the handle;
   - request and response TTLs → one `Finish(Failed)`; timer disposal on every
     close;
   - stop schedules one `Finish(Failed)` per pending request before `onStopped`
     and may execute after; stop from inside a callback does not deadlock;
     blocked `Response` + concurrent stop does not hang;
   - response-handle matrix (Reject/Finish/closed/after-stop);
   - `OnRequested` serialization; throwing handler; full dispatch queue;
   - random start override, increment, wrap;
   - `RequestId` published before `SendFrame`; source filter; handler checks;
     `NotConnected`/`BufferOverflow`; server ids start at 1; two threads on one
     handle produce one terminal result;
   - `OnSessionClosed(a)` closes only a's handles; b remains usable;
   - concurrent inbound responses for different requests are safe (table/session
     locks);
   - handler reuse, throwing client handlers, payload boundaries, callback
     serialization.
6. Run the verification.

**Verification.**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj
dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj
```

Expected: all core behaviour tests pass; full Direct, core, and Net tests green;
no real-time sleeps.

**Definition of done.** Core satisfies the reliable spec under a scripted
carrier: exactly-once request presentation, ordered responses, duplicate
suppression including terminal ids, one `Finish`, cancellation, TTLs,
server-handler serialization, response-handle matrix, deadlock-free stop,
session closure, and client/server layers with Coverage.

**Risks / notes.** Stop is mark-and-schedule, not wait. A terminal cause
commits once; later causes are silent. The response ordinal is `uint`, so the
stream is not practically capped. The server queue is started before carrier
registration. TTL timers are disposed on every close.

### Stage 4 — RRReliable Direct and integration

**Objective.** Deliver the in-process RRReliable Direct client/server with
binding, specification, E2E tests, and factory registration.

**Inputs.** Stage 1 carrier; Stage 3 core; reliable spec.

**Actions.**

1. Add `RR/Reliable/RRReliableDirectClient` (internal sealed):
   - ctor: server endpoint, request TTL, logger, memory, optional timer factory;
   - `StartCarrier` returns true; lazy channel;
   - `SendFrame` uses `acceptWithoutPeer: false` (no server → `Error`);
   - track `_hasLiveSession`, set on the first `SendFrame` that returns `Ok`;
   - channel disposal before the first successful send is a no-session loss:
     clear the channel, leave the transport valid; a later request retries and
     returns `Error` if no server;
   - channel disposal after a live session calls
     `Fail(new TextFail(Name, "direct channel disposed"))`; this is an `AnyFail`,
     so `onStopped` receives `TextFail` directly (no `ChainFail` wrapper);
   - inbound frames go to `OnFrameReceived(ServerEndPoint, frame)`.
2. Add `RR/Reliable/RRReliableDirectServer` (internal sealed):
   - ctor: server id, response TTL, logger, memory, optional timer factory;
   - `StartCarrier` calls `StartDispatch()` before
     `DirectRrChannelServer.TryStart`; on carrier start failure, stop dispatch;
   - `StopCarrier` stops the carrier and dispatch;
   - `SendFrame` delegates to the carrier;
   - inbound frames go to `OnFrameReceived`; `OnRequested` serialization is in
     the core; channel disposal calls `OnSessionClosed(source)`.
3. Add `RR/Reliable/RRReliableDirectBinding`:
   - scheme `direct-rr-reliable`, type `RRReliable`;
   - `id` required positional, `request_ttl` client-only, `response_ttl`
     server-only, defaults 3 s/3 s;
   - do not register `delivery`; the compiler rejects it; reject wrong-role
     TTLs in `Create`.
4. Add `RR/Reliable/Specification.md` with all nine sections and open areas:
   wire correlation (request id + `uint` ordinal), duplicate-suppression scope
   (1024 terminal ids), completion transport, timer factory, server-handler
   serialization, capacity, no persistence, payload-only max.
5. Add the RRReliable Direct Test Suit:
   - multiple ordered responses + Normal; Reject; Cancel;
   - request/response TTL via internal transports + manual timer;
   - stop schedules Failed; normal stop leaves `IsValid` true;
   - no server → `Error`, no callbacks, client valid; later server serves next;
   - channel loss after a live session → `onStopped` reason `TextFail`,
     `IsValid` false, `IsStarted` false; server handles return `Error`;
   - channel loss before any successful send → no failure, valid transport;
   - server `Init`/start rules; handler reuse; payload boundaries;
   - two clients independent;
   - two concurrent requests from one client, second answered first, each
     handler gets its own response/Finish and distinct server ids.
6. Register the binding in `FactoryBindingsTests` and `TransportRegistry`; add
   compile cases for both roles, TTLs, wrong roles, unknown `delivery`, and
   canonicalization; assert `RequestTtl`/`ResponseTtl` defaults and explicit
   values via the RR interfaces.
7. Run the full verification.

**Verification.**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj
dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj
dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj
dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj
dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj
```

Expected: 0 errors; all Direct, Transports, Transport, Factory, core, and Net
tests pass.

**Definition of done.** `direct-rr-reliable` runs both roles; E2E proves normal,
reject, cancel, TTLs, stop, no-server, channel-loss lifecycle, and concurrent
correlation; binding and spec exist; factory TTL assertions pass; no regressions.

**Risks / notes.** No-server `Error` leaves the client valid. Channel loss after
a live session is a `TextFail` and invalidates. Loss before a live session is
not a failure. The binding does not expose timers; TTL tests use internal
transports.

## Global definition of done

- Both bindings exist, compile for both roles, and are registered in factory
  tests.
- Both Direct transports run over `Channel`/`ChannelManager` and satisfy their
  family specs.
- RRUnreliable reuses the existing core; RRReliable uses the new reusable core.
- TTL defaults 3 s/3 s; RRUnreliable exposes netem; reconnects use fresh systems.
- No-server: RRReliable `Error`, RRUnreliable `Ok` + silent expiry.
- Suites: `Direct.RRUnreliable`, `Direct.RRReliable` Test Suits;
  `Direct.RRReliableCoreHelpers` Test Suit; `Direct.RRReliableTransport`
  Abstract Test Suit; each with README/Coverage per RULES.
- `dotnet build Pontifex.sln` 0 errors; all Direct, Transports, Transport,
  Factory, core, and Net tests pass.
- Working tree clean after stage commits; the executor deletes the plan in its
  completion commit.
