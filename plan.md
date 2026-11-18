# Unify Reliable Ack/NoAck server plumbing (part 1)

## Goal

After this plan is complete, five pieces of duplicated behaviour between the reliable Ack and
NoAck implementations exist exactly once, in the shared reliable layer: the admission helpers of
the server transports, the connection/disconnection notification machinery of the endpoints, the
`GetSafe`/`Test` handler wrappers, the server socket listener, and the server-handler logging
wrapper. Ack and NoAck become thin protocol deltas over those shared bases, the NoAck protocol
gains logging wrappers and a `log(...)` factory binding, and every affected test suite and
coverage record reflects the new layer ownership. No public behaviour changes except the two
documented fixes and one documented API change.

## Context

The repository `Pontifex` is a generalized transport library. Reliable transports come in two
protocol flavours: Ack (with an acknowledgement handshake) and NoAck (no handshake). The two
flavours grew in parallel and still carry near-identical code:

- `RawReliableServerTransport<TServerHandler>` (`Pontifex/Implementations/Raw/Reliable/RawReliableServerTransport.cs`)
  owns admission gates, the session registry, and the ticker. Its protocol subclasses
  `RawReliableAckServerTransport` and `RawReliableNoAckServerTransport` each re-declare four
  byte-identical admission helpers (`TryBeginAdmission`, `CompleteAdmission`, two
  `AbandonAdmission` overloads).
- `RawReliableEndpoint` → `RawReliableClientEndpoint` / `RawReliableServerEndpoint` →
  Ack/NoAck endpoint pairs. The four leaf endpoints each re-implement the same
  connect-once / disconnect-once / `IsConnected` latch logic and the same
  `OnConnected` exception handling.
- `Pontifex.Raw.Reliable.Ack.IRawAckServerHandler_Ext` provides `GetSafe`/`Test` wrappers only
  for `IRawReliableAckServerHandler`.
- `RawReliableAckServerLogger` bundles transport + acknowledger + handler logging; NoAck has no
  logging wrapper and no `log` factory binding at all.
- `IServerSocketListener`/`AsyncServerSocketListener` live in `Pontifex.Raw.Reliable.Ack.Tcp`
  although the NoAck TCP server transport already uses them (an explicit "namespace debt" note in
  that file).

A previous, already-committed change (`796b5a1 Move OnConnected into IRawReliableServerHandler`)
made `void OnConnected(IRawReliableEndpoint)` a member of the shared `IRawReliableServerHandler`,
leaving `IRawReliableAckServerHandler` and `IRawReliableNoAckServerHandler` empty marker
interfaces. That is what makes the shared wrappers, logger and endpoint machinery possible now.

