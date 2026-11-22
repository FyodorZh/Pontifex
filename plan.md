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
- a reusable `RRUnreliable` core in `Pontifex/Implementations/RR/Unreliable`
  (`RRUnreliableTransport`, `RRUnreliableClientTransport`,
  `RRUnreliableServerTransport`, `RRUnreliableFrame`,
  `RRUnreliableRequestTable`, `RRUnreliableResponse`);
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
  returns `Ok` and the frame is dropped, so the request expires silently.

The plan consolidates the earlier five-phase proposal into four review
checkpoints. Integration and factory registration are folded into the stage that
introduces each transport, because a separate integration-only stage would not
be a useful review boundary.

## Constraints

- Follow `AGENTS.md` for every project: explicit `Nullable`, `LangVersion`,
  `ImplicitUsings`, and `TreatWarningsAsErrors`; libraries use
  `TreatWarningsAsErrors=true`.
- Read the matching specifications before changing a transport abstraction or
  implementation. The RR specifications are normative.
- New test suites follow `Tests/RULES.md`. Test Suites need `README.md`,
  `Coverage.md`, a root category, tests under `Tests/`, and no nested layers.
  Abstract Test Suits use one layer per abstract type and add layers
  parent-first.
- Tests must be deterministic. Do not use real time, sleeps, or uncontrolled
  background tasks for TTL tests; use the injectable timer/clock seams.
- Do not change the existing Raw transports or the existing public behavior of
  the `RRUnreliable` core. Additive internal seams are allowed.
- The reusable RR core classes are public abstract types in `Pontifex`, matching
  the `RRUnreliable` core. Direct carrier classes are internal; bindings are
  public.
- The RRReliable core follows the shared RR serialization MUST: the single
  server handler's `OnRequested` callbacks are serialized.
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
| A1 | The existing RRUnreliable core is reusable by a new carrier through `StartCarrier`, `StopCarrier`, `SendFrame`, and `OnFrameReceived`. | `Pontifex/Implementations/RR/Unreliable/RRUnreliableClientTransport.cs`, `...ServerTransport.cs`, `...Transport.cs`; `Transports/Pontifex.Transport.Net/Udp/RR/Unreliable/*`. |
| A2 | RRReliable has no implementation. | `Pontifex/Implementations/RR/Reliable` does not exist; `Pontifex/TODO.md` lists the unified RRReliable implementation as open. |
| A3 | `Channel` is ordered and lossless; `Dispose` releases installed handlers and delivery systems, so a reconnecting client must install fresh ones. | `Transports/Pontifex.Transport.Direct/Channel/Channel.cs:48-78,122-140`; `PerfectDeliverySystem.cs`; `NetemDeliverySystem.cs`. |
| A4 | `ChannelManager` is a process-wide registry keyed by server `id` and client endpoint, and disposes channels on server unregister. | `Transports/Pontifex.Transport.Direct/Channel/ChannelManager.cs`. |
| A5 | Scheme canonical names are globally unique; two RR types need two names. | `Pontifex.TransportFormula/Registry/Internal/TransportRegistryBuilder.cs` rule 3. |
| A6 | The RR specifications do not require a start-time server check; `NotConnected` means the client is not running. | RR common specification §6.1, §6.2, §7.2, §9; RRUnreliable §9. |
| A7 | TTL defaults are a user decision: 3 s request and 3 s response. | User decision in this conversation. |
| A8 | `UnreliableDeliveryBinding` builds the `perfect`/`netem` delivery systems and is internal to the Direct assembly. | `Transports/Pontifex.Transport.Direct/Raw/Unreliable/UnreliableDeliveryBinding.cs`. |
| A9 | The Direct and test assemblies are friends of `Pontifex` and can use internal seams. | `Pontifex/Pontifex.csproj` `InternalsVisibleTo` entries. |
| A10 | `SerializedCallbackQueue<T>` is available for serializing server dispatch. | `Pontifex/Utils/SerializedCallbackQueue.cs`. |
| A11 | The test projects target .NET 10 and use NUnit. | `Tests/Pontifex.Transport.Direct.Tests/*.csproj`, `Tests/Pontifex.Transports.Tests/*.csproj`. |
| A12 | The plan file is committed by the orchestrator before the audit and execution phases; it is not an untracked file. | Plan revision commits; `plan-executor` workflow. |

