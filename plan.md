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
  expire; a later request may reconnect;
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
- Tests must be deterministic. Do not use real time, sleeps, or uncontrolled
  background tasks for TTL tests; use an injectable timer/clock seam.
- Do not change the existing Raw transports or the existing public behavior of
  the `RRUnreliable` core. Additive internal seams are allowed.
- The reusable RR core classes are public abstract types in `Pontifex`, matching
  the `RRUnreliable` core. Direct carrier classes are internal; bindings are
  public.
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
| A3 | `Channel` is ordered, lossless, and supports delivery-system replacement and disposal. | `Transports/Pontifex.Transport.Direct/Channel/Channel.cs`. |
| A4 | `ChannelManager` is a process-wide registry keyed by server `id` and client endpoint, and disposes channels on server unregister. | `Transports/Pontifex.Transport.Direct/Channel/ChannelManager.cs`. |
| A5 | Scheme canonical names are globally unique; two RR types need two names. | `Pontifex.TransportFormula/Registry/Internal/TransportRegistryBuilder.cs` rule 3. |
| A6 | The RR specifications do not require a start-time server check; `NotConnected` means the client is not running. | RR common specification §6.1, §6.2, §7.2, §9; RRUnreliable §9. |
| A7 | TTL defaults are a user decision: 3 s request and 3 s response. | User decision in this conversation. |
| A8 | `UnreliableDeliveryBinding` builds the `perfect`/`netem` delivery systems and is internal to the Direct assembly. | `Transports/Pontifex.Transport.Direct/Raw/Unreliable/UnreliableDeliveryBinding.cs`. |
| A9 | The Direct and test assemblies are friends of `Pontifex` and can use internal seams. | `Pontifex/Pontifex.csproj` `InternalsVisibleTo` entries. |
| A10 | `SerializedCallbackQueue<T>` is available for serializing RRUnreliable server dispatch. | `Pontifex/Utils/SerializedCallbackQueue.cs`. |
| A11 | The test projects target .NET 10 and use NUnit. | `Tests/Pontifex.Transport.Direct.Tests/*.csproj`, `Tests/Pontifex.Transports.Tests/*.csproj`. |

## Prerequisites

- .NET 10 SDK installed and `dotnet build Pontifex.sln` passing.
- Working tree clean before stage 1; the plan file is the only new untracked file.
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
   - `TransportName` fragments for the two schemes;
   - `MessageMaxByteSize = 1024 * 1024`;
   - `DefaultRequestTtl = TimeSpan.FromSeconds(3)`;
   - `DefaultResponseTtl = TimeSpan.FromSeconds(3)`.
2. Add an internal `DirectRrChannelClient` in
   `RR/Common/DirectRrChannelClient.cs`:
   - constructor takes server endpoint, an optional client endpoint, logger,
     memory, and optional delivery systems;
   - `TrySend(UnionDataList frame, bool acceptWithoutPeer)`: lazily calls
     `ChannelManager.Instance.Connect(server, client)`, installs the client
     handler and delivery systems on first use, and sends `frame` with
     `SendToServer`;
   - when no channel exists or the channel is disposed, release the frame and
     return `Ok` when `acceptWithoutPeer` is true, otherwise `Error`;
   - expose `Disposed`/`OnFrame` callbacks; the client owner decides whether a
     channel disposal is an RRUnreliable loss or an RRReliable failure;
   - `Stop()` unsubscribes, disconnects through `ChannelManager`, and disposes
     the channel exactly once.
3. Add an internal `DirectRrChannelServer` in
   `RR/Common/DirectRrChannelServer.cs`:
   - constructor takes server id, logger, memory, an inbound-frame callback, and
     an optional client-disconnected callback;
   - `TryStart()` registers the server with `ChannelManager`, failing on a
     duplicate id;
   - on channel creation, add the channel to a dictionary keyed by the client
     endpoint, set `ServerHandler`, and subscribe to `Disposed` to remove the
     matching entry and notify the owner;
   - `TrySend(IEndPoint destination, UnionDataList frame)` sends through the
     matching channel with `SendToClient`; release and return `Error` when the
     destination is absent or the channel is disposed;
   - `Stop()` unregisters the server, disposes every registered channel, clears
     the dictionary, and releases callbacks;
   - expose an internal `RegisteredClientCount` test seam.
