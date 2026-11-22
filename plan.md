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
  implementation. The RR specifications are normative; a bounded deviation is
  allowed only when the implementation spec §7 names the requirement, cause,
  impact, and handler tolerance, and the deviation is listed in the global DoD.
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
| A13 | `RR/Reliable/Specification.md` §6.3/§11 contradicts shared §11 on server-handler serialization; the header wording is an implementation-defined choice under §12, not a contradiction. | `RR/Reliable/Specification.md` §4/§6.3/§6.4/§8; `RR/Specification.md` §8/§11/§12. |
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
     `Action<UnionDataList>`, optional owner-disposal `Action`, an optional
     owner-live-session `Action`, and two `Func<IDeliverySystem>?` factories;
   - serialize the connect/install path, `TrySend`, and `Stop` with one private
     lock; the `Disposed` handler receives the disposed channel, and under the
     lock it calls `ChannelManager.Disconnect(server, client)` and the owner
     callback **only when `_channel` still references that channel**; a stale
     notification for an older channel is ignored;
   - the owner callback is invoked **after the carrier lock is released**; the
     caller (`Fail`) must never be called under that lock;
   - `TrySend(frame, acceptWithoutPeer)`:
     - if no channel or the channel is disposed, call
       `ChannelManager.Connect(server, client)`;
     - subscribe to `Disposed` before installing, install a fresh
       `ClientHandler` and fresh systems, then re-check `IsDisposed`; if
       disposed, call `ChannelManager.Disconnect(server, client)` to purge the
       registry entry, release the just-installed systems with
       `DeliverySystemLifetime.Release`, clear the channel, and treat as
       no-peer;
     - the `Disposed` handler uses the identity guard above; this is required so
       a later reconnect is not refused by a stale registry entry;
     - if no peer, release the frame and return `Ok` for
       `acceptWithoutPeer: true`, otherwise `Error`;
     - wrap the per-connect delivery-system factory calls in try/catch; a throw
       disconnects and disposes the just-created channel, clears `_channel`, and
       releases any created systems; the result is `Error`;
     - send with `SendToServer` inside try/catch; a delivery-system exception is
       caught, the frame is released exactly once, and the result is `Error`;
       translate `NotConnected` by calling
       `ChannelManager.Disconnect(server, client)` and clearing the channel
       under the lock before returning the no-peer result; this purges the
       stale registry entry even when the disposal notification is blocked on
       the lock (the channel already released the frame);
     - on the first successful `SendToServer`, invoke the live-session callback
       **under the lock before releasing it**, so the owner records
       `_hasLiveSession` before a concurrent disposal can consult it;
     - expose an internal `CurrentChannel` seam for tests.
   - `Stop()` unsubscribes, disconnects, disposes, and releases systems created
     but never installed.
3. Add an internal `DirectRrChannelServer`:
   - constructor takes server id, logger, inbound
     `Action<IEndPoint, UnionDataList>`, and optional
     `Action<IEndPoint>? onClientDisconnected`;
   - `_stopping` flag; `TryStart` registers and fails on a duplicate id;
   - a private `RejectChannel(clientEp, channel)` helper calls
     `ChannelManager.Disconnect(server, clientEp)` before `channel.Dispose()`;
     use it from every reject path; expose an internal
     `RejectChannelForTests(clientEp, channel)` hook, or make `RejectChannel`
     internal, so the reject path can be tested against a live registry entry;
   - `OnChannelCreated` rejects on the carrier's `_stopping` flag only; it must
     not call `IsStarted` while `TryStart` holds `_locker`. It disposes through
     `RejectChannel` on failed `TryAdd`, sets
     `channel.ServerHandler = (clientEp, message) => inbound(clientEp, message)`,
     subscribes to `Disposed`, and re-checks the stopping state and
     `channel.IsDisposed` after adding; a disposed/stopping re-check removes the
     channel from `_channels` under an identity check before `RejectChannel`;
   - `Disposed` removes the matching channel **by identity** and invokes
     `onClientDisconnected(clientEp)` only when the disposed channel is still
     the one registered for that endpoint; a stale notification is ignored;
   - `TrySend(destination, frame)` sends `SendToClient`; missing destination
     releases and returns `Error`; `NotConnected` maps to `Error`;
   - `Stop` unregisters, disposes all channels, clears, and releases callbacks;
   - internal `RegisteredClientCount` and a deterministic
     `StartGateForTests` hook signalled after registration/queue creation and
     optionally blocking `TryStart` until released, so the start-window test
     can pause before `OnStarted`.