## Prerequisites

- .NET 10 SDK installed and `dotnet build Pontifex.sln` passing.
- Working tree clean before stage 1; the plan file is tracked at the latest plan
  revision commit.
- Read `Pontifex/Abstractions/Transports/RR/Specification.md`,
  `RR/Reliable/Specification.md`, `RR/Unreliable/Specification.md`, and
  `Pontifex/Abstractions/Transports/TransportSpecificationsSpecification.md`.
- Read `Tests/RULES.md`.

## Stages

### Stage 1 — Shared Direct RR channel carrier and RRUnreliable Direct

**Objective.** Deliver a working in-process `RRUnreliable` client/server pair
with binding, specification, and tests, built on the existing RRUnreliable core
and a new shared Direct channel carrier.

**Inputs.** Existing `RRUnreliable` core, `Channel`, `ChannelManager`,
`UnreliableDeliveryBinding`, `RRUnreliable` and shared RR specifications.

**Actions.**

1. Add `Transports/Pontifex.Transport.Direct/RR/Common/RRDirectInfo.cs`
   (internal) with:
   - the two scheme names;
   - `MessageMaxByteSize = 1024 * 1024`;
   - `DefaultRequestTtl = TimeSpan.FromSeconds(3)`;
   - `DefaultResponseTtl = TimeSpan.FromSeconds(3)`;
   - `DispatchQueueCapacity = 100`.
2. Add an internal `DirectRrChannelClient` in
   `RR/Common/DirectRrChannelClient.cs`:
   - constructor takes the server endpoint, a client endpoint, logger, an
     inbound `Action<UnionDataList>`, an optional owner-disposal `Action`, and
     two `Func<IDeliverySystem>?` factories (client direction and server
     direction);
   - `TrySend(UnionDataList frame, bool acceptWithoutPeer)`:
     - when no channel is held or the held channel is disposed, call
       `ChannelManager.Instance.Connect(server, client)` and install a fresh
       `ClientHandler`, fresh delivery systems from the factories, and the
       `Disposed` subscription on every successful connect;
     - if no server is available or the channel is disposed, release the frame
       and return `Ok` when `acceptWithoutPeer` is true, otherwise `Error`;
     - if the channel exists, send with `SendToServer`;
   - a channel disposal clears the held channel and invokes the owner callback;
     the next `TrySend` reconnects;
   - `Stop()` unsubscribes, disconnects through `ChannelManager`, and disposes
     the channel exactly once; systems installed on the channel are released by
     the channel, and systems created but never installed are released by the
     carrier;
   - expose no public members; internal test seams only.
3. Add an internal `DirectRrChannelServer` in
   `RR/Common/DirectRrChannelServer.cs`:
   - constructor takes the server id, logger, inbound
     `Action<IEndPoint, UnionDataList>`, optional
     `Action<IEndPoint>? onClientDisconnected`, and a dispatch flag is not
     needed here;
   - a `_stopping` flag; `TryStart()` registers the server and fails on a
     duplicate id;
   - `OnChannelCreated` mirrors
     `RawUnreliableDirectServerTransport.OnChannelCreated`: reject when
     stopping, dispose on failed `TryAdd`, add the channel, subscribe to
     `Disposed`, and re-check the stopping state after adding so a stop race
     cannot leave a live channel;
   - `TrySend(IEndPoint destination, UnionDataList frame)` sends through the
     matching channel with `SendToClient`; release and return `Error` when the
     destination is absent or the channel is disposed;
   - `Stop()` sets `_stopping`, unregisters the server, disposes every
     registered channel, clears the dictionary, and releases callbacks;
   - internal `RegisteredClientCount` test seam.