4. Add `RR/Unreliable/RRUnreliableDirectClient.cs` (internal sealed), deriving
   `RRUnreliableClientTransport`:
   - public-in-assembly constructor takes server endpoint, request TTL, logger,
     memory, the two delivery systems, and an optional time provider;
   - `MessageMaxByteSize => RRDirectInfo.MessageMaxByteSize`;
   - `StartCarrier` returns true; the channel is lazy;
   - `StopCarrier` stops the channel carrier;
   - `SendFrame` delegates to `DirectRrChannelClient` with
     `acceptWithoutPeer: true`;
   - inbound frames go to `OnFrameReceived(ServerEndPoint, frame)`.
5. Add `RR/Unreliable/RRUnreliableDirectServer.cs` (internal sealed), deriving
   `RRUnreliableServerTransport`:
   - constructor takes server id, response TTL, logger, memory, an optional time
     provider, and a dispatch queue capacity;
   - `MessageMaxByteSize => RRDirectInfo.MessageMaxByteSize`;
   - `StartCarrier` starts `DirectRrChannelServer`;
   - `StopCarrier` stops the server carrier and disposes the dispatch queue;
   - `SendFrame(destination, frame)` delegates to the server carrier;
   - inbound frames are serialized through one
     `SerializedCallbackQueue<InboundFrame>` before calling
     `OnFrameReceived(source, frame)`; a full queue releases the frame (the
     unreliable model permits loss).
6. Add `RR/Unreliable/RRUnreliableDirectBinding.cs` (public static):
   - scheme `direct-rr-unreliable`, `TransportType.RRUnreliable`;
   - parameters: `id` required positional string, `request_ttl` optional
     positive integer seconds, `response_ttl` optional positive integer
     seconds, and `delivery` from `UnreliableDeliveryBinding.CreateParameter()`;
   - client role rejects `response_ttl`; server role rejects `request_ttl` and
     `delivery`;
   - build two delivery systems for the client when `delivery` is present,
     otherwise use perfect defaults;
   - construct `RRUnreliableDirectClient` or `RRUnreliableDirectServer` with the
     defaults from `RRDirectInfo`.
7. Add `RR/Unreliable/Specification.md` following the nine-section
   implementation-specification profile:
   - scope and references;
   - scheme and no negotiation;
   - parameters and role rules;
   - lifecycle and channel ownership, including lazy connect and loss policy;
   - failure, deviations, and protection;
   - implementation-defined choices (channel, addressing, dispatch queue,
     wire correlation, TTLs, capacity);
   - implementer checklist.
8. Add the RRUnreliable Direct Test Suit under
   `Tests/Pontifex.Transport.Direct.Tests/RR/RRUnreliableDirect/`:
   - `README.md` and `Coverage.md` following `Tests/RULES.md`;
   - `Tests/` files covering: request/response round trip; server route and
     request id; single-use response handle; rejection closes the handle;
     cancellation suppresses a later response; silent expiry with a manual
     clock plus a late response being dropped; invalid/oversized payloads;
     `Init` one-time and start-before-init; stop clears pending; start with no
     server returns `Ok` and expires; channel loss leaves the transport running;
     netem delivery drops/duplicates as configured; two clients cannot enter the
     server handler concurrently.
   - category `Direct.RRUnreliable`.
9. Register `RRUnreliableDirectBinding` in
   `Tests/Pontifex.Transports.Tests/FactoryBindingsTests.cs` and in
   `Tests/Pontifex.Transport.Tests/Core/TransportRegistry.cs`; add compile cases
   for both roles, TTL parameters, `delivery`, wrong-role parameters, and
   canonicalization.
10. Run the verification below.