4. Add `RR/Unreliable/RRUnreliableDirectClient` (internal sealed), deriving
   `RRUnreliableClientTransport`:
   - constructor takes server endpoint, request TTL, logger, memory, two
     delivery factories, and optional `IDateTimeProvider`;
   - the transport owns one fixed `GuidEndPoint` for its lifetime and passes it
     to the carrier, following `RawUnreliableDirectClientTransport`;
   - `MessageMaxByteSize` from `RRDirectInfo`;
   - `StartCarrier` returns true; `StopCarrier` stops the carrier;
   - `SendFrame` uses `acceptWithoutPeer: true`;
   - channel disposal is a loss, not a failure; inbound frames go to
     `OnFrameReceived(ServerEndPoint, frame)`;
   - expose an internal `CurrentCarrier` seam (the `DirectRrChannelClient`) for
     tests.
5. Add `RR/Unreliable/RRUnreliableDirectServer` (internal sealed), deriving
   `RRUnreliableServerTransport`:
   - constructor takes server id, response TTL, logger, memory, optional
     `IDateTimeProvider`, and queue capacity;
   - create the `SerializedCallbackQueue<InboundFrame>` **before**
     `DirectRrChannelServer.TryStart`, so a request cannot arrive before the
     queue exists; if registration fails, dispose the queue; dispose it on
     stop. The InboundFrame disposer releases `frame.Message` for frames still
     queued at stop;
   - override `OnStarted` to open a `_dispatchReady` gate under one lock that
     also owns the pending list: `OnStarted` sets the flag, swaps the list, and
     posts the swapped frames **while still holding the lock** (`Post` is
     non-blocking); the inbound callback under the same lock either appends or
     posts. Frames
     received before the gate opens are held in a transport-owned pending list
     bounded by `RRDirectInfo.DispatchQueueCapacity`; a full list releases the
     frame; stop releases the list. A
     queued frame is never popped and held. No `OnRequested` begins before
     `OnStarted`; a callback may begin during the final phase of `Start` before
     the caller observes the return, which the implementation spec §7 declares
     as a bounded deviation from shared §7.2;
   - `InboundFrame` is an internal readonly struct of source, message, and the
     receipt timestamp;
   - the RR server's inbound callback captures
     `DateTime receivedAt = _timeProvider.Now` **before** posting;
   - add an **internal** overload
     `OnFrameReceived(IEndPoint source, UnionDataList frame, DateTime receivedAt)`
     to `RRUnreliableServerTransport` that computes the response-handle deadline
     from `receivedAt`; the existing `OnFrameReceived(source, frame)` calls the
     overload with `Now`. The overload is internal because the constraints allow
     additive internal seams only; Direct is a friend assembly.
   - `SendFrame` delegates to the server carrier;
   - inbound frames are serialized before `OnFrameReceived(source, frame,
     receivedAt)`; a full queue releases the frame (loss is permitted); the
     server handler is never concurrent;
   - expose an internal `CurrentCarrier` seam (the `DirectRrChannelServer`) for
     the start-gate, reject, and registry tests.
6. Add `RR/Unreliable/RRUnreliableDirectBinding`:
   - scheme `direct-rr-unreliable`, `TransportType.RRUnreliable`;
   - `id` required positional, `request_ttl` client-only, `response_ttl`
     server-only, `delivery` optional client-only;
   - validate `delivery` eagerly during `Create` using a new internal
     `UnreliableDeliveryBinding.Validate` helper that shares the loss-model
     check with `NetemDeliverySystem` (extract the check or make
     `CreateLossModel` internal) and does not construct a threaded netem
     system; add compile cases for `fourState` and `gilbertElliot` without
     their parameter structs;
   - build a fresh system per connect through a factory; absent `delivery`
     means fresh perfect systems per connect;
   - reject wrong-role TTLs and `delivery` on the server.