4. Add `RR/Unreliable/RRUnreliableDirectClient.cs` (internal sealed), deriving
   `RRUnreliableClientTransport`:
   - constructor takes server endpoint, request TTL, logger, memory, the two
     `Func<IDeliverySystem>?` factories, and an optional `IDateTimeProvider`;
   - `MessageMaxByteSize => RRDirectInfo.MessageMaxByteSize`;
   - `StartCarrier` returns true; the channel is lazy;
   - `StopCarrier` stops the channel carrier;
   - `SendFrame` delegates to `DirectRrChannelClient` with
     `acceptWithoutPeer: true`;
   - a channel disposal is a loss, not a transport failure: the carrier clears
     the channel and the next request reconnects;
   - inbound frames go to `OnFrameReceived(ServerEndPoint, frame)`.
5. Add `RR/Unreliable/RRUnreliableDirectServer.cs` (internal sealed), deriving
   `RRUnreliableServerTransport`:
   - constructor takes server id, response TTL, logger, memory, an optional
     `IDateTimeProvider`, and the dispatch queue capacity;
   - `MessageMaxByteSize => RRDirectInfo.MessageMaxByteSize`;
   - `StartCarrier` starts `DirectRrChannelServer`;
   - `StopCarrier` stops the server carrier and disposes the dispatch queue;
   - `SendFrame(destination, frame)` delegates to the server carrier;
   - inbound frames are serialized through one
     `SerializedCallbackQueue<InboundFrame>` before calling
     `OnFrameReceived(source, frame)`; a full queue releases the frame (the
     unreliable model permits loss);
   - the server handler is never called concurrently.
6. Add `RR/Unreliable/RRUnreliableDirectBinding.cs` (public static):
   - scheme `direct-rr-unreliable`, `TransportType.RRUnreliable`;
   - parameters: `id` required positional string, `request_ttl` optional
     positive integer seconds, `response_ttl` optional positive integer
     seconds, and `delivery` from `UnreliableDeliveryBinding.CreateParameter()`;
   - client role rejects `response_ttl`; server role rejects `request_ttl` and
     `delivery`;
   - when `delivery` is present, the binding captures the struct value and
     builds a fresh delivery system on each connect through a
     `Func<IDeliverySystem>`; when absent, the client uses fresh perfect
     systems per connect;
   - construct `RRUnreliableDirectClient` or `RRUnreliableDirectServer` with
     the defaults from `RRDirectInfo`.
7. Add `RR/Unreliable/Specification.md` with all nine implementation-spec
   sections: scope and references, normative language, contract, scheme and no
   negotiation, parameters, lifecycle and teardown, failure/deviations/
   protection, implementation-defined choices, implementer checklist. Record
   the open areas from the type spec: wire correlation, TTL values, pending
   window storage, reconnect policy, capacity, and dispatch serialization.
8. Add the RRUnreliable Direct Test Suit under
   `Tests/Pontifex.Transport.Direct.Tests/RR/RRUnreliableDirect/`:
   - `README.md` and `Coverage.md` following `Tests/RULES.md`;
   - `Tests/` files covering:
     - request/response round trip and server route/request id;
     - single-use response handle and rejection;
     - cancellation suppresses a later response;
     - silent expiry with a manual clock plus a dropped late response; advance
       the clock and trigger the pending-window sweep with a later request or a
       late response;
     - invalid and oversized payloads;
     - `Init` one-time and start-before-init;
     - stop clears pending;
     - start with no server returns `Ok` and expires;
     - a channel loss leaves the transport running; after the server is
       restarted with the same id, the next request reconnects and receives a
       response;
     - reconnecting with `delivery = netem` uses fresh systems, not the
       released ones;
     - a scripted `IDeliverySystem` drops and duplicates frames; a duplicate
       response produces exactly one callback; a dropped request expires
       silently;
     - two clients cannot enter the server handler concurrently;
     - one client channel disposal decrements `RegisteredClientCount`, a send
       to the removed destination returns `Error`, and a server stop clears the
       count;
     - a binding-created client/server expose 3 s `RequestTtl`/`ResponseTtl`;
     - a binding-created client with `delivery = netem` starts and stops
       without real-time behavioural assertions.
   - category `Direct.RRUnreliable`.
9. Register `RRUnreliableDirectBinding` in
   `Tests/Pontifex.Transports.Tests/FactoryBindingsTests.cs` and in
   `Tests/Pontifex.Transport.Tests/Core/TransportRegistry.cs`; add compile
   cases for both roles, TTL parameters, `delivery`, wrong-role parameters, and
   canonicalization.