The scope is items 1, 3, 4, 5 and 7 of the unification inventory agreed with the user. Item 6
(client-transport dedup) is deliberately excluded; see [Deferred follow-up](#deferred-follow-up).

## Constraints

- Every `.csproj` keeps the settings required by `AGENTS.md`: `Nullable=enable`,
  `LangVersion=9` for `netstandard2.1` (all production projects here) and `14` for `net10.0`
  (test projects), `ImplicitUsings=disable` in production, `TreatWarningsAsErrors=true` in
  `Pontifex`.
- Production code must compile with C# 9. No file-scoped namespaces in `netstandard2.1`
  projects; the existing style uses block namespaces.
- XML documentation style follows the surrounding files: `<summary>` plus `<remarks>` with
  `<list type="bullet">` items for thread safety, pre/postconditions, and other notes. New
  production public/protected members carry documentation.
- No public type renames, no namespace moves of public types, and no new public abstract types.
  The only public API changes allowed are those named in the stages: new extension methods
  (stage 2), new protected members on existing public types (stages 3, 5), removal of
  `IRawReliableAckServerHandler` from `RawReliableAckServerLogger` is **not** allowed (stage 7
  keeps it), and new public logger/binding types in stage 8.
- `Tests/RULES.md` is normative for every test suite touched. In particular: one layer per
  abstract type (ATS-01), no duplicate tests between an ancestor and a derived layer
  (ATS-09/ATS-10, EVOL-11), `Coverage.md` updated in the same change as the tests
  (COVERAGE-11, EVOL-03), and no mocking library (HARNESS-09).
- Test fixtures keep the suite category (`[Category("Endpoints")]` or
  `[Category("Transports")]`); loose test files (stage 2) follow §9 of `Tests/RULES.md`.
- Builds must be run with `-p:NuGetAudit=false`. This machine has no NuGet network access and
  `NU1900` is promoted to an error in the library projects, which makes a plain `dotnet build`
  fail before compiling anything. This is an environment constraint, not a code issue.
- One git commit per stage, message `<imperative description> (stage N)`.

## Non-goals

- Item 6 of the earlier inventory: deduplicating `RawReliableAckClientTransport` and
  `RawReliableNoAckClientTransport`. Excluded by the user; see
  [Deferred follow-up](#deferred-follow-up).
- Deduplicating the TCP/UDP carrier endpoints (`RawReliable*TcpClientEndpoint`,
  `RawReliable*TcpServerEndpoint`) or the carrier server transports beyond the listener move.
- Changing the Ack handshake state machine, waiting-room logic, ack timeouts, or
  `RawReliableAckInfo`.
- Changing client-side `OnConnected` signatures (`IRawReliableAckClientHandler.OnConnected`
  keeps its `ackResponse` parameter).
- Renaming `RawAckServerSideEndpointWrapper` / `RawAckClientSideEndpointWrapper` or their
  namespace. The user rejected the rename; the NoAck loggers reuse them as-is.
- Reworking the Ack logger's single-instance handler state (a latent issue for concurrent Ack
  sessions). Stage 7 preserves today's behaviour exactly.
- Fixing the pre-existing unused variable/dead code noted in `RawReliableClientTransport.Init`
  (commented-out `processedHandler.Test(...)` line).

## Assumptions

- `IRawReliableAckServerHandler` and `IRawReliableNoAckServerHandler` are empty marker
  interfaces today. Evidence: `Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Server/IRawReliableAckServerHandler.cs`,
  `.../NoAck/Server/IRawReliableNoAckServerHandler.cs` at commit `796b5a1`.
- C# allows one class to implement two interfaces that share an inherited member set; with both
  markers empty, a single wrapper class can implement both. The repository already contains a
  class implementing several interfaces of the same hierarchy
  (`RawReliableAckServerLogger`, `RawReliableAckServerTransport`). No compiler restriction
  exists for this case.
- The factory registry keys registrations by `(scheme, TransportType)`, so a `log` registration
  for `TransportType.RawReliableNoAck` coexists with the existing `log` registration for
  `TransportType.RawReliableAck`. Evidence:
  `Pontifex.Factory/TransportConstructorRegistry.cs` (`Key` is scheme + type).
- Every reliable carrier endpoint calls `base.BeginDisconnect(...)` from its own
  `BeginDisconnect` override, so notification placed in an ancestor `BeginDisconnect` would still
  run. Evidence: `RawReliableAckDirectClientEndpoint.cs:112`,
  `RawReliableAckDirectServerEndpoint.cs:112`, `RawReliableAckTcpClientEndpoint.cs:227`,
  `RawReliableAckTcpServerEndpoint.cs:569`, `RawReliableNoAckTcpServerEndpoint.cs:293`. This plan
  does **not** rely on that; notification stays explicit per endpoint.
- `RawReliableEndpoint`, `RawReliableClientEndpoint`, `RawReliableServerEndpoint` and all four
  protocol endpoints override `GetIsConnected`, so changing the base predicate is unobservable
  until each endpoint is migrated.
- `Tests/Pontifex.Tests/Endpoints/README.md` and `.../Transports/README.md` layer tables are
  authoritative and already list every layer this plan touches; no layer is added or removed.
- The `Test(...)` extension is `#if DEBUG`-only. Test projects build Debug by default, so a
  loose test may exercise it; this plan nevertheless tests only `GetSafe` (see stage 2).

## Prerequisites

- Working tree clean and on a branch that contains `796b5a1`.
- `dotnet` SDK able to build `net10.0` and `netstandard2.1` (validated: the solution builds and
  the full test suite passes at the start of this plan).
- No NuGet network. Always pass `-p:NuGetAudit=false` to `dotnet build`/`dotnet test`.
- Plan file `plan.md` committed at the repository root (plan-executor Phase 2 requirement).

## Stages

### Stage 1 — Move the server socket listener to the shared socket namespace

**Objective.** `IServerSocketListener` and `AsyncServerSocketListener` live in a carrier-neutral
namespace used by both reliable server transports, and the "namespace debt" note is gone.

**Inputs.** Clean tree at `796b5a1`.

**Actions.**

1. Move `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/Server/IServerSocketListener.cs`
   to `Transports/Pontifex.Transport.Net/Common/ServerSocketListener/IServerSocketListener.cs`
   and change its namespace from `Pontifex.Raw.Reliable.Ack.Tcp` to `Pontifex.NetSockets.Tcp`.
   Keep it `internal`.
2. Move `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/Server/AsyncServerSocketListener.cs`
   to `Transports/Pontifex.Transport.Net/Common/ServerSocketListener/AsyncServerSocketListener.cs`,
   same namespace change, still `internal`. Keep the body byte-identical.
3. In `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/RawReliableAckTcpServerTransport.cs`
   and `.../NoAck/RawReliableNoAckTcpServerTransport.cs`, replace the `using
   Pontifex.Raw.Reliable.Ack.Tcp;` directive with `using Pontifex.NetSockets.Tcp;`. Remove any
   now-unused `using` directives; do not otherwise edit these files.
4. In `RawReliableNoAckTcpServerTransport.cs`, delete the `<item><description><b>Namespace
   debt</b>...` bullet from the class `<remarks>` (the debt no longer exists).
5. Delete the now-empty directory `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/Server/`
   if no other file remains in it. (`IServerSocketListener.cs` and
   `AsyncServerSocketListener.cs` are its only files.)

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.` with no
  warnings.
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj --no-build --nologo`
  → all tests pass (expected 306 at the start of this plan).
- `grep -rn "Raw.Reliable.Ack.Tcp" Transports/Pontifex.Transport.Net --include=*.cs` → no
  matches.

**Definition of done.** Both listener files exist under `Common/ServerSocketListener/` in
namespace `Pontifex.NetSockets.Tcp`; both server transports compile against them; the debt note
is gone; the transport integration tests pass.

**Risks / notes.** Both listener types are `internal`; moving them inside the same assembly
cannot break external consumers. The Ack namespace may still be referenced by other Ack files
(grep is part of verification, not a hint).

### Stage 2 — Share `GetSafe`/`Test` across both server handler contracts

**Objective.** One handler-wrapper implementation serves both marker interfaces; the Ack-only
extension class is gone; `GetSafe` also guards `OnConnected`; a loose test file covers the
wrappers.

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
     carrying the existing state machine (`Constructed → Connected → Disconnected`, `OnStopped`
     unguarded, `OnReceived` requires at least `Connected`), the existing `ToString()` override,
     and the `#if DEBUG`-guarded factory behaviour of today's `IRawAckServerHandler_Ext`.
   - Both `Test` overloads return `core` unchanged when `DEBUG` is not defined.
   - XML documentation on all four public methods (`<summary>`, `<param>`, `<returns>`).
2. Copy the bodies from `Pontifex/Implementations/Raw/Reliable/Ack/Ext/IRawAckServerHandler_Ext.cs`
   and delete that file. The only intentional behaviour change: `SafeServerHandler.OnConnected`
   now catches and reports exceptions through `_onException`, matching the other three callbacks.
3. Verify the four existing call sites compile unchanged:
   `Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckServer.cs:134` (`.Test(...).GetSafe(...)`),
   `Protocols/Pontifex.Protocol/Raw/Reliable/Ack/Server/AcknowledgerWrapper.cs:75`,
   `Pontifex/Converters/RawUnreliableNoAck/RawReliableAck/RawUnreliableNoAckToRawReliableAckServer.cs:143`,
   and the converter's server logic. Add a `using Pontifex.Raw.Reliable;` only where the enclosing
   namespace is not `Pontifex.Raw.Reliable.*`.
4. Create the loose test file `Tests/Pontifex.Tests/RawReliableServerHandlerExtTests.cs`
   (`namespace Pontifex.Tests;`, `[TestFixture]`, no suite category because it is a loose file).
   Cover, for both `GetSafe` overloads: each of the four callbacks is forwarded; an exception from
   each callback, including `OnConnected`, is reported to the callback and swallowed. Use a
   hand-written recording handler implementing the corresponding marker interface. Do not test
   `Test(...)` (DEBUG-only; state-machine invariants belong to the instrumentation, not to this
   contract change) — record that decision in a comment at the top of the fixture.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter FullyQualifiedName~RawReliableServerHandlerExt`
  → all tests pass.
- `grep -rn "IRawAckServerHandler_Ext" --include=*.cs .` → no matches.

**Definition of done.** The Ack-only extension file is deleted, the shared class provides both
overloads, all four production call sites compile, and the loose tests pass.

**Risks / notes.** The two overloads return the concrete marker type, so no call site needs a
cast. One instance implements both markers; it is never handed to a transport as both, so the
extra claim is unobservable. The `OnConnected` catch is a deliberate fix: previously a throwing
handler escaped the "safe" wrapper.

### Stage 3 — Hoist the admission helpers into `RawReliableServerTransport<TServerHandler>`

**Objective.** `TryBeginAdmission`, `CompleteAdmission`, and both `AbandonAdmission` overloads
exist once in the shared server transport and take `IRawReliableServerHandler?`; the protocol
transports no longer declare them.

**Inputs.** Stage 2 committed.

**Actions.**

1. In `Pontifex/Implementations/Raw/Reliable/RawReliableServerTransport.cs`, add (copying the
   XML documentation from the Ack transport and replacing "acknowledger"/"factory" with "session
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
   `.../NoAck/RawReliableNoAckServerTransport.cs`. Update the `<see cref="CompleteAdmission(...)"/>`
   and `<see cref="AbandonAdmission(...)"/>` crefs in the remaining members of both files so they
   name the new signatures (handler parameter `IRawReliableServerHandler?`).
3. Update the test doubles' exposed wrappers to the new parameter type:
   `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/Ack/Harness/TestRawReliableAckServerTransport.cs`
   and `.../NoAck/Harness/TestRawReliableNoAckServerTransport.cs`: `ExposedCompleteAdmission`
   and `ExposedAbandonAdmission` take `IRawReliableServerHandler? handler`. Do not rename the
   exposed members.
4. Do not change `TryCreateHandler` / `TryAcknowledge` or the `StopOnce*` wrappers.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter TestCategory=Transports`
  → all tests pass, including the three admission tests in each protocol layer.
- `grep -n "TryBeginAdmission\|CompleteAdmission\|AbandonAdmission" Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckServerTransport.cs Pontifex/Implementations/Raw/Reliable/NoAck/RawReliableNoAckServerTransport.cs`
  → no matches.

**Definition of done.** The shared base owns the admission helpers; both protocol transports
compile without them; the transports suite passes unchanged.

**Risks / notes.** This is a protected-surface change: any external subclass calling
`CompleteAdmission` with a concrete handler still compiles because the concrete handler converts
to `IRawReliableServerHandler`. The concrete transports' own protected members are unaffected.

### Stage 4 — Migrate admission tests to the `RawReliableServerTransport` layer

**Objective.** Admission behaviour is tested once, in the layer that now owns it; the Ack and
NoAck server transport layers keep only their protocol delta.

**Inputs.** Stage 3 committed; the admission helpers live in the shared base.

**Actions.**

1. In `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/RawReliableServerTransportTests.cs`
   add the region `#region Admission` with these tests, moved verbatim from the protocol layers
   but driven through the base `TestRawReliableServerTransport` double:
   - `AbandonAdmission_WithHandler_NotifiesOnStoppedOnce`
   - `CompleteAdmission_WhenSessionCannotStart_NotifiesOnStoppedOnce`
   - `CompleteAdmission_WhenTransportStopping_DoesNotPreemptStartedSession`
   - plus two new tests for behaviour that had no direct test at either protocol layer:
     `TryBeginAdmission_ReservesSessionAndHandshakeSlots` (both gates exhausted after the call)
     and `TryBeginAdmission_WhenHandshakeGateFull_ReleasesTheSessionSlot`.
   The base double already exposes `ExposedTryBeginAdmission`, `ExposedCompleteAdmission`, and
   `ExposedAbandonAdmission`; update those wrappers to `IRawReliableServerHandler?` if stage 3
   has not already (they already use `IRawReliableServerHandler`).
2. Delete the three moved tests from
   `.../Server/Ack/RawReliableAckServerTransportTests.cs` and the three from
   `.../Server/NoAck/RawReliableNoAckServerTransportTests.cs`, together with any now-unused
   private helpers and `using` directives in those files. The `#region Admission` block of each
   protocol file disappears.
3. Update `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/Coverage.md`: add the five rows to
   the covered-tests table, with the note that the shared gate ordering is asserted through
   `TestRawReliableServerTransport` and that `CompleteAdmission` may release the slot through the
   endpoint's terminal notification.
4. Update `.../Server/Ack/Coverage.md` and `.../Server/NoAck/Coverage.md`: remove the moved rows
   from the covered-tests tables; do not touch their planned/deferred sections.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter TestCategory=Transports`
  → all pass; the total count is unchanged except for the two added `TryBeginAdmission` tests.
- `grep -rn "Admission_" Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/Ack Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/NoAck --include=*.cs`
  → no matches.

**Definition of done.** No admission test remains in a protocol layer, the base layer covers the
moved and the two new cases, and all three `Coverage.md` files match.

**Risks / notes.** The moved tests use `TestRawReliableServerSession` and the base double's
`Start()` helper; if a protocol test depended on the concrete `Type` value, drop only that
assertion, not the behaviour. Keep test names unchanged so coverage rows stay traceable.

### Stage 5 — Add shared connection-notification machinery to the endpoint base

**Objective.** `RawReliableEndpoint` owns the handler reference, the connected/disconnected
latches, the notification helpers and the disconnect-aware `IsConnected` predicate;
`RawReliableServerEndpoint` exposes `NotifyConnected()`; `RawReliableClientEndpoint` exposes the
typed client handler. No endpoint behaviour changes yet: all four leaf endpoints still override
`GetIsConnected` and still run their own notification code.

**Inputs.** Stage 4 committed.

**Actions.**

1. In `Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableEndpoint.cs`:
   - add `private readonly IRawReliableHandler _handler;` assigned in the constructor;
   - add `protected IRawReliableHandler Handler => _handler;`
   - add `private int _connectedNotified;` and `private int _disconnectedNotified;`
   - `protected void MarkConnectedNotified()` → `Volatile.Write(ref _connectedNotified, 1);`
   - `protected bool NotifyConnectedCore(Action onConnected)`:
     marks the latch, then `try { onConnected(); return true; } catch (Exception ex) { Log.wtf(ex);
     Stop(new ExceptionFail(TransportName, ex, "OnConnected failed"), false); return false; }`.
   - `protected void NotifyDisconnected(StopReason reason)`:
     `if (Interlocked.Exchange(ref _disconnectedNotified, 1) == 0 && Volatile.Read(ref _connectedNotified) != 0) { try { _handler.OnDisconnected(reason); } catch (Exception ex) { Log.wtf(ex); } }`.
   - change `protected virtual bool GetIsConnected()` from `=> IsLowLevelConnected` to
     `=> IsLowLevelConnected && Volatile.Read(ref _disconnectedNotified) == 0`.
   - Add `using System.Threading;` if absent. Keep the existing `FailHard`, `BeginDisconnect`
     contract, and `_currentStage` logic untouched: the base does **not** auto-notify on these
     paths, so unconverted leaf endpoints cannot double-notify.
2. In `RawReliableServerEndpoint.cs`: add
   `private readonly IRawReliableServerHandler _handler;` (assigned in the constructor, which
   already receives it), `protected IRawReliableServerHandler ServerHandler => _handler;`, and
   `protected bool NotifyConnected()` → `NotifyConnectedCore(() => _handler.OnConnected(this))`.
3. In `RawReliableClientEndpoint.cs`: add the typed handler field and
   `protected IRawReliableClientHandler ClientHandler => _handler;` (constructor already receives
   it).
4. Update the two layer harnesses so the new surface is drivable:
   - `Tests/Pontifex.Tests/Endpoints/RawReliable/Harness/TestRawReliableEndpoint.cs`: expose
     `ExposedMarkConnectedNotified()`, `ExposedNotifyConnectedCore(Action)`,
     `ExposedNotifyDisconnected(StopReason)`; its `BeginDisconnect` override continues to record
     only (it must not call a base implementation, which does not exist).
   - `Tests/Pontifex.Tests/Endpoints/RawReliable/Server/Harness/TestRawReliableServerEndpoint.cs`:
     expose `ExposedNotifyConnected()`.
5. Add base-layer tests to `Tests/Pontifex.Tests/Endpoints/RawReliable/RawReliableEndpointTests.cs`:
   - `NotifyDisconnected_WhenConnected_NotifiesOnce` (second call does not notify);
   - `NotifyDisconnected_WhenNeverConnected_DoesNotNotify`;
   - `NotifyDisconnected_WhenHandlerThrows_Swallows` (endpoint keeps running, next callback still
     delivered or no exception escapes — assert only that no exception escapes and the handler was
     called once);
   - `IsConnected_AfterDisconnectNotification_IsFalse`;
   - `NotifyConnectedCore_WhenCallbackThrows_StopsWithExceptionFail`.
6. Add a server-layer test to
   `Tests/Pontifex.Tests/Endpoints/RawReliable/Server/RawReliableServerEndpointTests.cs`:
   `NotifyConnected_ForwardsToHandlerAndMarksConnected` and
   `NotifyConnected_WhenHandlerThrows_StopsWithExceptionFail`.
7. Update `Tests/Pontifex.Tests/Endpoints/RawReliable/Coverage.md` and
   `.../Server/Coverage.md` with the new rows.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter TestCategory=Endpoints`
  → all pass. Existing tests must not change behaviour: every leaf endpoint still overrides
  `GetIsConnected`. The base-layer test `FailHard_WhenConnected_StopsWithoutDisconnectedNotification`
  still passes because the base does not auto-notify.
- `git diff --stat` for this stage touches only the five files above plus two `Coverage.md`.

**Definition of done.** The shared helpers exist and are covered at their own layer; no leaf
endpoint uses them yet; the whole Endpoints suite is green.

**Risks / notes.** `RawEndpoint` keeps its own `private IRawHandler _handler`; the reliable layer
now holds a second reference to the same object. That is intentional and documented in the class
remarks ("the handler is retained, not disposed"). Do not remove `RawEndpoint`'s field: it drives
`OnReceived` and the terminal stop notification.

### Stage 6 — Adopt the shared machinery in the NoAck endpoints

**Objective.** `RawReliableNoAckClientEndpoint` and `RawReliableNoAckServerEndpoint` shrink to a
constructor plus a short `OnConnected` override; their duplicated latches, `GetIsConnected`,
`FailHard`, `BeginDisconnect` and private `NotifyDisconnected` are gone.

**Inputs.** Stage 5 committed.

**Actions.**

1. `RawReliableNoAckServerEndpoint.cs`:
   - delete `_handler`, `_connectedNotified`, `_disconnectedNotified`, `GetIsConnected`,
     `FailHard`, `BeginDisconnect`, and the private `NotifyDisconnected`;
   - `OnConnected()` becomes `if (!NotifyConnected()) { return; } base.OnConnected();`;
   - keep the constructor and pass the handler to the base only.
2. `RawReliableNoAckClientEndpoint.cs`: same deletions; `OnConnected()` becomes
   `if (!NotifyConnectedCore(() => ClientHandler.OnConnected(this))) { return; } base.OnConnected();`.
3. Keep both class `<remarks>` intact except for statements that are no longer true about
   overrides in this class.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter TestCategory=Endpoints`
  → all pass, in particular every NoAck server/client test, unchanged.
- `grep -n "GetIsConnected\|NotifiesDisconnected\|_disconnectedNotified" Pontifex/Implementations/Endpoints/Raw/Reliable/NoAck/*.cs`
  → no matches.

**Definition of done.** Both NoAck endpoints contain no notification machinery of their own and
their existing tests pass without edits.

**Risks / notes.** `FailHard` for the NoAck endpoints used to notify before
`RawReliableEndpoint.FailHard` moved the stage to `Disconnected`. The base helper is called from
`RawReliableEndpoint.FailHard` only if stage 5 wired it — it did not. Therefore
`RawReliableNoAck*Endpoint` must still override `FailHard` as
`NotifyDisconnected(reason); base.FailHard(reason);` (two lines). Same for `BeginDisconnect`:
`NotifyDisconnected(reason);`. This preserves the exact notification-before-stage-change ordering.
That is the intended end state: the duplicated bodies collapse, the two one-line overrides stay.

### Stage 7 — Adopt the shared machinery in the Ack endpoints

**Objective.** The Ack server and client endpoints use the shared latches and helpers; the
duplicated disconnect block that existed twice in each file exists once per endpoint and uses
`NotifyDisconnected`.

**Inputs.** Stage 6 committed.

**Actions.**

1. `RawReliableAckServerEndpoint.cs`:
   - delete `_handler`; use `ServerHandler` from the base;
   - `SendAckResponse`: before `ServerHandler.OnConnected(this)` call `MarkConnectedNotified()`;
     keep the local `try/catch` unchanged (it sets `_stage = State.LogicallyDisconnected` and
     stops with `new ExceptionFail(TransportName, ex)` — deliberately different from the shared
     helper);
   - `FailHard`: `ReleasePendingAckResponse(); _stage = State.LogicallyDisconnected;
     NotifyDisconnected(reason); base.FailHard(reason);`
   - `BeginDisconnect`: `ReleasePendingAckResponse(); _stage = State.LogicallyDisconnected;
     NotifyDisconnected(reason);`
   - keep `GetIsConnected` (`IsLowLevelConnected && _stage == State.LogicallyConnected`) and the
     `State` enum unchanged.
2. `RawReliableAckClientEndpoint.cs`:
   - delete `_handler`; use `ClientHandler` for `FillAckData` and the two-argument
     `OnConnected`;
   - `FinishAckConnect`, `case State.AckRequestSent`: keep `_stage = State.LogicallyConnected;`
     and `base.OnConnected();` (keep-alive ordering), then replace the manual `try/catch` with
     `if (!NotifyConnectedCore(() => ClientHandler.OnConnected(this, message))) { }` — the helper
     already marks the latch, logs, and stops with the same `"OnConnected failed"` reason the
     current catch uses;
   - `FailHard`: `_stage = State.LogicallyDisconnected; NotifyDisconnected(reason);
     base.FailHard(reason);`
   - `BeginDisconnect`: `_stage = State.LogicallyDisconnected; NotifyDisconnected(reason);`
   - keep `GetIsConnected` and the `State` enum unchanged.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter TestCategory=Endpoints`
  → all pass, including `FailHard_WhenHandshakePending_DoesNotNotifyOnDisconnected` and
  `Disconnect_WhenHandshakePending_DoesNotNotifyOnDisconnected` in both Ack layers (the shared
  latch is only armed by `MarkConnectedNotified`).
- `grep -c "OnDisconnected(reason)" Pontifex/Implementations/Endpoints/Raw/Reliable/Ack/Server/RawReliableAckServerEndpoint.cs`
  → `0`.

**Definition of done.** Both Ack endpoints notify only through the shared helper, their tests
pass unedited, and each file's disconnect logic appears once.

**Risks / notes.** Do not move `MarkConnectedNotified()` earlier than the point where the handler
is notified: `GetIsConnected` for Ack is driven by `_stage`, but the disconnect latch guard uses
the connect latch, so arming it before the ack response is admitted would let a later stop report
a disconnect for a session whose handler never saw `OnConnected`.

### Stage 8 — Migrate endpoint notification tests to their ancestor layers

**Objective.** Notification ordering and once-only behaviour is tested once, at the layer that
owns it; Ack and NoAck layers keep only protocol deltas; every `Coverage.md` matches.

**Inputs.** Stage 7 committed; all four endpoints use the shared machinery.

**Actions.**

1. Move to `Tests/Pontifex.Tests/Endpoints/RawReliable/RawReliableEndpointTests.cs` (adjust the
   driving double to `TestRawReliableEndpoint` and use `ExposedMarkConnectedNotified()` before the
   stop where the connected state matters):
   - from `Server/NoAck/RawReliableNoAckServerEndpointTests.cs`:
     `Stop_WhenConnected_NotifiesDisconnectedThenStopped`,
     `FailHard_WhenConnected_NotifiesDisconnectedThenStopped`,
     `FailHard_WhenConnecting_NotifiesStoppedOnly`,
     `Disconnect_WhenConnected_MakesIsConnectedFalseDuringOnDisconnected`;
   - from `Client/NoAck/RawReliableNoAckClientEndpointTests.cs`: the same four names;
   - from `Server/Ack/RawReliableAckServerEndpointTests.cs`:
     `Disconnect_WhenLogicallyConnected_NotifiesOnDisconnected`,
     `FailHard_WhenLogicallyConnected_NotifiesDisconnectedThenStopped`;
   - from `Client/Ack/RawReliableAckClientEndpointTests.cs`: the same two names.
2. Delete the moved tests from their source files. Also delete the now-duplicated
   `Stop_WhileConnecting_ReportsConnectionThenTearsDown` from both NoAck layers (the base layer
   already owns that transition).
3. Delete from `Server/NoAck/...Tests.cs` and `Client/NoAck/...Tests.cs` the tests that now only
   re-prove base behaviour: `OnConnected_WhenHandlerThrows_StopsWithExceptionFail`. Keep
   `Connect_NotifiesHandler` / `Connect_NotifiesHandlerAndSetsIsConnected` as the NoAck-specific
   proof that the notification happens without a handshake.
4. Keep in the Ack layers every test whose expectation depends on the handshake state:
   `..._WhenHandshakePending_DoesNotNotifyOnDisconnected` in both files, plus all connection,
   ack-response, and pending-response tests.
5. Update `Coverage.md` for the four affected layers (`RawReliable/Coverage.md`,
   `RawReliable/Server/Coverage.md`, `RawReliable/Server/Ack/Coverage.md`,
   `RawReliable/Server/NoAck/Coverage.md`, `RawReliable/Client/Ack/Coverage.md`,
   `RawReliable/Client/NoAck/Coverage.md` — six files): add rows for the moved tests to the target
   layer, remove them from the source layers, and keep the planned/deferred sections unchanged.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter TestCategory=Endpoints`
  → all pass; total Endpoints count decreases by exactly the number of deleted duplicate tests
  and increases by the number of moved tests, so the net count is unchanged.
- Cross-check the six `Coverage.md` files against `Tests/RULES.md` COVERAGE-11: every test name in
  each layer's test file appears in that layer's covered-tests table exactly once.

**Definition of done.** No test in an Ack/NoAck endpoint layer asserts behaviour that the base
layers already own; the six coverage files are consistent with the test files.

**Risks / notes.** Some moved tests assert `IsConnected` during `OnDisconnected`. In the base
layer that requires `ExposedMarkConnectedNotified()` before stopping; without it the base
`NotifyDisconnected` guard suppresses the callback. This is the one place where a mechanical move
needs a semantic adjustment — call it out in the moved test's `Coverage.md` note.

### Stage 9 — Extract the shared server-handler logging wrapper

**Objective.** The handler-logging behaviour (log line + endpoint wrapper for `Send`/`Disconnect`
+ forward to the user handler) exists once in an internal helper; `RawReliableAckServerLogger`
uses it and its public surface is unchanged.

**Inputs.** Stage 8 committed.

**Actions.**

1. Create `Pontifex/Wrappers/Logger/RawReliableServerHandlerLogger.cs`, namespace
   `Pontifex.Raw.Reliable.Logger`, `internal sealed class RawReliableServerHandlerLogger :
   IRawReliableAckServerHandler, IRawReliableNoAckServerHandler`:
   - constructor `(ILogger log, IRawReliableServerHandler? userHandler)` and a settable
     `UserHandler` property;
   - `OnConnected` logs `UserHandler.OnConnected()`, wraps the endpoint with
     `RawAckServerSideEndpointWrapper` (same four lambda arguments as today), and forwards;
   - `OnDisconnected`, `OnReceived`, `OnStopped` log and forward exactly as
     `RawReliableAckServerLogger` does today.
2. Rewrite `RawReliableAckServerLogger` to delegate to one instance of that helper:
   - keep the class signature and all interfaces exactly as today (`IRawReliableAckServer`,
     `IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>`,
     `IRawReliableAckServerHandler`);
   - replace `_userHandler` with `private readonly RawReliableServerHandlerLogger _handlerLogger;`
     constructed from `core.Log`;
   - `TryAck` sets `_handlerLogger.UserHandler = result.Value.handler;` and returns
     `(this, result.Value.ackResponse)` as today;
   - the four handler callbacks delegate to `_handlerLogger`.
3. Do not change `RawReliableAckClientLogger` or the endpoint wrapper classes.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.`
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --nologo`
  → all pass (the Ack `log` binding is compiled in `FactoryBindingsTests`).
- `grep -n "_userHandler" Pontifex/Wrappers/Logger/RawReliableAckServerLogger.cs` → no matches.

**Definition of done.** The Ack server logger keeps its public contract and gains no duplicated
logging bodies; the helper implements both marker interfaces.

**Risks / notes.** The helper is `internal`; `Pontifex.Tests` has `InternalsVisibleTo`, and no
external consumer can depend on it. The Ack logger still holds one handler at a time — that
pre-existing limitation is preserved, not fixed.

### Stage 10 — Add NoAck logging wrappers and the NoAck `log` binding

**Objective.** `log(...)` works for `RawReliableNoAck` in both roles, built on the shared handler
wrapper; the binding is registered in the TestUI factory and covered by a compile test.

**Inputs.** Stage 9 committed.

**Actions.**

1. Create `Pontifex/Wrappers/Logger/RawReliableNoAckServerLogger.cs`, namespace
   `Pontifex.Raw.Reliable.NoAck.Logger`, `public sealed class RawReliableNoAckServerLogger :
   IRawReliableNoAckServer, IRawReliableNoAckServerHandlerFactory<IRawReliableNoAckServerHandler>`:
   - mirror `RawReliableAckServerLogger`'s transport forwarding (`Name`, `IsValid`, `IsStarted`,
     `Start`, `Stop`, `Log`, `Memory`, `GetControls`, `MessageMaxByteSize`, `Type`);
   - `Init(factory)` stores the user factory and returns `_core.Init(this)`;
   - `TryCreate(remote)` logs the call, invokes the user factory, and on a non-null handler
     returns `new RawReliableServerHandlerLogger(_core.Log, handler)`; an exception is logged and
     rethrown so the transport keeps its own rejection semantics.
2. Create `Pontifex/Wrappers/Logger/RawReliableNoAckClientLogger.cs`, namespace
   `Pontifex.Raw.Reliable.NoAck.Logger`, `public sealed class RawReliableNoAckClientLogger :
   IRawReliableNoAckClient, IRawReliableNoAckClientHandler`: mirror `RawReliableAckClientLogger`
   minus `FillAckData`, with `OnConnected(IRawReliableEndpoint)` wrapping the endpoint through
   `RawAckClientSideEndpointWrapper` (the same two lambdas, no controls array).
3. Create `Pontifex/Wrappers/Logger/RawReliableNoAckLoggerBinding.cs`, namespace
   `Pontifex.Raw.Reliable.NoAck.Logger`, `public static class RawReliableNoAckLoggerBinding` with
   `public const string SchemeName = "log";` and a `CreateRegistration()` that mirrors
   `RawReliableAckLoggerBinding` with `TransportType.RawReliableNoAck` and
   `TransportTypeSet.Exactly(TransportType.RawReliableNoAck)`; `Create` returns the server logger
   for `TransportRole.Server` and the client logger otherwise.
4. Register the binding in `Pontifex.TestUI/TransportFactory.cs` next to
   `RawReliableAckLoggerBinding.CreateRegistration()`.
5. In `Tests/Pontifex.Transports.Tests/FactoryBindingsTests.cs`, register
   `RawReliableNoAckLoggerBinding.CreateRegistration()` in the builder and add two compile cases
   with `[Category("Integration")]`, matching the existing style:
   `Case(TransportRole.Server, "log(tcp-noack(\"127.0.0.1\", 9000))", TransportType.RawReliableNoAck)`
   and the `TransportRole.Client` counterpart.

**Verification.**

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` → `Build succeeded.`
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --nologo`
  → all pass, including the new compile cases.
- `dotnet test Pontifex.sln --no-build --nologo` → whole solution green.

**Definition of done.** A `log(...)` formula over a NoAck transport compiles in both roles; the
NoAck loggers log every callback and wrap the endpoint the same way the Ack loggers do.

**Risks / notes.** `RawReliableNoAckServerLogger` creates one handler wrapper per admitted
session, unlike the Ack logger, because a NoAck factory is invoked once per connection. That is a
deliberate improvement in the new code and must not be back-ported to the Ack logger in this plan.
The reused endpoint wrappers keep their `Ack` names and namespace by decision.

## Global definition of done

- `dotnet build Pontifex.sln -p:NuGetAudit=false -v q --nologo` succeeds with zero warnings.
- `dotnet test Pontifex.sln --no-build --nologo` passes every project; skipped tests are only the
  pre-existing ones.
- The admission helpers, the connection/disconnection latches, the `GetSafe`/`Test` wrappers, the
  server socket listener and the server-handler logging wrapper each exist in exactly one place.
- `grep` checks named in the stages return no matches.
- Every touched test suite satisfies `Tests/RULES.md`: no duplicated derived-layer tests, and each
  `Coverage.md` matches its layer's test file.
- Ten stage commits exist, one per stage, in plan order.

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