7. Add `Transports/Pontifex.Transport.Direct/RR/Unreliable/Specification.md`
   with all nine sections and the open areas: wire correlation, TTL values,
   pending storage, reconnect policy, capacity, payload-only max size,
   process-wide `id` uniqueness across Direct transports, the single dispatch
   queue as the callback scheduler, channel-disposal failure detection, and
   logging. State the single-queue
   server-callback serialization as a guarantee in §3/§6/§7, not an open choice.
   In §7 declare the bounded deviations: a request whose
   `ResponseTtl` expired while queued may still be presented to
   `OnRequested` (shared §6.5 says SHOULD NOT), `OnRequested` already running at
   stop may complete after `onStopped` because dispatch disposal is
   non-blocking, and `OnRequested` may begin during the final phase of `Start`
   after `OnStarted` before the caller observes the return (shared §7.2); the
   handler must tolerate a callback before it has observed `Start` returning.
   Also declare the bounded netem teardown: stopping an RRUnreliable client
   with `delivery = netem` may wait up to five seconds while
   `NetemDeliverySystem` joins its dequeue thread; the terminal-notification
   rule does not promise prompt carrier teardown under netem.
8. Add the RRUnreliable Direct Test Suit under `RR/RRUnreliableDirect/` with
   README, Coverage, and tests covering: put the manual clock, tracking
   delivery system, and scripted delivery system in the suite `Harness/` and
   document them in the README.
   - round trip, route, request id, single-use, rejection, cancel, silent expiry
     with a manual clock (advance the clock and trigger the sweep with a later
     request or late response), invalid/oversized payloads;
   - `Init`/start rules, stop clears pending, no-server `Ok` + expiry while the
     client remains valid, a server started later serves the next request;
   - a server start with a deterministic gate: a request sent before
     `OnStarted` begins no handler callback; after the gate opens, the queued
     request is presented exactly once. The test records whether the callback
     observed `Start` returned, and accepts the declared deviation; it uses an
     internal start-gate seam, not a timing race; a send concurrent with the
     gate release is neither lost nor reordered;
   - the server's internal `RejectChannelForTests` path purges the
     `ChannelManager` entry; the next request after a rejection reconnects, and
     a restarted server serves it;
   - response TTL starts at receipt: block the dispatch queue, enqueue a
     request, advance the manual clock past `ResponseTtl`, release the queue,
     and assert the handle is closed and `Response` returns `Error`;
   - channel loss without a server restart: use the internal `CurrentChannel`
     seam to dispose the channel, then the next request reconnects and receives
     a response; the registry entry is purged; a send racing a disposal purges
     the entry and the next request reconnects;
   - a channel disposed during connect/install releases the just-installed
     systems and returns the no-peer result; the transport's internal
     `CurrentCarrier` seam lets the suite reach the carrier;
   - the carrier's `acceptWithoutPeer: false` branch returns `Error` with no
     live session; the live-session callback fires exactly once on the first
     successful send and never on a pre-session loss;
   - reconnecting with a tracking delivery factory creates fresh systems, not
     released ones; the real netem reconnect lives only in the integration
     group;
   - a scripted `IDeliverySystem` drops/duplicates; a duplicate response yields
     exactly one callback; a dropped request expires silently;
   - a system installed on a channel that is rejected during install is
     released; a full dispatch queue releases the frame without blocking; a
     frame still queued at stop is released by the queue disposer; a throwing
     delivery system is caught and the frame released;
   - two clients cannot enter the server handler concurrently;
   - registry removal, `TrySend` to a removed destination returns `Error`, stop
     clears the count; send-after-dispose returns the no-peer policy, never
     `NotConnected`;
   - binding-created transports expose 3 s defaults and explicit TTLs through a
     hand-written `ITransportBuildContext` harness;
   - a binding-created client/server round trip through the real binding, in
     the `Tests/Integration/` group with category
     `Direct.RRUnreliable.Integration`, using the bounded signal wait pattern;
     a binding-created reconnect with `delivery = netem` in the same group;
   - category `Direct.RRUnreliable`; integration category
     `Direct.RRUnreliable.Integration`.
9. Register the binding in `FactoryBindingsTests` and `TransportRegistry`; add
   compile cases for both roles, TTLs, delivery, invalid delivery, wrong roles,
   and canonicalization. In `FactoryBindingsTests`, cast compiled transports to
   the RR interfaces and assert TTL defaults/values, and add one binding-created
   round trip that starts the compiled server/client and exchanges a request and
   a response, using the bounded signal wait pattern of the existing RR UDP
   round-trip tests.
10. Run the verification.

**Verification.**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRUnreliable&TestCategory!=Direct.RRUnreliable.Integration"
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRUnreliable.Integration"
dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj \
  --filter "FullyQualifiedName~FactoryBindingsTests"
dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj
dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj
```

Expected: 0 build errors; RRUnreliable, factory, full transport, and full Direct
tests pass. At least one test must match
`Direct.RRUnreliable.Integration`; zero matches is a failure. The default
filter must contain no real netem, sleep, or uncontrolled thread; the
integration group is excluded. Also inspect
manually: `Transports/Pontifex.Transport.Direct/RR/
Unreliable/Specification.md` has all nine sections, the §5 parameter table, the
§7 deviations, every §8 open-area choice, and the §9 implementer checklist; the
`RR/RRUnreliableDirect/README.md` and `Coverage.md` pass the
`Tests/RULES.md` CHECK-01..12 review. The filtered RRUnreliable command must
report at least the planned tests; zero matches is a failure.

**Definition of done.** `direct-rr-unreliable` compiles for both roles;
response, cancellation, silent expiry, rejection, and stop behave as specified;
the server handler is serialized; the registry purges disposed clients and
reconnects work with fresh systems; the suite, Coverage, and nine-section spec
exist; factory and transport tests pass.

**Risks / notes.** The server dispatch queue is created **before** carrier
registration and disposed if registration fails. Dispatch is released only after
`OnStarted`; frames arriving during start are held until the gate opens, and
released on stop. The client must purge the registry entry on disposal, or the
next connect is refused. `SendFrame` never returns `NotConnected` for a missing
peer.

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
     matching shared §11;
   - §5.1 Handlers row and §10 processing sentence: scope concurrency to
     different request handlers, not the single server handler;
   - §15: add the implementer-checklist item "serialize the single server
     handler's callbacks" so the new MUST is traceable.
   Record the correction in the commit message.
2. Add `RRReliableFrame` (internal static):
   - Request `[type][requestId]` + payload;
   - Response `[type][requestId][ordinal uint]` + payload; ordinal starts at 1;
     encode the ordinal as an `int` with
     `PutFirst(new UnionData(unchecked((int)ordinal)))` and decode it with
     `TryPopFirst(out int value)` followed by `unchecked((uint)value)`, because
     the `UnionDataList` extensions have no `uint` overload; add a round-trip
     test at `uint.MaxValue`;
   - Finish `[type][requestId][completion byte]`;
   - Cancel `[type][requestId]`;
   - encode/decode, malformed handling, ownership, header constant, and a
     maximum-header boundary test.
3. Add `RRReliableRequestState` (internal sealed): client-local id, handler,
   pending/provisional/accepted/terminal transition, cancel flag,
   callback-running flag, timer handle, last delivered ordinal, and a
   provisional-response buffer; serialized, idempotent transitions;
   `Stop`/`Cancel` terminal finishes are deferred while a callback runs.
   Responses and terminal frames received while the request is provisional are
   buffered in wire order; after `Request` returns `Ok`, the buffer is drained
   through the gate in order; on non-`Ok`, the buffer and payloads are released
   and no callback is delivered. Responses with an ordinal above the expected
   next ordinal are buffered until the gap is filled; lower/duplicate ordinals
   are dropped.
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
7. Add a **public** `IRRReliableTimerFactory` at
   `Pontifex/Abstractions/Transports/RR/Reliable/IRRReliableTimerFactory.cs`
   (`IDisposable Schedule(TimeSpan, Action)`); declare it in the reliable type
   specification §4, because it appears in protected constructors of public
   abstract classes. Add an internal `SystemRRReliableTimerFactory` using
   `System.Threading.Timer`; core constructors accept
   `IRRReliableTimerFactory? = null`; timer is the only TTL mechanism (no
   clock).
8. Add suites **in `Tests/Pontifex.Transport.Direct.Tests`** (the core is a
   friend assembly there), not in `Tests/Pontifex.Tests`:
   - `RR/RRReliableCoreHelpers/` Test Suit for `RRReliableFrame`,
     `RRReliableRequestTable`, and `RRReliableRequestState`; category
     `Direct.RRReliableCoreHelpers`;
   - `RR/RRReliableTransport/` Abstract Test Suit: root `RRReliableTransport`
     (including the timer-factory lifecycle) and child
     `RRReliableTransport<THandler>`; category `Direct.RRReliableTransport`;
     record the planned client layer in the root Coverage and the planned
     server layer in the generic layer Coverage.
9. Add a single loose
   `Tests/Pontifex.Transport.Direct.Tests/RR/TestSupport/ManualTimerFactory.cs`
   outside all suites. All three RR suites live in the same test project, so the
   one file is visible to the helpers, abstract, and Direct suites. The manual
   factory records scheduled callbacks and exposes a pending count so a test can
   assert "scheduled before `onStopped`" from inside `onStopped`; it supports
   fire, suppress, and dispose. `Tests/Pontifex.Tests` and
   `Tests/Pontifex.Transport.Net.Tests` are regression runs only and do not own
   an RR suite.
10. Tests:
   - helpers: every frame type/ordinal, malformed frames, header boundary,
     table non-removing lookup/terminal removal/concurrency, state transitions;
   - root layer: lifecycle, payload validation, id allocation, timer-factory
     fire/suppress/dispose and production schedule/dispose without waiting
     (real-fire deferred by design), stop scheduling;
   - generic layer: `Init` rules and dispatch gating.
11. Run the verification.

**Verification.**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRReliableCoreHelpers"
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRReliableTransport"
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj
dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj
```