10. Run the verification below.

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

Expected: build succeeds with 0 errors; the RRUnreliable suite, factory
bindings, the full transport test project, and the full Direct test project
pass.

**Definition of done.** `direct-rr-unreliable` compiles for both roles; a
client can obtain a response from a running server; cancellation, silent expiry,
rejection, and stop behave as specified; the server handler is serialized; the
registry removes disposed clients and a reconnect after server restart works
with fresh delivery systems; the suite and Coverage exist; the specification
exists; factory and transport tests pass.

**Risks / notes.** Multiple channels can deliver concurrently, so the server
dispatch queue is required. The client must install a fresh handler and fresh
delivery systems on every reconnect because `Channel.Dispose` releases both.
`SendFrame` must never return `NotConnected` for a missing peer; `Ok` plus a
dropped frame preserves unreliable semantics.

### Stage 2 — RRReliable core foundation

**Objective.** Provide the reusable, carrier-neutral foundation of the
`RRReliable` core: frame encoding including response ordinals, request state
and table, lifecycle base classes, and a public timer seam, with deterministic
unit tests in correctly typed suites.

**Inputs.** Stage 1 complete; RRReliable interfaces and specification; RR
common lifecycle.

**Actions.**

1. Add `Pontifex/Implementations/RR/Reliable/RRReliableFrame.cs` (internal
   static):
   - frame types `Request`, `Response`, `Finish`, `Cancel`;
   - request: `[type][requestId]` plus payload;
   - response: `[type][requestId][ordinal ushort]` plus payload;
   - finish: `[type][requestId][completion byte]`;
   - cancel: `[type][requestId]`;
   - encode and decode helpers for every type, malformed-input handling, and
     ownership;
   - a constant for the maximum header size.
2. Add `RRReliableRequestState.cs` (internal sealed):
   - owns the client-local id, handler, pending/accepted/terminal transition,
     cancel-requested flag, timer handle, and the last delivered response
     ordinal;
   - transition methods are serialized and idempotent; the terminal transition
     wins exactly once.
3. Add `RRReliableRequestTable.cs` (internal):
   - entries hold the handler and its request state; no deadline and no clock;
   - methods to add, get without removing (for response dispatch), take by
     exact id and handler for terminal transitions, remove, and clear;
   - a request is not removed by a response; it remains until `Finish`,
     cancellation, expiry, or stop;
   - tests for add/duplicate/get-without-remove/take/clear and state
     transitions.
4. Add `RRReliableTransport.cs` (public abstract `AnyTransport`,
   `IRRReliableTransport`):
   - `Type => TransportType.RRReliable`;
   - abstract `MessageMaxByteSize`;
   - `FrameMaxByteSize` derived from the payload maximum and header;
   - `TryValidatePayload`, `NextServerRequestId`, `StartCarrier`,
     `StopCarrier`, `ClearPendingRequests`, `TryStart`, and `OnStopped`,
     mirroring `RRUnreliableTransport`;
   - `OnStopped` clears pending state and stops the carrier.
5. Add `RRReliableTransport<THandler>` (public abstract) with one-time
   `Init(THandler)`, `DispatchHandler`, and the same start/stop gating as
   `RRUnreliableTransport<THandler>`.
6. Add a **public** `IRRReliableTimerFactory` in `Pontifex.RR.Reliable`:
   - `IDisposable Schedule(TimeSpan delay, Action callback)`;
   - an internal `SystemRRReliableTimerFactory` using `System.Threading.Timer`;
   - the core constructors accept `IRRReliableTimerFactory? timerFactory = null`
     and default to the system factory; this is the only TTL mechanism in the
     RRReliable core (no `IDateTimeProvider`).
   - the timer factory type is public because it appears in protected
     constructors of public abstract classes.