**Verification.**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRUnreliable"
dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj \
  --filter "FullyQualifiedName~FactoryBindingsTests"
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj
```

Expected: build succeeds with 0 errors; the RRUnreliable suite and factory
tests pass; the full Direct test project remains green.

**Definition of done.** `direct-rr-unreliable` compiles for both roles; a
client can obtain a response from a running server; cancellation, silent expiry,
rejection, and stop behave as specified; the server handler is serialized; the
channel registry removes disposed clients; the new suite and Coverage exist; the
specification exists; factory tests pass.

**Risks / notes.** The server dispatch queue is required because multiple
channels can deliver concurrently and the RRUnreliable spec requires one
serialized handler. The client's `SendFrame` must never return `NotConnected` for
a missing peer; `Ok` plus a dropped frame preserves unreliable semantics.

### Stage 2 — RRReliable core foundation

**Objective.** Provide the reusable, carrier-neutral foundation of the
`RRReliable` core: frame encoding, request/pending tables, lifecycle base
classes, and timer seams, with deterministic unit tests.

**Inputs.** Stage 1 complete; RRReliable interfaces and specification; RR
common lifecycle.

**Actions.**

1. Add `Pontifex/Implementations/RR/Reliable/RRReliableFrame.cs` (internal
   static):
   - frame types `Request`, `Response`, `Finish`, `Cancel`;
   - `EncodeRequest`, `EncodeResponse`, `EncodeFinish`, `EncodeCancel`;
   - `TryDecode` methods that pop the frame type, the 16-bit client request id,
     and the completion status for `Finish`;
   - a constant for the maximum header size;
   - tests for every type, malformed input, and ownership.
2. Add `RRReliableRequestTable.cs` (internal):
   - entries hold the request handler, the deadline, and a per-request state;
   - methods to add, re-arm after `Request` returns `Ok`, take by exact id and
     handler for terminal transitions, find for response dispatch, remove, and
     clear;
   - a request is not removed by a response; it remains until `Finish`,
     cancellation, expiry, or stop;
   - tests for add/duplicate/re-arm/take/clear and expiry boundaries.
3. Add `RRReliableTransport.cs` (public abstract `AnyTransport`,
   `IRRReliableTransport`):
   - `Type => TransportType.RRReliable`;
   - abstract `MessageMaxByteSize`;
   - `FrameMaxByteSize` derived from the payload maximum and header;
   - `TryValidatePayload`, `NextServerRequestId`, `StartCarrier`,
     `StopCarrier`, `ClearPendingRequests`, `TryStart`, and `OnStopped`,
     mirroring `RRUnreliableTransport`;
   - `OnStopped` clears pending state and stops the carrier.
4. Add `RRReliableTransport<THandler>` (public abstract) with one-time
   `Init(THandler)`, `DispatchHandler`, and the same start/stop gating as
   `RRUnreliableTransport<THandler>`.
5. Add an internal timer seam in `Pontifex.RR.Reliable`:
   - `IRRReliableTimerFactory` with `IDisposable Schedule(TimeSpan, Action)`;
   - a production implementation using `System.Threading.Timer`;
   - the client/server core constructors accept an optional factory;
   - tests use a manual factory that records callbacks and fires them
     explicitly.
6. Add a Test Suit under
   `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableCore/` with README,
   Coverage, and tests for the frame codec, request table, lifecycle base, and
   timer seam. Category `Direct.RRReliableCore`.
7. Run the verification below.

**Verification.**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRReliableCore"
```

Expected: build 0 errors; frame, table, lifecycle, and timer tests pass.

**Definition of done.** The core foundation compiles; frame round trips and
malformed frames are covered; the pending table preserves a request across
responses and removes it only on a terminal cause; the base lifecycle mirrors
the RRUnreliable lifecycle; no Direct RRReliable transport exists yet.

**Risks / notes.** The table must not remove an entry on `Response`; reliable
responses are a stream. The timer seam is the only allowed source of real time
in production and is replaced in tests.

### Stage 3 — RRReliable core client/server behaviour

**Objective.** Deliver a complete reusable `RRReliable` core: client request
lifecycle, server request/session handling, response handles, exactly-once
`Finish`, cancellation, TTL completion, and stop semantics, proven with a
scripted carrier.