Expected: 0 errors; full Direct, core, and Net tests pass. Also inspect:
`Pontifex/Abstractions/Transports/RR/Reliable/Specification.md` §4/§5.1/§6.3/
§6.4/§8/§10/§11/§15 match the corrected text; the root/generic `Coverage.md` record the
planned client/server layers in the correct ancestor; the helper and abstract
suites pass CHECK-01..12. The full Direct project run must include a non-zero
number of tests from both new categories.

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
   - `Request`: check `IsStarted` **before** taking the state lock; handler
     checks and payload validation; reserve the handler; allocate the next id
     with the RRUnreliable loop (next id, `TryAdd`, at most 65536 attempts,
     else `BufferOverflow`); add a **provisional** entry under the state lock;
     then `Bind` to publish `RequestId` and the cancel action; re-check a
     state-lock-protected `_stopping` flag and the cancel flag under the lock
     (never call `IsStarted` under the lock); on failure remove the provisional
     entry, unbind, and return `NotConnected`/`Error`; send; on `Ok`, mark
     accepted only if still provisional and arm the TTL (dispose immediately
     otherwise); if a concurrent stop/cancel removed the provisional entry,
     return `Error` with no callback; no callback is scheduled for a request
     that returns non-`Ok`. The state lock never nests `_locker`; `Request`
     reads `IsStarted` outside it; `ClearPendingRequests` sets `_stopping` under
     the state lock;
   - inbound `Response`: if the entry is provisional, buffer the response in
     the request state; if accepted, get without removing, buffer a higher
     ordinal until the expected gap is filled, drop lower/duplicate ordinals,
     invoke `Response` through the per-request callback gate, keep binding;
   - inbound `Finish`: if the entry is provisional, buffer the terminal frame
     in wire order; if accepted, hold the terminal behind the expected ordinal
     sequence, drain every buffered response in order, then invoke one `Finish`
     through the gate and unbind inside the gate after the callback returns; a
     terminal already buffered causes later responses to be dropped;
   - `Cancel`: suppress responses, best-effort `Cancel` frame, one
     `Finish(Canceled)`, deferred inside a callback; a provisional request is
     removed silently;
   - the client callback gate is a transport-owned serialized pump. Each
     request has a pending-terminal slot drained by the pump; a terminal
     requested inside a callback is deferred until the callback returns; a
     stop-scheduled terminal is posted to the pump. Expose an internal
     `PumpGateForTests`/`ScheduledTerminalCount` seam so a test can hold the
     pump, call `Stop`, assert the scheduled count in `onStopped`, then release
     the pump and observe exactly one `Finish(Failed)`;
   - `Stop` from a callback also defers `Finish(Failed)`;
   - the TTL timer delivers one `Finish(Failed)` through the gate and is
     disposed on every terminal close; the terminal action unbinds after the
     `Finish` callback returns, never before;
   - `ClearPendingRequests`: set `_stopping` under the state lock; remove
     provisional entries silently; for each **accepted** pending request, mark
     terminal and post the `Finish(Failed)` to the pump without unbinding;
     increment `ScheduledTerminalCount` when queued and decrement when it runs;
     do not wait for a running callback. `onStopped` may begin
     before the scheduled finish runs; the finish is scheduled before
     `onStopped`, and the gate unbinds after the callback returns.