7. Add two suites under `Tests/Pontifex.Transport.Direct.Tests/RR/`:
   - `RRReliableCoreHelpers/` — a Test Suit for the tightly coupled
     non-abstract helpers `RRReliableFrame`, `RRReliableRequestTable`,
     `RRReliableRequestState`, and the timer factory; README, Coverage,
     category `Direct.RRReliableCoreHelpers`;
   - `RRReliableTransport/` — an Abstract Test Suit for the abstract transport
     hierarchy. Stage 2 creates the root layer `RRReliableTransport` and the
     child layer `RRReliableTransport<THandler>`; README, Coverage per layer,
     category `Direct.RRReliableTransport`. Child layers are added parent-first.
8. Tests:
   - helpers: every frame type including the response ordinal, malformed
     frames, table non-removing lookup, terminal removal, state transitions,
     timer schedule/fire/dispose;
   - root layer: lifecycle, payload validation, request-id allocation, stop
     ordering;
   - generic layer: `Init` one-time and pre-start, dispatch gating.
9. Run the full Direct test project, not only a filter.

**Verification.**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj
```

Expected: build 0 errors; the full Direct test project passes, including both
new core suites.

**Definition of done.** The core foundation compiles; frame round trips,
response ordinals, malformed frames, table non-removing lookup and terminal
removal, state transitions, and the timer seam are covered; the abstract
transport hierarchy has a correctly typed Abstract Test Suit; the generic layer
is present; no Direct RRReliable transport exists yet.

**Risks / notes.** A response must not remove the pending entry. The timer
factory is the only real-time source for RRReliable; tests use a manual factory.

### Stage 3 — RRReliable core client/server behaviour

**Objective.** Deliver a complete reusable `RRReliable` core: client request
lifecycle, server request/session handling, response handles, exactly-once
`Finish`, cancellation, TTL completion, duplicate suppression, serialized server
callbacks, and stop semantics, proven with a scripted carrier.

**Inputs.** Stage 2 foundation and suites.

**Actions.**

1. Add `RRReliableClientTransport.cs` (public abstract,
   `IRRReliableClientTransport`):
   - constructor takes type name, server endpoint, request TTL, logger, memory,
     and an optional `IRRReliableTimerFactory`; no clock;
   - random 16-bit client id start on `TryStart`; unchecked increment;
   - `Request` performs the handler checks, validates the payload, binds the
     handler, adds the pending entry, encodes and sends the request, arms the
     TTL timer only after `Ok`, and removes/unbinds on every non-`Ok` path;
   - inbound `Response` looks up the request without removing it, rejects a
     duplicate or lower ordinal, invokes the handler's `Response`, and keeps
     the binding;
   - inbound `Finish` removes the entry, invokes exactly one `Finish`, and
     unbinds after the callback;
   - `Cancel` suppresses later responses, sends a best-effort `Cancel` frame,
     and delivers exactly one `Finish(Canceled)`, deferred if called from inside
     a callback of the same handler;
   - the request TTL timer delivers exactly one `Finish(Failed)`; the timer is
     disposed on every terminal transition;
   - `ClearPendingRequests` finishes every pending request with `Failed`.
2. Add `RRReliableServerTransport.cs` (public abstract,
   `IRRReliableServerTransport`):
   - constructor takes type name, response TTL, logger, memory, and an optional
     `IRRReliableTimerFactory`;
   - `Init` rules mirror `RRUnreliableServerTransport`;
   - per-client session state keyed by the source endpoint, including a bounded
     completed-request-id set (for example 1024 ids) for duplicate suppression;
   - inbound `Request` checks the payload, ignores a duplicate live or completed
     id, assigns a server-global id, creates a response handle with a
     `ResponseTtl` timer, and dispatches `OnRequested` through one serialized
     queue so the single server handler is never concurrent (shared RR §11);
     the queue is disposed on stop, and undispatched requests are released; the
     client requests fail through stop or TTL;
   - inbound `Cancel` closes the matching live request and suppresses later
     responses;
   - `OnSessionClosed(source)` closes every handle for that session;
   - a response-TTL callback sends `Finish(Failed)` and closes the handle;
   - `internal SendResponse(RRReliableResponse, UnionDataList)` bridges the
     handle to the protected abstract `SendFrame`, mirroring
     `RRUnreliableServerTransport.SendResponse`.
3. Add `RRReliableResponse.cs` (internal sealed, `IRRReliableResponse`):
   - states undecided/accepted/rejected/closed;
   - `Response` allocates the next response ordinal, sends, and keeps the handle
     open; `Finish` sends `Finish(Normal)` and closes; `Reject` sends
     `Finish(Rejected)` and closes;
   - result precedence and exceptions from the reliable specification,
     including `Response` after `Reject` throwing, repeated `Reject` returning
     `Ok`, `Reject` after acceptance throwing, `Finish` after `Finish` returning
     `Error`, and a closed or stopped handle returning `Error`;
   - calls for one handle are serialized; a terminal transition is exactly
     once;
   - sends frames through `RRReliableServerTransport.SendResponse`.
4. Add the client and server layers to the `RRReliableTransport` Abstract Test
   Suit, parent-first, with `Coverage.md` for each layer.
5. Add deterministic tests with a scripted in-memory carrier and a manual timer
   factory:
   - zero or more responses followed by `Finish(Normal)`, in order;
   - a duplicated response (same ordinal) produces exactly one callback;
   - a request duplicated before `Finish` is presented once;
   - a request duplicated after `Finish` is suppressed by the completed-id
     window;
   - `Reject` produces `Finish(Rejected)`; a lost notification produces
     `Finish(Failed)` at the request TTL;
   - cancel produces exactly one `Finish(Canceled)`, including from inside a
     response callback; late responses are suppressed; the server receives the
     best-effort cancel and closes the handle;
   - request TTL and response TTL produce exactly one `Finish(Failed)`;
   - stop produces one `Finish(Failed)` per pending request before `onStopped`;
     server stop closes every open handle;
   - the response-handle result and exception matrix from §6.6;
   - `OnRequested` serialization across concurrent requests;
   - random start id and unchecked 16-bit wrap;
   - responses only from `ServerEndPoint`;
   - handler checks (null, wrong type, already bound throw before payload
     consumption);
   - `Request` returns `NotConnected` when not running and `BufferOverflow`
     when the scripted carrier reports it;
   - handler reuse after `Finish`; throwing handlers; invalid and oversized
     payloads; callback serialization per handler.
6. Run the full Direct test project.

**Verification.**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj
```