**Inputs.** Stage 2 foundation and tests.

**Actions.**

1. Add `RRReliableRequestState.cs` if the per-request state is not already in
   the table; it owns the pending/accepted/terminal transition, cancellation
   flag, timer handle, and callback-serialization state.
2. Add `RRReliableClientTransport.cs` (public abstract,
   `IRRReliableClientTransport`):
   - constructor takes type name, server endpoint, request TTL, logger, memory,
     optional time provider, and optional timer factory;
   - random 16-bit client id start on `TryStart`; unchecked increment;
   - `Request` performs the handler checks, validates the payload, binds the
     handler, adds the pending entry, encodes and sends the request, re-arms
     the TTL only after `Ok`, and removes/unbinds on every non-`Ok` path;
   - inbound `Response` invokes the handler's `Response`, keeps the binding, and
     suppresses a response for an unknown/terminal id;
   - inbound `Finish` removes the entry, invokes exactly one `Finish`, and
     unbinds after the callback;
   - `Cancel` suppresses later responses, sends a best-effort `Cancel` frame,
     and delivers exactly one `Finish(Canceled)`, deferred if called from inside
     a callback of the same handler;
   - the request TTL timer delivers exactly one `Finish(Failed)`;
   - `ClearPendingRequests` finishes every pending request with `Failed`.
3. Add `RRReliableServerTransport.cs` (public abstract,
   `IRRReliableServerTransport`):
   - `Init` rules mirror `RRUnreliableServerTransport`;
   - per-client session state keyed by the source endpoint;
   - inbound `Request` checks the payload, suppresses duplicate live ids for a
     session, assigns a server-global id, creates a response handle with a
     `ResponseTtl` timer, and calls `OnRequested`; a throwing callback rejects
     an undecided handle and leaves an already-accepted/rejected handle
     unchanged;
   - inbound `Cancel` closes the matching live request and suppresses later
     responses;
   - `OnSessionClosed(source)` closes every handle for that session;
   - a response-TTL callback sends `Finish(Failed)` and closes the handle.
4. Add `RRReliableResponse.cs` (internal sealed, `IRRReliableResponse`):
   - states undecided/accepted/rejected/closed;
   - `Response`, `Finish`, and `Reject` follow the result precedence and
     exceptions in the reliable specification;
   - calls for one handle are serialized; a terminal transition is exactly
     once;
   - sends frames through the transport's abstract
     `SendFrame(IEndPoint, UnionDataList)`.
5. Add deterministic tests with a scripted in-memory carrier under the
   `RRReliableCore` suite:
   - zero or more responses followed by `Finish(Normal)`, in order;
   - `Reject` produces `Finish(Rejected)`; a lost notification produces
     `Finish(Failed)` at the request TTL;
   - cancel produces exactly one `Finish(Canceled)`, including from inside a
     response callback; late responses are suppressed;
   - request TTL and response TTL produce exactly one `Finish(Failed)`;
   - stop produces one `Finish(Failed)` per pending request before `onStopped`;
   - duplicate requests/responses are suppressed; handler reuse after
     `Finish`; throwing handlers; invalid/oversized payloads; callback
     serialization per handler.
6. Run the verification below.