2. Add `RRReliableServerTransport` (public abstract):
   - ctor: type name, response TTL, logger, memory, optional timer factory,
     optional dispatch capacity (default 1024);
   - `protected abstract SendResult SendFrame(IEndPoint, UnionDataList)` and
     `protected void OnFrameReceived(IEndPoint, UnionDataList)`; ownership as
     above;
   - `protected bool StartDispatch()` creates the serialized `OnRequested`
     queue; its item carries source, request, receipt time, and the created
     response handle. The disposer releases the request payload, closes the
     handle, disposes its timer, and records the client-local id terminal
     without presenting `OnRequested`. `protected void StopDispatch()` disposes
     it without waiting for a running callback. The Direct server in Stage 4
     calls `StartDispatch()` before carrier registration and `StopDispatch()`
     on start failure and in `StopCarrier`;
   - dispatch is gated by a `_dispatchReady` flag opened in `OnStarted` under
     one lock that also owns the pending list; `OnStarted` sets the flag, swaps
     the list, and posts the swapped frames while still holding the lock; the
     inbound path under the same lock either appends or posts. Frames received
     before ready are held in the pending list and flushed on start, or released
     by `StopDispatch`; no `OnRequested` begins before `OnStarted`; a callback
     may begin during the final phase of `Start` before the caller observes the
     return (declared deviation, §7);
   - `Init` rules mirror RRUnreliable;
   - per-client sessions keyed by source; a lock-protected session map and a
     lock-protected completed window per session;
   - a full 16-bit **per-session** completed bitmap (65536 bits, 8 KB) keyed by
     the **client-local id**; it records every terminal id after presentation:
     `Finish`, `Reject`, `Cancel`, and `ResponseTtl` expiry, so duplicate
     suppression covers the whole id domain with no eviction. The server-global
     id is never a duplicate-suppression key; frames carry the client-local
     id;
   - inbound Request: payload check, ignore a duplicate live or completed id in
     the same session, assign a server-global id from 1, create a handle with a
     response timer, dispatch `OnRequested` through the queue; a full queue
     closes the handle, records the id terminal, releases the request, and the
     client fails at its TTL; a throwing `OnRequested` is logged and rejects an
     undecided handle;
   - the dispatch worker checks the handle on dequeue: if the response TTL
     already expired or the session closed, it releases the request, records the
     client-local id terminal, and does **not** present `OnRequested`;
   - inbound Cancel closes the matching live request, records the id terminal,
     and suppresses later responses;
   - `OnSessionClosed(source)` atomically marks that session's handles closed
     and disposes their timers without waiting for an in-flight `Response`;
     later calls return `Error`; other sessions stay usable;
   - override `ClearPendingRequests` to close every session handle and dispose
     its timer without waiting for a running callback;
   - response-TTL expiry sends `Finish(Failed)`, closes the handle, disposes
     its timer, and records the client-local id terminal;
   - `internal SendResponse(RRReliableResponse, UnionDataList)` bridges the
     handle to `SendFrame`.
3. Add `RRReliableResponse` (internal sealed):
   - undecided/accepted/rejected/closed; timer handle disposed on every close;
   - `Response` sends first; the `uint` ordinal is committed only after
     `SendFrame` returns `Ok`, so a failed send consumes no ordinal and the
     client stream never stalls; `Finish` sends Normal and closes; `Reject`
     sends Rejected and closes; an internal `SendFailed()` sends
     `Finish(Failed)` and closes, used by the response-TTL and session-close
     paths;
   - precedence: while running, a handle closed by `Reject` throws
     `InvalidOperationException` on later `Response`/`Finish`; repeated
     `Reject` returns `Ok`; `Reject` after acceptance throws; `Finish` after
     `Finish` returns `Error`; after stop all calls return `Error`; a handle
     closed by Finish/TTL/session close returns `Error`; null/invalid →
     `InvalidMessage`; too big → `MessageTooBig`; carrier results pass through;
   - calls for one handle are serialized by a **reentrant** gate held across
     `SendFrame`; a synchronous client `Cancel` re-enters the same gate on the
     same thread without deadlock. Stop and session close do not take the gate:
     they mark the handle closed atomically, so a stopping thread never waits
     for an in-flight `Response`; a terminal transition is exactly once;