Expected: all core behaviour tests pass; the full Direct project remains green;
no real-time sleeps or uncontrolled timers are used.

**Definition of done.** The reusable core satisfies the RRReliable
specification under a scripted carrier: exactly-once request presentation,
ordered responses, duplicate suppression (request and response), exactly one
`Finish`, cancellation, both TTLs, server-handler serialization, response-handle
result/exception matrix, stop ordering, and session closure.

**Risks / notes.** This is the highest-risk stage. A terminal cause commits
once; later causes are silent. The response ordinal is the duplicate-suppression
key for the response stream. The server completed-id window is bounded and
documented. `Finish(Failed)` must be delivered before `onStopped` begins.

### Stage 4 — RRReliable Direct and integration

**Objective.** Deliver the in-process `RRReliable` Direct client/server with
binding, specification, end-to-end tests, and factory registration, reusing the
shared channel carrier and the Stage 3 core.

**Inputs.** Stage 1 shared carrier; Stage 3 core; RRReliable specification.

**Actions.**

1. Add `RR/Reliable/RRReliableDirectClient.cs` (internal sealed), deriving
   `RRReliableClientTransport`:
   - constructor takes server endpoint, request TTL, logger, memory, and an
     optional `IRRReliableTimerFactory`;
   - `MessageMaxByteSize => RRDirectInfo.MessageMaxByteSize`;
   - `StartCarrier` returns true; the channel is lazy;
   - `SendFrame` delegates to `DirectRrChannelClient` with
     `acceptWithoutPeer: false`, so no server yields `Error`;
   - a channel disposal after a session exists fails the transport
     (`UnknownRemoteIntention`), which finishes pending requests with `Failed`;
   - inbound frames go to `OnFrameReceived(ServerEndPoint, frame)`.
2. Add `RR/Reliable/RRReliableDirectServer.cs` (internal sealed), deriving
   `RRReliableServerTransport`:
   - constructor takes server id, response TTL, logger, memory, and an optional
     `IRRReliableTimerFactory`;
   - `StartCarrier` starts `DirectRrChannelServer`;
   - `StopCarrier` stops the server carrier;
   - `SendFrame(destination, frame)` delegates to the server carrier;
   - inbound frames go to `OnFrameReceived`; `OnRequested` serialization is
     provided by the core, not the carrier; a channel disposal calls
     `OnSessionClosed(source)`.
