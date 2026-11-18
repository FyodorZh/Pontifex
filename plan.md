# Unify Reliable Ack/NoAck server plumbing (part 1)

## Goal

After this plan is complete, five pieces of duplicated behaviour between the reliable Ack and
NoAck implementations exist exactly once, in the shared reliable layer: the admission helpers of
the server transports, the connection/disconnection notification machinery of the endpoints, the
`GetSafe`/`Test` handler wrappers, the server socket listener, and the server-handler logging
wrapper. Ack and NoAck become thin protocol deltas over those shared bases, and the NoAck
protocol gains logging wrappers plus a `log-noack(...)` factory binding. The plan adds API surface but
changes existing behaviour only where the next paragraph says so.

Deliberate behaviour/API changes (the complete list):

1. `GetSafe(...)` now also guards `OnConnected`. Today a throwing user handler escapes the "safe"
   wrapper on that one callback.
2. Handler log lines of `RawReliableAckServerLogger` move from the static `Scriba.Log` sink to the
   transport's `ILogger` (`_core.Log`), matching `RawReliableAckClientLogger`. The transport and
   acknowledger log lines of the same class keep their current static `Scriba.Log` sink; this plan
   does not move them.
3. The public static class `Pontifex.Raw.Reliable.Ack.IRawAckServerHandler_Ext` is replaced by
   `Pontifex.Raw.Reliable.IRawReliableServerHandler_Ext`. All in-repo call sites keep compiling;
   external callers must switch namespace/class name. This is the only removed public type.

Additive API (not a behaviour change): new protected members on `RawReliableEndpoint`,
`RawReliableServerEndpoint` and `RawReliableServerTransport<TServerHandler>`; new extension
overloads for the NoAck marker; new public logging wrappers and a factory binding for NoAck.

## Context

`Pontifex` is a generalized transport library. Reliable transports come in two protocol flavours:
Ack (with an acknowledgement handshake) and NoAck (no handshake). Both grew in parallel and still
carry near-identical code:

- `RawReliableServerTransport<TServerHandler>` (`Pontifex/Implementations/Raw/Reliable/RawReliableServerTransport.cs`)
  owns admission gates, the session registry, and the ticker. Its protocol subclasses
  `RawReliableAckServerTransport` and `RawReliableNoAckServerTransport` each re-declare four
  byte-identical admission helpers (`TryBeginAdmission`, `CompleteAdmission`, two
  `AbandonAdmission` overloads). The only protocol difference is the stop-once wrapper the handler
  arrives in (`StopOnceAckServerHandler` / `StopOnceNoAckServerHandler`).
- `RawReliableEndpoint` → `RawReliableClientEndpoint` / `RawReliableServerEndpoint` →
  Ack/NoAck endpoint pairs. The four leaf endpoints each re-implement the same connect-once /
  disconnect-once / `IsConnected` latch logic and the same `OnConnected` exception handling.
- `Pontifex.Raw.Reliable.Ack.IRawAckServerHandler_Ext` provides `GetSafe`/`Test` wrappers only for
  `IRawReliableAckServerHandler`.
- `RawReliableAckServerLogger` bundles transport + acknowledger + handler logging; NoAck has no
  logging wrapper and no `log` factory binding at all.
- `IServerSocketListener`/`AsyncServerSocketListener` live in `Pontifex.Raw.Reliable.Ack.Tcp`
  although the NoAck TCP server transport already uses them (an explicit "namespace debt" note in
  that file).

A previous, already-committed change (`796b5a1 Move OnConnected into IRawReliableServerHandler`)
made `void OnConnected(IRawReliableEndpoint)` a member of the shared `IRawReliableServerHandler`,
leaving `IRawReliableAckServerHandler` and `IRawReliableNoAckServerHandler` empty marker
interfaces. That is what makes the shared wrappers, logger and endpoint machinery possible now.