4. Add client and server layers to the ATS parent-first; update README and
   Coverage; assign each test to its layer. Put the scripted carrier shared by
   the client and server layers in the ATS root `Harness/`, documented in the
   README; sibling layers must not own or reference each other's harness.
5. Tests with a scripted carrier and the loose manual timer:
   - ordered responses + `Finish(Normal)`;
   - duplicate response ordinal → one callback; response after `Finish` dropped;
   - duplicate request before `Finish` presented once; after `Finish`/`Reject`/
     `Cancel`/`ResponseTtl` suppressed by the per-session completed window; two
     clients with the same client-local id do not suppress each other;
   - a non-`Ok` `Request` schedules no `Finish`, including a stop or channel
     loss during `SendFrame`; a response or terminal frame arriving while the
     request is provisional is buffered and released on non-`Ok`, never
     delivered; a synchronous server `Finish`/`Reject` inside `SendFrame` is
     buffered and delivered only after `Ok`; a `Stop` or `Cancel` injected in
     the bind-to-add window is observed and the request returns non-`Ok` with
     no callback;
   - an out-of-order response ordinal is buffered until the expected ordinal
     arrives; a `Finish` arriving before a lower ordinal is held until the
     responses drain, then delivered; duplicates/lower ordinals are dropped;
   - a deferred terminal keeps the handler bound until the `Finish` callback
     returns; a rebind is refused before then and succeeds after;
   - Reject → `Finish(Rejected)`; lost notification → TTL `Failed`;
   - cancel → one `Finish(Canceled)`, including inside a callback; late
     responses suppressed; server cancel closes the handle;
   - request and response TTLs → one `Finish(Failed)`; timer disposal on every
     close; the response TTL starts at frame receipt, before a blocked
     `OnRequested` runs; if the TTL expires while queued, the request is not
     presented, the payload is released, and the id is terminal;
   - stop schedules one `Finish(Failed)` per pending request before `onStopped`
     and may execute after; `onStopped` observes the internal
     `ScheduledTerminalCount` greater than zero before the gate runs; stop from
     inside a callback does not deadlock; blocked `Response` + concurrent stop
     does not hang;
   - response-handle matrix (Reject/Finish/closed/after-stop);
   - `OnRequested` serialization; throwing handler; full dispatch queue; a
     request still queued at stop is released, its handle is closed, and its id
     is recorded terminal without presentation; a request queued before
     `OnStarted` is not presented until the ready gate opens;
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
no real-time sleeps. Also inspect: the ATS README layer table declares the
client and server layers with their parents; each new layer has `Coverage.md`
with covered/planned/deferred sections; the root/generic Coverage no longer
lists them as planned; the scripted carrier is in the nearest common ancestor
layer `Harness/` and documented; CHECK-01..12 pass; no default test uses real
time, sleeps, or uncontrolled threads.

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
   - the transport owns one fixed `GuidEndPoint` for its lifetime and passes it
     to the carrier;
   - `StartCarrier` returns true; lazy channel;
   - `SendFrame` uses `acceptWithoutPeer: false` (no server → `Error`);
   - track `_hasLiveSession`; the carrier invokes the live-session callback
     under its lock on the first successful send, before a concurrent disposal
     can consult it; the Direct client sets the flag from that callback;
   - channel disposal before the first successful send is a no-session loss:
     clear the channel, leave the transport valid; a later request retries and
     returns `Error` if no server;
   - channel disposal after a live session calls
     `Fail(new TextFail(Name, "direct channel disposed"))` **only when the
     transport is not already stopping**; the owner callback checks a carrier
     `_stopping`/`IsStarted` guard, and `Fail` is not called under the carrier
     lock. A normal `Stop` therefore leaves `IsValid` true. Before failing,
     `ClearPendingRequests` schedules `Finish(Failed)` for every accepted
     pending request;
   - expose an internal `CurrentCarrier` seam (the `DirectRrChannelClient`) for
     tests;
   - inbound frames go to `OnFrameReceived(ServerEndPoint, frame)`.
2. Add `RR/Reliable/RRReliableDirectServer` (internal sealed):
   - ctor: server id, response TTL, logger, memory, optional timer factory;
   - `StartCarrier` calls `StartDispatch()` before
     `DirectRrChannelServer.TryStart`; on carrier start failure, call
     `StopDispatch`; `StopCarrier` stops the carrier and calls `StopDispatch`;
   - `SendFrame` delegates to the carrier;
   - inbound frames go to `OnFrameReceived`; `OnRequested` serialization is in
     the core; channel disposal calls `OnSessionClosed(source)`.