3. Add `RR/Reliable/RRReliableDirectBinding.cs` (public static):
   - scheme `direct-rr-reliable`, `TransportType.RRReliable`;
   - parameters: `id` required positional, `request_ttl` client-only,
     `response_ttl` server-only, both optional positive seconds;
   - defaults 3 s/3 s from `RRDirectInfo`;
   - reject `delivery` and the wrong-role TTL.
4. Add `RR/Reliable/Specification.md` with all nine implementation-spec
   sections, including the open areas from the type spec: wire correlation
   (request id plus response ordinal), duplicate-suppression scope (completed-id
   window), completion transport, timer factory, server-handler serialization,
   capacity, and no persistence.
5. Add the RRReliable Direct Test Suit under
   `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableDirect/`:
   - README and Coverage per `Tests/RULES.md`; category `Direct.RRReliable`;
   - E2E tests:
     - request with multiple ordered responses then `Finish(Normal)`;
     - `Reject` → `Finish(Rejected)`;
     - cancel → `Finish(Canceled)`;
     - request TTL and response TTL → `Finish(Failed)` using the manual timer
       factory; TTL tests construct the internal transports directly because
       the binding does not expose a timer factory;
     - stop finishes pending requests with `Failed`;
     - no server → `Error` and no callbacks;
     - channel loss → `Failed`;
     - server `Init` one-time and start-before-init;
     - handler reuse after `Finish`;
     - invalid and oversized payloads;
     - two clients have independent sessions;
     - one client sends two concurrent requests with distinct payloads and
       handlers before either completes; the server responds to the second
       first; each handler receives only its own response and `Finish`, and the
       two server ids are distinct.
6. Register `RRReliableDirectBinding` in `FactoryBindingsTests` and
   `TransportRegistry`; add compile cases for both roles, TTL parameters, wrong
   roles, and canonicalization. Ensure the formula compiler accepts the new
   hyphenated scheme.
7. Run the full verification below.

**Verification.**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRReliable"
dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj \
  --filter "FullyQualifiedName~FactoryBindingsTests"
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj
dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj
dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj
```

Expected: build 0 errors; RRReliable suite and factory-binding tests pass; the
full Direct, transport, and factory test projects remain green.

**Definition of done.** `direct-rr-reliable` compiles and runs for both roles;
the E2E suite proves normal, reject, cancel, TTL, stop, no-server, channel-loss,
and concurrent-request correlation; the binding and nine-section specification
exist; factory tests pass; no Raw behavior regressed.

**Risks / notes.** `Start` succeeds without a server; the first `Request`
returns `Error` and binds no handler. A channel disposal after the first
successful session is a transport failure, not a retry signal. The binding does
not expose a timer factory; deterministic TTL tests construct the internal
transports directly.

## Global definition of done

- `direct-rr-unreliable` and `direct-rr-reliable` bindings exist, compile for
  client and server roles, and are registered in the factory tests.
- Both Direct transports run over `Channel`/`ChannelManager` and satisfy their
  family specifications.
- `RRUnreliable` uses the existing core; `RRReliable` uses a new reusable core.
- TTL defaults are 3 s/3 s; `delivery` netem is available on RRUnreliable;
  reconnection installs fresh delivery systems.
- No-server behavior is exactly: RRReliable `Error`, RRUnreliable `Ok` plus
  silent expiry.
- The new suites are: `Direct.RRUnreliable` (Test Suit), `Direct.RRReliable`
  (Test Suit), `Direct.RRReliableCoreHelpers` (Test Suit), and
  `Direct.RRReliableTransport` (Abstract Test Suit); each has README and
  Coverage where RULES requires it.
- `dotnet build Pontifex.sln` succeeds with 0 errors and all Direct, transport,
  and factory tests pass.
- Working tree is clean after the stage commits. The plan file is deleted in a
  dedicated final housekeeping commit, outside the stage commits.