Scope: items 1, 3, 4, 5 and 7 of the unification inventory agreed with the user. Item 6
(client-transport dedup) is deliberately excluded; see [Deferred follow-up](#deferred-follow-up).

## Constraints

- Every `.csproj` keeps the settings required by `AGENTS.md`: `Nullable=enable`,
  `LangVersion=9` for `netstandard2.1` (all production projects here) and `14` for `net10.0`
  (test projects), `ImplicitUsings=disable` in production, `TreatWarningsAsErrors=true` in the
  library projects.
- Production code must compile with C# 9. No file-scoped namespaces in `netstandard2.1`
  projects; the existing style uses block namespaces.
- XML documentation style follows the surrounding files: `<summary>` plus `<remarks>` with
  `<list type="bullet">` items for thread safety, pre/postconditions, and other notes. New
  production public/protected members carry documentation.
- No public type renames and no namespace moves of public types. The allowed public API changes
  are exactly: the new extension class replacing `IRawAckServerHandler_Ext` (stage 2); the four
  admission helpers moving from the two protocol server transports to
  `RawReliableServerTransport<TServerHandler>` (stage 3 — source-compatible for callers, but
  binary-breaking for external subclasses of the protocol transports); new protected members on
  existing types (stage 5); and new public logging/binding types for NoAck (stage 9).
  `RawReliableAckServerLogger` keeps all three of its interfaces (stage 8).
- Every stage's build verification is `dotnet build Pontifex.sln -p:NuGetAudit=false`. Because
  `Terminal.UICommon`, `Pontifex.TestUI` and `Pontifex.Tests` do not promote warnings to errors,
  each stage additionally compares the warning set with the documented baseline: 24 warnings at
  the start of the plan, all pre-existing, distributed as 16 `Terminal.UICommon`, 3
  `Pontifex.Tests`, 5 `Pontifex.TestUI`. A stage may not add a warning.
- `Tests/RULES.md` is normative for every test suite touched: one layer per abstract type
  (ATS-01), derived layers cover only their own delta and never duplicate an ancestor
  (ATS-09/ATS-10, EVOL-11), `Coverage.md` updated in the same change as the tests
  (COVERAGE-11, EVOL-03), harness is test-only, hand-written, and never referenced across sibling
  layers (HARNESS-03/05/09).
- Test fixtures keep the suite category (`[Category("Endpoints")]` or
  `[Category("Transports")]`); loose test files (stages 2 and 8) follow §9 of `Tests/RULES.md`.
- Builds must be run with `-p:NuGetAudit=false`. This machine has no NuGet network access and
  `NU1900` is promoted to an error in the library projects, which makes a plain `dotnet build`
  fail before compiling anything. This is an environment constraint, not a code issue.
- One git commit per stage, message `<imperative description> (stage N)`.

## Non-goals

- Item 6 of the earlier inventory: deduplicating `RawReliableAckClientTransport` and
  `RawReliableNoAckClientTransport`. Excluded by the user; see
  [Deferred follow-up](#deferred-follow-up).
- Moving the connection notification into `RawReliableEndpoint.FailHard` or the base stop path.
  The base deliberately keeps the notification call site explicit in each endpoint so the Ack
  "handshake pending" and Ack-server "OnConnected threw" paths keep their current behaviour.
  Consequently no test moves from a leaf layer to `RawReliableEndpoint`; the leaf layers' tests
  cover their own hooks, which is their contract delta.
- Deduplicating the TCP/UDP carrier endpoints (`RawReliable*TcpClientEndpoint`,
  `RawReliable*TcpServerEndpoint`) or the carrier server transports beyond the listener move.
- Changing the Ack handshake state machine, waiting-room logic, ack timeouts, or
  `RawReliableAckInfo`.
- Changing client-side `OnConnected` signatures (`IRawReliableAckClientHandler.OnConnected`
  keeps its `ackResponse` parameter). No shared property may be introduced for the client
  handler: `IRawReliableClientHandler` declares no callbacks.
- Renaming `RawAckServerSideEndpointWrapper` / `RawAckClientSideEndpointWrapper` or their
  namespace. The user rejected the rename; the NoAck loggers reuse them as-is.
- Reworking the Ack logger's single-instance handler state (a latent issue for concurrent Ack
  sessions). Stage 8 preserves that behaviour exactly.
- Fixing the pre-existing unused variable/dead code in `RawReliableClientTransport.Init`
  (commented-out `processedHandler.Test(...)` line).

## Assumptions

- `IRawReliableAckServerHandler` and `IRawReliableNoAckServerHandler` are empty marker
  interfaces today. Evidence:
  `Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Server/IRawReliableAckServerHandler.cs`,
  `.../NoAck/Server/IRawReliableNoAckServerHandler.cs` at commit `796b5a1`.
- C# allows one class to implement two interfaces that share an inherited member set; with both
  markers empty, a single wrapper class can implement both.
- The low-level `TransportConstructorRegistry` keys entries by `(scheme, TransportType)`, but the
  public builder path used by `Pontifex.TestUI` and `FactoryBindingsTests` rejects a duplicate
  canonical scheme name regardless of type (`TransportRegistryBuilder`, duplicate-canonical-name
  rule), and the selector prefers the canonical constructor for a scheme. A second `log`
  registration is therefore impossible. Evidence: `Pontifex.Factory/TransportConstructorRegistry.cs`,
  `Pontifex.TransportFormula/Registry/Internal/TransportRegistryBuilder.cs`,
  `Pontifex.TransportFormula/Semantics/Internal/ConstructorSelector.cs`. The NoAck binding
  therefore uses its own scheme, `log-noack`, mirroring the `tcp` / `tcp-noack` convention.
- Every reliable carrier endpoint calls `base.BeginDisconnect(...)` from its own
  `BeginDisconnect` override. Evidence: `RawReliableAckDirectClientEndpoint.cs:112`,
  `RawReliableAckDirectServerEndpoint.cs:112`, `RawReliableAckTcpClientEndpoint.cs:227`,
  `RawReliableAckTcpServerEndpoint.cs:569`, `RawReliableNoAckTcpServerEndpoint.cs:293`,
  `RawReliableNoAckTcpClientEndpoint.cs:244`. This plan keeps `BeginDisconnect` abstract and
  per-endpoint, so the evidence only confirms that no carrier overrides it without delegating.
- `RawReliableEndpoint` and **only** the four leaf endpoints override `GetIsConnected`;
  `RawReliableClientEndpoint` and `RawReliableServerEndpoint` do not. Evidence: `grep -n
  "GetIsConnected" Pontifex/Implementations/Endpoints/Raw/Reliable`. The base predicate change in
  stage 5 is therefore unobservable until a leaf is migrated in stages 6-7, and is never
  observable for the protocol base layers.
- `IRawReliableClientHandler` is an empty marker; `OnConnected` and `FillAckData` live only on the
  protocol client handlers. The two client leaf endpoints therefore keep their own typed
  `_handler` fields.
- `Tests/Pontifex.Tests/Endpoints/README.md` and `.../Transports/README.md` layer tables are
  authoritative; this plan adds, removes and re-parents no layer.
- Baseline test counts measured at the start of this plan: `Pontifex.Tests` Endpoints 156,
  Transports 210; `Pontifex.Transport.Net.Tests` 306; whole solution 2230 passed / 67 skipped
  (the skips are pre-existing attribute-based skips in `Pontifex.Transports.Tests`).
- A clean solution build reports 24 pre-existing warnings in `Terminal.UICommon`,
  `Pontifex.TestUI` and `Pontifex.Tests`; the five library projects build warning-free because
  `TreatWarningsAsErrors=true` is set there.
- The `Test(...)` extension is `#if DEBUG`-only. Test projects build Debug by default, so a
  loose test may exercise it; this plan nevertheless tests only `GetSafe` (stage 2).

## Prerequisites

- Working tree clean and on a branch that contains `796b5a1`.
- `dotnet` SDK able to build `net10.0` and `netstandard2.1` (validated: the solution builds and
  the full test suite passes at the start of this plan).
- No NuGet network. Always pass `-p:NuGetAudit=false` to `dotnet build`/`dotnet test`.
- Plan file `plan.md` committed at the repository root.

## Stages

### Stage 1 — Move the server socket listener to the shared socket namespace

**Objective.** `IServerSocketListener` and `AsyncServerSocketListener` live in the carrier-neutral
`Pontifex.NetSockets.Tcp` namespace used by both reliable server transports, and the "namespace
debt" note is gone.

**Inputs.** Clean tree at `796b5a1`.

**Actions.**

1. Move `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/Server/IServerSocketListener.cs`
   to `Transports/Pontifex.Transport.Net/Common/ServerSocketListener/IServerSocketListener.cs`
   and change its namespace from `Pontifex.Raw.Reliable.Ack.Tcp` to `Pontifex.NetSockets.Tcp`.
   Keep it `internal`.
2. Move `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/Server/AsyncServerSocketListener.cs`
   to `Transports/Pontifex.Transport.Net/Common/ServerSocketListener/AsyncServerSocketListener.cs`,
   same namespace change, still `internal`. Keep the body byte-identical.
3. `RawReliableAckTcpServerTransport.cs` already imports `Pontifex.NetSockets.Tcp` and has no
   `using Pontifex.Raw.Reliable.Ack.Tcp;`; change nothing in that file.
   `RawReliableNoAckTcpServerTransport.cs` has both `using Pontifex.NetSockets.Tcp;` and
   `using Pontifex.Raw.Reliable.Ack.Tcp;`. Delete only the latter (a duplicate `using` would be
   CS0105, an error in this project).
4. In `RawReliableNoAckTcpServerTransport.cs`, delete the `<item><description><b>Namespace
   debt</b>...` bullet from the class `<remarks>` (the debt no longer exists).
5. Delete the now-empty directory `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/Server/`
   if no other file remains in it. That directory contains only the two moved files.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.` with no new
  warnings.
- `ls Transports/Pontifex.Transport.Net/Common/ServerSocketListener` → exactly
  `AsyncServerSocketListener.cs` and `IServerSocketListener.cs`.
- `grep -rn "namespace Pontifex.NetSockets.Tcp" Transports/Pontifex.Transport.Net/Common/ServerSocketListener`
  → 2 matches.
- `ls Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/Server` → `No such file or directory`.
- `grep -n "using Pontifex.Raw.Reliable.Ack.Tcp;" Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/RawReliableAckTcpServerTransport.cs Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/NoAck/RawReliableNoAckTcpServerTransport.cs`
  → no matches.
- `grep -n "Namespace debt" Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/NoAck/RawReliableNoAckTcpServerTransport.cs`
  → no matches.
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj --no-build --nologo`
  → 306 tests, all pass.

**Definition of done.** Both listener files exist under `Common/ServerSocketListener/` in
namespace `Pontifex.NetSockets.Tcp`; both server transports compile against them; the debt note is
gone; the transport integration tests pass.

**Risks / notes.** Both listener types are `internal`; moving them inside the same assembly
cannot break external consumers. The Ack namespace stays for the Ack carrier types and is not
touched.

### Stage 2 — Share `GetSafe`/`Test` across both server handler contracts

**Objective.** One handler-wrapper implementation serves both marker interfaces; the Ack-only
extension class is replaced by the shared one; `GetSafe` also guards `OnConnected`; a loose test
file covers it.

**Inputs.** Stage 1 committed. `IRawReliableAckServerHandler` and `IRawReliableNoAckServerHandler`
are empty markers.

**Actions.**

1. Create `Pontifex/Implementations/Raw/Reliable/Ext/IRawReliableServerHandler_Ext.cs`, namespace
   `Pontifex.Raw.Reliable`, `public static class IRawReliableServerHandler_Ext`, with:
   - `public static IRawReliableAckServerHandler Test(this IRawReliableAckServerHandler core, Action<string> onFail)`
   - `public static IRawReliableNoAckServerHandler Test(this IRawReliableNoAckServerHandler core, Action<string> onFail)`
   - `public static IRawReliableAckServerHandler GetSafe(this IRawReliableAckServerHandler core, Action<Exception> onFail)`
   - `public static IRawReliableNoAckServerHandler GetSafe(this IRawReliableNoAckServerHandler core, Action<Exception> onFail)`
   - `private sealed class SafeServerHandler : IRawReliableAckServerHandler, IRawReliableNoAckServerHandler`
     holding `IRawReliableServerHandler _handler` and `Action<Exception> _onException`, wrapping
     every callback (`OnConnected`, `OnDisconnected`, `OnReceived`, `OnStopped`) in
     `try { ... } catch (Exception e) { _onException(e); }`.
   - `private sealed class TestServerHandler : InvariantChecker<TestServerHandler.HandlerState>, IRawReliableAckServerHandler, IRawReliableNoAckServerHandler`
     with the existing state machine (`Constructed → Connected → Disconnected`, `OnStopped`
     unguarded, `OnReceived` requires at least `Connected`), the existing `ToString()` override,
     and the `#if DEBUG`-guarded factory behaviour of today's `IRawAckServerHandler_Ext`.
   - Both `Test` overloads return `core` unchanged when `DEBUG` is not defined.
   - XML documentation on all four public methods (`<summary>`, `<param>`, `<returns>`).
2. Copy the bodies from `Pontifex/Implementations/Raw/Reliable/Ack/Ext/IRawAckServerHandler_Ext.cs`
   and delete that file. The only intentional behaviour change in the copy:
   `SafeServerHandler.OnConnected` now catches and reports exceptions through `_onException`,
   matching the other three callbacks.
3. Confirm the existing call sites compile unchanged:
   `Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckServer.cs:134` (`.Test(...).GetSafe(...)`),
   `Protocols/Pontifex.Protocol/Raw/Reliable/Ack/Server/AcknowledgerWrapper.cs:75`, and
   `Pontifex/Converters/RawUnreliableNoAck/RawReliableAck/RawUnreliableNoAckToRawReliableAckServer.cs:143`.
   Add `using Pontifex.Raw.Reliable;` only where the enclosing namespace is not
   `Pontifex.Raw.Reliable.*`.
4. Create the loose test file `Tests/Pontifex.Tests/RawReliableServerHandlerExtTests.cs`
   (`namespace Pontifex.Tests;`, `[TestFixture]`, no suite category — loose file). For **each**
   overload (`GetSafe` on `IRawReliableAckServerHandler` and on `IRawReliableNoAckServerHandler`)
   and **each** of the four callbacks, write one forwarding test and one exception test:
   - forwarding: the callback reaches the wrapped handler with unchanged arguments;
   - exception: the wrapped handler throws, `onFail` receives that exception, and nothing escapes.
   That is 16 tests. Use a hand-written recording handler implementing the marker interface.
   Do not test `Test(...)` (DEBUG-only); put that decision in a short comment at the top of the
   fixture.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter FullyQualifiedName~RawReliableServerHandlerExt`
  → 16 tests, all pass.
- `grep -rn "IRawAckServerHandler_Ext" --include=*.cs .` → no matches.

**Definition of done.** The Ack-only extension file is deleted, the shared class provides both
overloads, all production call sites compile, and the loose tests pass.

**Risks / notes.** One instance implements both markers; it is never handed to a transport as
both, so the extra claim is unobservable. The `OnConnected` catch is a deliberate fix (Goal item
1). The public type removal is Goal item 3.

### Stage 3 — Hoist the admission helpers into `RawReliableServerTransport<TServerHandler>`

**Objective.** `TryBeginAdmission`, `CompleteAdmission`, and both `AbandonAdmission` overloads
exist once in the shared server transport and take `IRawReliableServerHandler?`; the protocol
transports no longer declare them.

**Inputs.** Stage 2 committed.

**Actions.**

1. In `Pontifex/Implementations/Raw/Reliable/RawReliableServerTransport.cs`, add (copying the XML
   documentation from the Ack transport, replacing "acknowledger"/"factory" with "session
   handler"):
   - `protected bool TryBeginAdmission()`
   - `protected bool CompleteAdmission(IRawReliableServerEndpointImp session, IRawReliableServerHandler? handler, StopReason failureReason)`
   - `protected void AbandonAdmission()`
   - `protected void AbandonAdmission(IRawReliableServerHandler? handler, StopReason reason)`
   Bodies are identical to the existing ones; they use only `TryReserveSession`,
   `TryBeginHandshake`, `EndHandshake`, `ReleaseSession`, `TryStartAndRegisterSession`, `Name`,
   `Log`.
2. Delete the four members from
   `Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckServerTransport.cs` and
   `.../NoAck/RawReliableNoAckServerTransport.cs`. Update any `<see cref="CompleteAdmission(...)"/>`
   / `<see cref="AbandonAdmission(...)"/>` crefs in the remaining members of both files so they
   name the new handler parameter type.
3. Keep the protocol test doubles' exposed wrappers
   (`.../Server/Ack/Harness/TestRawReliableAckServerTransport.cs`,
   `.../Server/NoAck/Harness/TestRawReliableNoAckServerTransport.cs`), changing only the handler
   parameter type to `IRawReliableServerHandler?`. Do not rename the exposed members: the protocol
   admission tests keep calling them.
4. Do not change `TryCreateHandler`, `TryAcknowledge`, or the `StopOnce*` wrappers.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter TestCategory=Transports`
  → 210 tests, all pass.
- `grep -n "protected bool TryBeginAdmission\|protected bool CompleteAdmission\|protected void AbandonAdmission" Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckServerTransport.cs Pontifex/Implementations/Raw/Reliable/NoAck/RawReliableNoAckServerTransport.cs`
  → no matches (declarations only; documentation crefs may still name the members).

**Definition of done.** The shared base owns the admission helpers; both protocol transports
compile without them; the Transports suite passes unchanged.

**Risks / notes.** This is a protected-surface change: any external subclass calling
`CompleteAdmission` with a concrete handler still compiles because the concrete handler converts
to `IRawReliableServerHandler`, but code compiled against the old declaring type must be rebuilt,
and a subclass that overrides nothing is unaffected. Do not also delete `TryBeginHandshake` /
`TryReserveSession`: those stay as the low-level gates.

### Stage 4 — Cover the shared admission helpers at the `RawReliableServerTransport` layer

**Objective.** The layer that now owns the admission helpers proves their mechanics with a plain
handler. The protocol layers keep their existing gate tests, which prove the protocol-specific
stop-once delta.

**Inputs.** Stage 3 committed.

**Actions.**

1. Add the three missing exposure wrappers to
   `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/Harness/TestRawReliableServerTransport.cs`,
   mirroring the protocol doubles:
   - `public bool ExposedTryBeginAdmission() => TryBeginAdmission();`
   - `public bool ExposedCompleteAdmission(IRawReliableServerEndpointImp session, IRawReliableServerHandler? handler, StopReason failureReason) => CompleteAdmission(session, handler, failureReason);`
   - `public void ExposedAbandonAdmission(IRawReliableServerHandler? handler, StopReason reason) => AbandonAdmission(handler, reason);`
   - `public void ExposedAbandonAdmission() => AbandonAdmission();`
2. Add `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/Harness/RecordingRawAdmissionServerHandler.cs`
   (`internal sealed class`, namespace `Pontifex.Tests.Transports`): implements
   `IRawReliableServerHandler`, records `StoppedCount`, `LastStopReason`, `ReceivedCount`, and
   `DisconnectedCount`. Hand-written (HARNESS-09), no `IDisposable`.
   The name must not be `RecordingRawReliableServerHandler`: that type exists in
   `Pontifex.Tests.Endpoints` and is imported by `RawReliableServerTransportTests.cs`, where a
   same-named type in the file's own namespace would silently rebind every existing use.
3. Add a `#region Admission` to
   `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/RawReliableServerTransportTests.cs` with
   six new tests written against that plain recording handler (not moved from a protocol layer):
   - `TryBeginAdmission_ReservesSessionAndHandshakeSlots` — construct the transport with
     `maxConcurrentHandshakes: 1, maxActiveSessions: 1`; after a successful call, both gates are
     exhausted (`ExposedTryBeginHandshake()` and `ExposedTryReserveSession()` return false).
   - `TryBeginAdmission_WhenHandshakeGateFull_ReleasesTheSessionSlot` — same 1/1 limits; occupy
     the handshake gate with `ExposedTryBeginHandshake()`, then assert `TryBeginAdmission` fails
     and `ExposedActiveSessions` is zero.
   - `AbandonAdmission_WithHandler_NotifiesOnStopped` — passes
     `RecordingRawAdmissionServerHandler` and asserts `StoppedCount == 1` and both gates released.
   - `AbandonAdmission_WithoutHandler_ReleasesBothSlots` — parameterless overload; gates released,
     no handler involved.
   - `CompleteAdmission_WhenSessionCannotStart_NotifiesOnStopped` — `TestRawReliableServerSession`
     with `Queue.RejectNextSchedule = true`; `StoppedCount == 1`, session slot released.
   - `CompleteAdmission_WhenSessionStartsWithTransportStopping_DoesNotNotifyOnStopped` — mirrors
     the protocol test without a stop-once gate: the accepted session owns the terminal
     notification, so `StoppedCount` is zero until the session's queue is drained.
4. Do **not** delete or reword the existing protocol admission tests
   (`AbandonAdmission_WithHandler_NotifiesOnStoppedOnce`,
   `CompleteAdmission_WhenSessionCannotStart_NotifiesOnStoppedOnce`,
   `CompleteAdmission_WhenTransportStopping_DoesNotPreemptStartedSession` in both protocol test
   files). They prove that the stop-once wrappers installed by `TryAcknowledge` /
   `TryCreateHandler` deduplicate a second notification — a delta the shared helper does not have.
5. Update `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/Coverage.md` with the six new rows
   and a note that these tests use a raw handler, so a second `OnStopped` would double-count; the
   protocol layers own the stop-once assertions.
6. Do not edit `.../Server/Ack/Coverage.md` or `.../Server/NoAck/Coverage.md`: their tests are
   unchanged.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter TestCategory=Transports`
  → 216 tests, all pass (210 + 6).
- `git diff --stat` for this stage → the two harness files, the base test file, and one
  `Coverage.md`.

**Definition of done.** The shared helper mechanics are covered at their own layer; every protocol
admission test is untouched and still passes.

**Risks / notes.** Server-session doubles drive their own queue; the two `CompleteAdmission` tests
must use `DrainAll()` (or `Queue.RejectNextSchedule`) exactly like the protocol tests do, or the
slot accounting assertions become timing-dependent.

### Stage 5 — Add shared connection-notification machinery to the endpoint base

**Objective.** `RawReliableEndpoint` owns the handler reference, the connected/disconnected
latches, the notification helpers and the disconnect-aware `IsConnected` predicate;
`RawReliableServerEndpoint` exposes `NotifyConnected()`. Nothing calls the new helpers yet, so no
endpoint behaviour changes.

**Inputs.** Stage 4 committed.

**Actions.**

1. In `Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableEndpoint.cs`:
   - add `private readonly IRawReliableHandler _handler;` assigned in the constructor;
   - add `protected IRawReliableHandler Handler => _handler;`
   - add `private int _connectedNotified;` and `private int _disconnectedNotified;`
   - `protected void MarkConnectedNotified()` → `Volatile.Write(ref _connectedNotified, 1);`
   - `protected bool NotifyConnectedCore(Action onConnected)`: marks the latch, then
     `try { onConnected(); return true; } catch (Exception ex) { Log.wtf(ex);
     Stop(new ExceptionFail(TransportName, ex, "OnConnected failed"), false); return false; }`.
   - `protected void NotifyDisconnectedCore(StopReason reason)`:
     `if (Interlocked.Exchange(ref _disconnectedNotified, 1) == 0 && Volatile.Read(ref _connectedNotified) != 0) { try { _handler.OnDisconnected(reason); } catch (Exception ex) { Log.wtf(ex); } }`.
     The name must not be `NotifyDisconnected`: both NoAck leaf endpoints still declare a private
     `NotifyDisconnected` with that signature until stage 6, and the resulting CS0108 would be an
     error in `Pontifex`.
   - change `protected virtual bool GetIsConnected()` from `=> IsLowLevelConnected` to
     `=> IsLowLevelConnected && Volatile.Read(ref _disconnectedNotified) == 0`.
   - add `using System.Threading;` if absent.
   - Keep `FailHard`, `BeginStop`, `BeginDisconnect` and the stage machine exactly as they are:
     the base does **not** auto-notify. The four leaf endpoints keep their own hooks and call the
     helper explicitly (stages 6-7).
2. In `RawReliableServerEndpoint.cs`: add
   `private readonly IRawReliableServerHandler _handler;` (assigned in the constructor, which
   already receives it), `protected IRawReliableServerHandler ServerHandler => _handler;`, and
   `protected bool NotifyConnected()` → `NotifyConnectedCore(() => _handler.OnConnected(this))`.
3. Do not add any client-handler property: `IRawReliableClientHandler` declares no callbacks, and
   `OnConnected`/`FillAckData` are protocol-specific. The two client leaf endpoints keep their own
   typed `_handler` fields.
4. Update the two layer harnesses:
   - `Tests/Pontifex.Tests/Endpoints/RawReliable/Harness/TestRawReliableEndpoint.cs`: expose
     `ExposedMarkConnectedNotified()`, `ExposedNotifyConnectedCore(Action)`, and
     `ExposedNotifyDisconnectedCore(StopReason)`. Its `BeginDisconnect` override keeps recording
     only.
   - `Tests/Pontifex.Tests/Endpoints/RawReliable/Server/Harness/TestRawReliableServerEndpoint.cs`:
     expose `ExposedNotifyConnected()`.
5. Add base-layer tests to `Tests/Pontifex.Tests/Endpoints/RawReliable/RawReliableEndpointTests.cs`.
   The two throwing cases use test-local private doubles declared inside the fixture file (the
   layer recorders cannot throw): `ThrowingRawReliableHandler` for
   `IRawReliableHandler.OnDisconnected` and `ThrowingRawReliableServerHandler` for
   `IRawReliableServerHandler.OnConnected`.
   - `NotifyDisconnected_WhenConnected_NotifiesOnce` — `ExposedMarkConnectedNotified()`, then
     `ExposedNotifyDisconnectedCore` twice; the handler is called once.
   - `NotifyDisconnected_WhenNeverConnected_DoesNotNotify`.
   - `NotifyDisconnected_WhenHandlerThrows_DoesNotThrowOutOfTheEndpoint` — connected first; the
     throwing handler is called once and no exception escapes the helper.
   - `IsConnected_AfterDisconnectNotification_IsFalse` — connect the endpoint through
     `ConnectAndComplete()` first, then notify; assert `IsConnected` is false inside the handler
     callback (the recording handler takes a probe) and after it.
   - `NotifyConnectedCore_WhenCallbackThrows_StopsWithExceptionFail` — the callback throws;
     assert the endpoint stopped with an `ExceptionFail` whose source is the transport name and
     that `NotifyConnectedCore` returned false. (Use the server-layer test for the
     `NotifyConnected` variant; this one drives `NotifyConnectedCore` directly through the double.)
6. Add server-layer tests to
   `Tests/Pontifex.Tests/Endpoints/RawReliable/Server/RawReliableServerEndpointTests.cs`
   (`NotifyConnected_...` uses the test-local `ThrowingRawReliableServerHandler`):
   `NotifyConnected_ForwardsToHandlerAndMarksConnected` and
   `NotifyConnected_WhenHandlerThrows_StopsWithExceptionFail`.
7. Update `Tests/Pontifex.Tests/Endpoints/RawReliable/Coverage.md` and
   `.../Server/Coverage.md` with the new rows.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter TestCategory=Endpoints`
  → 163 tests, all pass (156 + 7). Existing tests must not change behaviour: every leaf endpoint
  still overrides `GetIsConnected`, and the base still does not notify.
- `git diff --stat` for this stage → `RawReliableEndpoint.cs`, `RawReliableServerEndpoint.cs`,
  `TestRawReliableEndpoint.cs`, `TestRawReliableServerEndpoint.cs`,
  `RawReliableEndpointTests.cs`, `RawReliableServerEndpointTests.cs`, and two `Coverage.md`
  files.

**Definition of done.** The shared helpers exist and are covered at their own layers; no leaf
endpoint uses them yet; the whole Endpoints suite is green.

**Risks / notes.** `RawEndpoint` keeps its own `private IRawHandler _handler`; the reliable layer
now holds a second reference to the same object. That is intentional: `RawEndpoint` drives
`OnReceived` and the terminal stop notification. Do not remove its field. All three handler fields
(`RawEndpoint`, `RawReliableEndpoint`, `RawReliableServerEndpoint`) are private, so a same-named
private field in a derived class is not a hiding warning; the protocol client endpoints keep their
own typed `_handler` fields, which are likewise private and do not collide.

### Stage 6 — Adopt the shared machinery in the NoAck endpoints

**Objective.** Both NoAck endpoints lose their duplicated latches, private `NotifyDisconnected`
and `GetIsConnected`; their `FailHard`/`BeginDisconnect` hooks become one-liners over the shared
helper, and `OnConnected` uses the shared guarded helper.

**Inputs.** Stage 5 committed.

**Actions.**

1. `RawReliableNoAckServerEndpoint.cs`:
   - delete `_handler`, `_connectedNotified`, `_disconnectedNotified`, `GetIsConnected`, and the
     private `NotifyDisconnected`;
   - **keep** `FailHard` and `BeginDisconnect` as:
     `protected override void FailHard(StopReason reason) { NotifyDisconnectedCore(reason); base.FailHard(reason); }`
     and `protected override void BeginDisconnect(StopReason reason, bool tryBeGentle) { NotifyDisconnectedCore(reason); }`.
     They must stay: `RawReliableEndpoint.BeginDisconnect` is abstract, and the carrier endpoints
     (`RawReliableNoAckTcpServerEndpoint.cs:293`, `RawReliableNoAckTcpClientEndpoint.cs:244`) call
     `base.BeginDisconnect`, which would otherwise be CS0205;
   - `OnConnected()` becomes `if (!NotifyConnected()) { return; } base.OnConnected();`;
   - the constructor no longer stores the handler.
2. `RawReliableNoAckClientEndpoint.cs`:
   - same deletions, but **keep** the typed `private readonly IRawReliableNoAckClientHandler _handler;`
     field (the client handler's `OnConnected` is not on the shared interface);
   - keep `FailHard`/`BeginDisconnect` as the same one-liners;
   - `OnConnected()` becomes
     `if (!NotifyConnectedCore(() => _handler.OnConnected(this))) { return; } base.OnConnected();`.
3. Leave the class `<remarks>` intact except for statements that are no longer true about fields
   or methods that were removed.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter TestCategory=Endpoints`
  → 163 tests, all pass, with every NoAck test unedited.
- `grep -n "GetIsConnected\|_disconnectedNotified\|private void NotifyDisconnected" Pontifex/Implementations/Endpoints/Raw/Reliable/NoAck/*.cs`
  → no matches.

**Definition of done.** Both NoAck endpoints have no notification machinery of their own beyond
the two one-line hooks, and their existing tests pass without edits.

**Risks / notes.** Do not "simplify" the two one-line hooks away: the base notification call site
is deliberately explicit so the Ack paths can keep their different ordering (stage 7). Keep the
notification before `base.FailHard`, which moves the reliable stage, exactly as today.

### Stage 7 — Adopt the shared machinery in the Ack endpoints

**Objective.** The Ack server and client endpoints use the shared latches and helper; the
disconnect block duplicated twice per file exists once per endpoint; existing Ack tests pass
unedited.

**Inputs.** Stage 6 committed.

**Actions.**

1. `RawReliableAckServerEndpoint.cs`:
   - delete `_handler`; use `ServerHandler`;
   - in `SendAckResponse`, keep `_stage = State.LogicallyConnected;` **before**
     `ServerHandler.OnConnected(this)` (the handler must observe a usable endpoint), and call
     `MarkConnectedNotified()` **after** the handler returns normally. Do not arm the latch before
     the call: the existing catch clears the stage and stops, and arming first would deliver an
     `OnDisconnected` that the contract says must not happen on that path;
   - keep the local `try/catch` in `SendAckResponse` unchanged (it sets
     `_stage = State.LogicallyDisconnected` and stops with `new ExceptionFail(TransportName, ex)`);
   - `FailHard`: `ReleasePendingAckResponse(); _stage = State.LogicallyDisconnected;
     NotifyDisconnectedCore(reason); base.FailHard(reason);`
   - `BeginDisconnect`: `ReleasePendingAckResponse(); _stage = State.LogicallyDisconnected;
     NotifyDisconnectedCore(reason);`
   - keep `GetIsConnected` and the `State` enum unchanged.
2. `RawReliableAckClientEndpoint.cs`:
   - keep the typed `_handler` field (`IRawReliableAckClientHandler`) for `FillAckData` and the
     two-argument `OnConnected`;
   - `FinishAckConnect`, `case State.AckRequestSent`: keep `_stage = State.LogicallyConnected;`
     and `base.OnConnected();` (keep-alive ordering), then replace the manual `try/catch` with
     `NotifyConnectedCore(() => _handler.OnConnected(this, message))` and ignore its result. The
     helper marks the latch, logs, and stops with the same `"OnConnected failed"` reason the
     current catch uses;
   - `FailHard`: `_stage = State.LogicallyDisconnected; NotifyDisconnectedCore(reason);
     base.FailHard(reason);`
   - `BeginDisconnect`: `_stage = State.LogicallyDisconnected; NotifyDisconnectedCore(reason);`
   - keep `GetIsConnected` and the `State` enum unchanged.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter TestCategory=Endpoints`
  → 163 tests, all pass. In particular these existing tests must pass unedited:
  `OnConnected_Throws_StopsWithExceptionFailAndNoOnDisconnected` (Ack server) and
  `FailHard_WhenHandshakePending_DoesNotNotifyOnDisconnected` (Ack server),
  `Disconnect_WhenHandshakePending_DoesNotNotifyOnDisconnected` and
  `FailHard_WhenHandshakePending_DoesNotNotifyOnDisconnected` (Ack client).
- `grep -c "_handler.OnDisconnected" Pontifex/Implementations/Endpoints/Raw/Reliable/Ack/Server/RawReliableAckServerEndpoint.cs Pontifex/Implementations/Endpoints/Raw/Reliable/Ack/Client/RawReliableAckClientEndpoint.cs`
  → `0` in both files.

**Definition of done.** Both Ack endpoints notify only through the shared helper, their tests pass
unedited, and each file's disconnect logic appears once.

**Risks / notes.** The Ack server's "OnConnected threw" path differs from every other endpoint by
design: it must report no `OnDisconnected`. Arming the latch after the handler call is what
preserves that. The Ack client arms it before the call (via the helper) and therefore keeps
today's `OnDisconnected`-after-throw behaviour.

### Stage 8 — Extract the shared server-handler logging wrapper

**Objective.** The handler-logging behaviour (log line + endpoint wrapper for `Send`/`Disconnect`
+ forward to the user handler) exists once in an internal helper; `RawReliableAckServerLogger`
uses it and keeps its public surface.

**Inputs.** Stage 7 committed.

**Actions.**

1. Create `Pontifex/Wrappers/Logger/RawReliableServerHandlerLogger.cs`, namespace
   `Pontifex.Raw.Reliable.Logger`, `internal sealed class RawReliableServerHandlerLogger :
   IRawReliableAckServerHandler, IRawReliableNoAckServerHandler`:
   - constructor `(ILogger log, IRawReliableServerHandler? userHandler)`, a settable
     `UserHandler` property, and a `private readonly ILogger _log;`
   - `OnConnected` logs `UserHandler.OnConnected()`, wraps the endpoint with
     `RawAckServerSideEndpointWrapper` (the same four arguments as today), forwards, and logs;
   - `OnDisconnected`, `OnReceived`, `OnStopped` log and forward exactly as
     `RawReliableAckServerLogger` does today, but through `_log` instead of the static
     `Scriba.Log` sink.
2. Rewrite `RawReliableAckServerLogger` to delegate to one helper instance:
   - keep the class signature and all three interfaces exactly as today;
   - replace `_userHandler` with `private readonly RawReliableServerHandlerLogger _handlerLogger;`
     constructed from `_core.Log`;
   - `TryAck` sets `_handlerLogger.UserHandler = result.Value.handler;` and returns
     `(this, result.Value.ackResponse)` as today;
   - the four handler callbacks delegate to `_handlerLogger`.
3. Do not change `RawReliableAckClientLogger` or the endpoint wrapper classes.
4. Create the loose test file `Tests/Pontifex.Tests/RawReliableServerHandlerLoggerTests.cs`
   (`namespace Pontifex.Tests;`, `[TestFixture]`): construct the helper with `new Logger([])` and
   a recording handler, then assert each callback is forwarded; `OnConnected` passes a wrapper
   instance (not the original endpoint) and the wrapper's `Send`/`Disconnect` operate on the
   original (use `null` as the wrapped endpoint and expect `SendResult.Error` / `false`); changing
   `UserHandler` redirects subsequent callbacks.
5. Create the loose test file `Tests/Pontifex.Tests/RawReliableAckServerLoggerTests.cs` to prove
   the rewritten class still delegates. Declare a test-local `FakeAckServer : IRawReliableAckServer`
   implementing the ten `ITransport` members plus `Init` (return stored values; `Log` returns
   `new Logger([])`), and a recording acknowledger. Drive `Init(recordingAcknowledger)` →
   `TryAck(remote, ackData)` → take the returned `IRawReliableServerHandler` and call all four
   callbacks. Assert each reached the user handler exactly once, that `OnConnected` received a
   wrapper rather than the original endpoint, and that a second `Init`/`TryAck` rewires
   `UserHandler` to the new session handler.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter FullyQualifiedName~RawReliableServerHandlerLogger`
  → all pass.
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter FullyQualifiedName~RawReliableAckServerLogger`
  → all pass.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --nologo`
  → all pass (the Ack `log` binding is compiled in `FactoryBindingsTests`).
- `grep -n "_userHandler" Pontifex/Wrappers/Logger/RawReliableAckServerLogger.cs` → no matches.

**Definition of done.** The Ack server logger keeps its public contract, gains no duplicated
logging bodies, and the shared helper is covered by loose tests.

**Risks / notes.** The helper is `internal`; `Pontifex.Tests` has `InternalsVisibleTo`, and no
external consumer can depend on it. Handler log lines of the Ack server logger now use the
transport logger (Goal item 2) and appear in a different sink than before — intended.
The Ack logger still holds one handler at a time; that pre-existing limitation is preserved.

### Stage 9 — Add NoAck logging wrappers and the NoAck `log-noack` binding

**Objective.** `log-noack(...)` works for `RawReliableNoAck` in both roles, built on the shared
handler wrapper; the binding is registered in the TestUI factory and covered by compile and
behaviour tests.

**Inputs.** Stage 8 committed.

**Actions.**

1. Create `Pontifex/Wrappers/Logger/RawReliableNoAckServerLogger.cs`, namespace
   `Pontifex.Raw.Reliable.NoAck.Logger`, `public sealed class RawReliableNoAckServerLogger :
   IRawReliableNoAckServer, IRawReliableNoAckServerHandlerFactory<IRawReliableNoAckServerHandler>`:
   - mirror `RawReliableAckServerLogger`'s transport forwarding (`Name`, `IsValid`, `IsStarted`,
     `Start`, `Stop`, `Log`, `Memory`, `GetControls`, `MessageMaxByteSize`, `Type`) using
     `_core.Log` for logging;
   - `Init(factory)` stores the user factory and returns `_core.Init(this)`;
   - `TryCreate(remote)` logs the call, invokes the user factory, and on a non-null handler
     returns `new RawReliableServerHandlerLogger(_core.Log, handler)`; an exception is logged and
     rethrown so the transport keeps its own rejection semantics.
2. Create `Pontifex/Wrappers/Logger/RawReliableNoAckClientLogger.cs`, namespace
   `Pontifex.Raw.Reliable.NoAck.Logger`, `public sealed class RawReliableNoAckClientLogger :
   IRawReliableNoAckClient, IRawReliableNoAckClientHandler`: mirror `RawReliableAckClientLogger`
   minus `FillAckData`, with `OnConnected(IRawReliableEndpoint)` wrapping the endpoint through
   `RawAckClientSideEndpointWrapper` (the same two lambdas, no controls array) and logging through
   `_core.Log`.
3. Create `Pontifex/Wrappers/Logger/RawReliableNoAckLoggerBinding.cs`, namespace
   `Pontifex.Raw.Reliable.NoAck.Logger`, `public static class RawReliableNoAckLoggerBinding` with
   `public const string SchemeName = "log-noack";` (a distinct canonical scheme is required: the
   builder rejects a second `log`). Mirror `RawReliableAckLoggerBinding` with
   `TransportType.RawReliableNoAck` and `TransportTypeSet.Exactly(TransportType.RawReliableNoAck)`.
   `Create` returns the server logger for `TransportRole.Server` and the client logger otherwise.
4. Register the binding in `Pontifex.TestUI/TransportFactory.cs` next to
   `RawReliableAckLoggerBinding.CreateRegistration()`.
5. In `Tests/Pontifex.Transports.Tests/FactoryBindingsTests.cs`, register
   `RawReliableNoAckLoggerBinding.CreateRegistration()` in the builder and add two formulas to
   the existing `CompileCases` source (no category; the `Case(...)` helper cannot carry one):
   `Case(TransportRole.Server, "log-noack(tcp-noack(\"127.0.0.1\", 9000))", TransportType.RawReliableNoAck)`
   and the `TransportRole.Client` counterpart.
6. Create the loose test files
   `Tests/Pontifex.Tests/RawReliableNoAckServerLoggerTests.cs` and
   `Tests/Pontifex.Tests/RawReliableNoAckClientLoggerTests.cs`:
   - server: a test-local `FakeNoAckServer : IRawReliableNoAckServer` (ten `ITransport` members +
     `Init`), a recording factory; assert `Init` forwards and stores the factory, `TryCreate`
     returns a handler that forwards all four callbacks and wraps the endpoint, and an exception
     from the user factory is logged and rethrown;
   - client: a test-local `FakeNoAckClient : IRawReliableNoAckClient`, a recording handler; assert
     `Init` forwards, all four callbacks reach the handler, and `OnConnected` receives a wrapper
     rather than the original endpoint.
   Use `VoidEndPoint.Instance` for the `IEndPoint` argument and `new Logger([])` for logging.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter FullyQualifiedName~RawReliableNoAckServerLogger`
  → all pass.
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter FullyQualifiedName~RawReliableNoAckClientLogger`
  → all pass.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --nologo`
  → all pass, including the two new compile cases (`FactoryBindingsTests` grows by 2).
- `dotnet test Pontifex.sln --no-build --nologo` → whole solution green. The expected increase
  over the 2230-test baseline is 6 (stage 4) + 7 (stage 5) + 2 (stage 9 compile cases) + the
  loose tests added in stages 2, 8 and 9; record the exact per-project counts in the completion
  report.

**Definition of done.** A `log-noack(...)` formula over a NoAck transport compiles in both roles;
the NoAck loggers forward and wrap exactly like the Ack loggers, proven by loose tests.

**Risks / notes.** `RawReliableNoAckServerLogger` creates one handler wrapper per admitted
session, unlike the Ack logger, because a NoAck factory is invoked once per connection. That is
deliberate for the new code and must not be back-ported to the Ack logger here. The reused
endpoint wrappers keep their `Ack` names and namespace by decision. The scheme is `log-noack`, not
`log`: see the registry assumption.

## Global definition of done

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` succeeds. The five library
  projects (`Pontifex`, `Pontifex.Factory`, `Pontifex.Api`, `Protocols/*`, `Transports/*`) report
  zero warnings. The solution may still report the 24 pre-existing warnings in
  `Terminal.UICommon`, `Pontifex.TestUI` and `Pontifex.Tests`; no warning introduced by this plan
  is acceptable.
- `dotnet test Pontifex.sln --no-build --nologo` passes every project. The only skipped tests are
  the pre-existing ones (67 in `Pontifex.Transports.Tests` at the start of this plan).
- The admission helpers, the connection/disconnection latches, the `GetSafe`/`Test` wrappers, the
  server socket listener and the server-handler logging wrapper each exist in exactly one place.
- Every `grep` check named in the stages returns the stated result.
- Every touched test suite satisfies `Tests/RULES.md`: derived layers keep only their own deltas,
  no test is duplicated from an ancestor, and each `Coverage.md` matches its layer's test file.
- Nine stage commits exist, one per stage, in plan order.

## Deferred follow-up

**Item 6 — deduplicate the reliable client transports.** Deliberately excluded from this plan; the
user asked to be reminded when this plan is implemented. Nothing in this plan blocks it.

Known shape and known problems (to be re-evaluated then):

- `RawReliableAckClientTransport` and `RawReliableNoAckClientTransport` differ by 9 lines out of
  ~90: `Type`, the constructor, and the return type of `CreateEndpoint`. Their `BeginConnect` and
  `DestroyTransport` bodies are identical.
- Originally proposed fix — `RawReliableClientTransport<TClientHandler, TEndpoint>` — adds a new
  public abstract type. `Tests/Pontifex.Tests/Transports/README.md` declares the inheritance tree
  and layer table as authoritative; ATS-01 requires one layer per abstract type, ATS-08 requires
  the gap to be recorded, and STRUCT-06 forbids an empty layer. That means a new layer directory,
  `Coverage.md`, test file, harness double, README tree/layer-table edits, and inheritance-parent
  updates for both protocol layers — for ~60 saved lines.
- It also changes the base class of two public types, which is binary-breaking for external
  subclasses, and the new type's only independent behaviour is the shared `BeginConnect` body, so
  the derived layers would have almost nothing left to own after ATS-09/ATS-10 are applied.
- Cheaper alternative to evaluate then: keep both types and move the two shared bodies into
  protected helpers on the existing `RawReliableClientTransport<TClientHandler>`
  (`ConnectWith(Func<TClientHandler, IRawReliableClientEndpointImp>)` and
  `StopAttachedEndpoint(StopReason)`), reducing each protocol override to one line. That adds
  protected surface to an existing layer (tests go in that layer) and adds no type.
- Independent of the shape: `RawReliableAckTcpClientTransport` redundantly re-declares
  `IRawReliableAckClient`, and `RawReliableClientTransport.Init` contains a commented-out
  `processedHandler.Test(...)` line. Both are cosmetic and out of scope here.