**Verification.**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "TestCategory=Direct.RRReliableCore"
```

Expected: all core behaviour tests pass; no real-time sleeps or uncontrolled
timers are used.

**Definition of done.** The reusable core satisfies the RRReliable
specification under a scripted carrier: exactly-once request presentation,
ordered responses, exactly one `Finish`, cancellation, both TTLs, stop
ordering, and duplicate suppression.

**Risks / notes.** This is the highest-risk stage. The per-request state machine
must serialize response callbacks, timer completion, cancellation, and stop.
A terminal cause commits once; later causes are silent. `Finish(Failed)` must be
delivered before `onStopped` begins.

### Stage 4 — RRReliable Direct and integration

**Objective.** Deliver the in-process `RRReliable` Direct client/server with
binding, specification, end-to-end tests, and factory registration, reusing the
shared channel carrier and the Stage 3 core.

**Inputs.** Stage 1 shared carrier; Stage 3 core; RRReliable specification.

**Actions.**

1. Add `RR/Reliable/RRReliableDirectClient.cs` (internal sealed), deriving
   `RRReliableClientTransport`:
   - constructor takes server endpoint, request TTL, logger, memory, optional
     time provider, and optional timer factory;
   - `MessageMaxByteSize => RRDirectInfo.MessageMaxByteSize`;
   - `StartCarrier` returns true; the channel is lazy;
   - `SendFrame` delegates to `DirectRrChannelClient` with
     `acceptWithoutPeer: false`, so no server yields `Error`;
   - a channel disposal after a session exists fails the transport
     (`UnknownRemoteIntention`), which finishes pending requests with `Failed`;
   - inbound frames go to `OnFrameReceived(ServerEndPoint, frame)`.
2. Add `RR/Reliable/RRReliableDirectServer.cs` (internal sealed), deriving
   `RRReliableServerTransport`:
   - constructor takes server id, response TTL, logger, memory, optional time
     provider, and optional timer factory;
   - `StartCarrier` starts `DirectRrChannelServer`;
   - `StopCarrier` stops the server carrier;
   - `SendFrame(destination, frame)` delegates to the server carrier;
   - inbound frames go directly to `OnFrameReceived` (concurrent per client is
     allowed); a channel disposal calls `OnSessionClosed(source)`.
3. Add `RR/Reliable/RRReliableDirectBinding.cs` (public static):
   - scheme `direct-rr-reliable`, `TransportType.RRReliable`;
   - parameters: `id` required positional, `request_ttl` client-only,
     `response_ttl` server-only, both optional positive seconds;
   - defaults 3 s/3 s from `RRDirectInfo`;
   - reject `delivery` and the wrong-role TTL.
4. Add `RR/Reliable/Specification.md` following the nine-section
   implementation profile: scheme, parameters, lifecycle (lazy connect,
   no-server `Error`, channel-loss failure), failure/deviations/protection,
   implementation-defined choices (wire correlation, timer factory, dispatch,
   capacity), and checklist.
5. Add the RRReliable Direct Test Suit under
   `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableDirect/`:
   - README and Coverage per `Tests/RULES.md`;
   - E2E tests: request with multiple ordered responses then `Finish(Normal)`;
     `Reject` → `Finish(Rejected)`; cancel → `Finish(Canceled)`; request TTL and
     response TTL → `Finish(Failed)` using the manual timer factory; stop
     finishes pending requests with `Failed`; no server → `Error` and no
     callbacks; channel loss → `Failed`; server `Init` one-time and
     start-before-init; handler reuse after `Finish`; invalid/oversized
     payloads; two clients have independent sessions.
   - category `Direct.RRReliable`.
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
```

Expected: build 0 errors; RRReliable suite and factory tests pass; the full
Direct and transport test projects remain green.

**Definition of done.** `direct-rr-reliable` compiles and runs for both roles;
the E2E suite proves normal, reject, cancel, TTL, stop, no-server, and
channel-loss behavior; the binding and specification exist; factory tests pass;
no Raw behavior regressed.

**Risks / notes.** `Start` succeeds without a server; the first `Request`
returns `Error` and binds no handler. A channel disposal after the first
successful session is a transport failure, not a retry signal.

## Global definition of done

- `direct-rr-unreliable` and `direct-rr-reliable` bindings exist, compile for
  client and server roles, and are registered in the factory tests.
- Both Direct transports run over `Channel`/`ChannelManager` and satisfy their
  family specifications.
- `RRUnreliable` uses the existing core; `RRReliable` uses a new reusable core.
- TTL defaults are 3 s/3 s; `delivery` netem is available on RRUnreliable.
- No-server behavior is exactly: RRReliable `Error`, RRUnreliable `Ok` plus
  silent expiry.
- Test Suits with README and Coverage exist for the three new suites.
- `dotnet build Pontifex.sln` succeeds with 0 errors and all Direct,
  transport, and factory tests pass.
- Working tree is clean after the stage commits; the plan file is removed at the
  end of execution.