3. Add `RR/Reliable/RRReliableDirectBinding`:
   - scheme `direct-rr-reliable`, type `RRReliable`;
   - `id` required positional, `request_ttl` client-only, `response_ttl`
     server-only, defaults 3 s/3 s;
   - do not register `delivery`; the compiler rejects it; reject wrong-role
     TTLs in `Create`.
4. Add `Transports/Pontifex.Transport.Direct/RR/Reliable/Specification.md`
   with all nine sections and open areas: wire correlation (request id + `uint`
   ordinal), duplicate-suppression scope (per-session 64K-bit client-local-id
   bitmap), completion transport, timer factory, capacity, no persistence,
   payload-only max, process-wide `id` uniqueness across Direct transports,
   the dispatch queue as the callback scheduler, channel-disposal failure
   detection, and logging. State the single-dispatch-queue `OnRequested`
   serialization as a guarantee in §3/§6/§7, not an open choice. In §7 declare the bounded deviations: a request
   arriving before the server's `OnStarted` is held until the gate opens; an
   `OnRequested` already running at stop may complete after `onStopped` because
   dispatch disposal is non-blocking; and `OnRequested` may begin during the
   final phase of `Start` after `OnStarted` before the caller observes the
   return (shared §7.2). A request whose response TTL expired while queued is
   not presented: the worker releases it and records the id terminal.
5. Add the RRReliable Direct Test Suit at
   `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableDirect/` with
   `README.md`, root `Coverage.md`, tests under `Tests/`, and category
   `Direct.RRReliable`:
   - multiple ordered responses + Normal; Reject; Cancel;
   - request/response TTL via internal transports + manual timer;
   - stop schedules Failed; normal stop leaves `IsValid` true; a disposal that
     occurs after `Stop` completes leaves `IsValid` true; a disposal that
     occurs before `Stop` starts fails the transport (`IsValid` false) and
     yields one `Finish(Failed)`; the two orderings are tested separately, never
     concurrently;
   - no server → `Error`, no callbacks, client valid; later server serves next;
   - channel loss after a live session → `onStopped` reason `TextFail`,
     `IsValid` false, `IsStarted` false; the pending request receives exactly
     one scheduled `Finish(Failed)`; server handles return `Error`;
   - channel loss before any successful send → no failure, valid transport;
   - a synchronous `Cancel` from inside a `Response` callback completes
     without deadlock and yields exactly one `Finish(Canceled)`;
   - server `Init`/start rules; handler reuse; payload boundaries;
   - two clients independent;
   - two concurrent requests from one client, second answered first, each
     handler gets its own response/Finish and distinct server ids;
   - a binding-created client/server round trip through the real binding, in a
     `Tests/Integration/` group with category `Direct.RRReliable.Integration`
     (only the TTL tests use internal transports because the binding does not
     expose a timer factory; use the bounded signal wait pattern of the existing
     RR UDP round-trip tests);
   - the pre-session loss case is induced through the internal `CurrentCarrier`
     seam or a test-owned `ChannelManager` server entry that disposes the
     channel immediately.
6. Register the binding in `FactoryBindingsTests` and `TransportRegistry`; add
   compile cases for both roles, TTLs, wrong roles, unknown `delivery`, and
   canonicalization; assert `RequestTtl`/`ResponseTtl` defaults and explicit
   values via the RR interfaces; add one binding-created round trip.
7. Run the full verification.

**Verification.**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRReliable&TestCategory!=Direct.RRReliable.Integration"
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRReliable.Integration"
dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj
dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj
dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj
dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj
```

Expected: 0 errors; all Direct, Transports, Transport, Factory, core, and Net
tests pass. At least one test must match `Direct.RRReliable.Integration`; zero
matches is a failure. The default filter must contain no real timer wait or
uncontrolled thread; TTL tests use the manual factory. Also inspect: the RRReliable Direct `Specification.md` has all nine
sections, §5, §7 deviations, §8 choices, §9 implementer checklist; the Direct suite README/Coverage
pass CHECK-01..12; the new `FactoryBindingsTests` case names are present and
executed (both schemes × both roles × TTLs × wrong roles × unknown `delivery` ×
canonicalization).

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
