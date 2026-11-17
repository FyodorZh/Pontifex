# Implement RawReliableNoAck over TCP and align OnStopped semantics

## Goal

Introduce `RawReliableNoAck` as a first-class reliable transport contract that is the sibling of
`RawReliableAck` but has no acknowledgement handshake, implement it over TCP with the canonical
scheme `tcp-noack`, and first correct the reliable `OnStopped` semantics globally so both Ack and
NoAck deliver the callback sequence documented on `IRawHandler`.

After this plan is complete:

- Reliable endpoints deliver `OnConnected -> OnDisconnected -> OnStopped` exactly once for a
  connection that connected, and exactly one `OnStopped` for a started attempt that never reached
  `OnConnected`.
- A server handler created by an acknowledger or handler factory receives exactly one `OnStopped`
  even when its session endpoint cannot be created, started, or admitted.
- A client transport whose `Start` returns `false` does not invoke the client handler's `OnStopped`.
- The legacy `RawReliableNoAck` transport-level event/send abstraction is gone and replaced by the
  endpoint-owned architecture used by `RawReliableAck`.
- `tcp-noack` constructs a working reliable NoAck server/client pair.
- Direct NoAck, NoAck conformance controls, and `Pontifex.Api` changes remain out of scope.

## Context

`RawReliableAck` is the only implemented reliable raw contract. Its implementation is layered:

- The core reliable machinery in `Pontifex/Implementations/Endpoints/Raw/Reliable/` and
  `Pontifex/Implementations/Raw/Reliable/` provides connect/disconnect lifecycle, framing,
  keep-alive, idle watchdog, send admission, session registry, admission gates, and the session
  ticker.
- The Ack layer in `Pontifex/Abstractions/Transports/Raw/Reliable/Ack/` and
  `Pontifex/Implementations/Endpoints/Raw/Reliable/Ack/` adds only the acknowledgement handshake:
  client ack data, server ack response, pending ack data, ack size limits, and the acknowledgement
  stage machine.
- TCP carriers in `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/` own the socket,
  framing, receiver, sender, connect/teardown, and the server-side accept handshake.

The existing `RawReliableNoAck` abstraction is dead legacy code. It uses transport-level
`OnReceived` events and `Send` methods, plus `IRawReliableNoAckClientSession`, and has no
carrier or consumer. It must be replaced, not extended.

The current reliable `OnStopped` behavior does not match the documented `IRawHandler` contract on
edge paths:

- `RawEndpoint.FinishStop` always calls `OnStopped`, but `RawEndpoint.FailHard` only calls it when
  the raw stage is `Started`, and `RawEndpoint.Stop` while `Constructed` never calls it.
- `RawReliableServerTransport` has no handler fallback when a session endpoint fails to start or is
  stopped before `Started`.
- `RawReliableAckServerTransport` has no way to notify a handler returned by an acknowledger when
  endpoint creation or admission subsequently fails.
- The Ack specification says server sessions do not receive `OnStopped`; the implementation and
  `IRawHandler` documentation say they do.

The agreed normative rule is:

| Situation | Handler callbacks |
|---|---|
| Transport `Start` returns `false` | none |
| `Start` returns `true`, connection fails before `OnConnected` | exactly one `OnStopped` |
| `OnConnected` delivered | `OnConnected`, then `OnDisconnected`, then `OnStopped`, each once |
| Server handler created, but session endpoint creation/start/admission fails | exactly one `OnStopped` |

## Constraints

- Follow `AGENTS.md`: every project keeps its existing `TargetFramework`, `LangVersion`,
  `ImplicitUsings`, `Nullable`, and `TreatWarningsAsErrors` settings. Do not add a `latest` value.
- Follow `Tests/RULES.md`. New test layers require the suite `README.md` layer table update and a
  `Coverage.md` in the same change. No mocking library; hand-written doubles only.
- Library projects (`netstandard2.1`) have `TreatWarningsAsErrors=true`; new production code must
  compile cleanly with nullable enabled and C# 9.
- Use the existing endpoint-owned architecture. Do not add transport-level receive events or
  transport-level `Send` to NoAck.
- Keep `TransportType.RawReliableNoAck = 1`. Do not renumber the enum.
- `tcp-noack` is a canonical scheme only. Do not register `tcp` as an alias for NoAck.
- Do not implement Direct NoAck; `direct-noack` is reserved but must not be registered.
- Do not add NoAck conformance controls, endpoint controls, or a conformance suite.
- Do not change `Pontifex.Api`, `Pontifex.TestUI`, `Pontifex.Protocol.*`, logger wrappers, or
  Zip/Reconnectable wrappers.
- Do not implement T2a/T2b TCP endpoint deduplication. T3 intentionally writes a fresh NoAck TCP
  endpoint; record the future consolidation as deferred.
- Do not add ack frame types 5 or 6 to NoAck traffic. The shared `MessageType` enum is unchanged.
- All plan and code text is in English.

## Non-goals

- Direct NoAck transport.
- NoAck conformance controls or conformance suite.
- `Pontifex.Api` support for NoAck.
- TestUI controls or views for NoAck.
- Wrappers (`Logger`, `Zip`, `Reconnectable`) for NoAck.
- Renaming or renumbering `TransportType` values.
- A shared TCP client/server endpoint base or composition helper (future T2a/T2b work).
- Fixing the transport-level `Start(onStopped)` callback behavior on a failed start; only the
  handler callback rule is in scope.

## Assumptions

- The existing Ack TCP endpoint implementations are the reference for socket connect, send,
  receive, compositor, source draining, half-close policy, graceful teardown, and connect
  cancellation. Evidence: `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/`.
- `RawReliableClientEndpoint` and `RawReliableServerEndpoint` already own keep-alive and watchdog
  behavior and only omit the logical connect/disconnect handler notifications. Evidence:
  `Pontifex/Implementations/Endpoints/Raw/Reliable/`.
- `RawEndpoint` owns the serial action queue and terminal handler stop notification. Evidence:
  `Pontifex/Implementations/Endpoints/Raw/RawEndpoint.cs`, `FinishStop` and `FailHard`.
- `Pontifex.Tests` and `Pontifex.Transport.Net.Tests` have `InternalsVisibleTo` access to the
  production assemblies they test. Evidence: `Pontifex/Pontifex.csproj` and
  `Transports/Pontifex.Transport.Net/Pontifex.Transport.Net.csproj`.
- The test harnesses `TestRawEndpoint`, `TestRawReliableClientEndpoint`,
  `TestRawReliableServerSession`, `TestRawReliableAckServerTransport`,
  `RecordingRawReliableAckServerHandler`, and the Ack TCP integration tests can be extended or
  mirrored. Evidence: `Tests/Pontifex.Tests/...` and `Tests/Pontifex.Transport.Net.Tests/...`.
- The Ack TCP integration tests use real loopback sockets and are categorised `Integration`. The
  Ack Direct tests are in-process `Channel` tests and are not categorised. Real loopback sockets are
  acceptable in the TCP carrier stages; direct tests must not be treated as socket tests.
- A fresh NoAck TCP endpoint can be written by copying the Ack TCP endpoint and deleting the
  handshake; this is the agreed T3 strategy.

## Prerequisites

- .NET SDK able to build the solution (`netstandard2.1` libraries and `net10.0` tests).
- A clean working tree except for this plan file.
- No running tests or file locks on the build outputs.
- The repository root is `/Users/fyodor/Development/Home/Pontifex`.

## Stages

### Stage 1 — Enforce once-only handler `OnStopped` in `RawEndpoint` and the reliable client transport

**Objective**

Terminal endpoint paths that occur after a start attempt deliver `handler.OnStopped` exactly once,
`Stop` while `Constructed` does not notify the handler, and a client transport whose `Start`
returns `false` does not notify the client handler.

**Inputs**

- Current `RawEndpoint`, `RawReliableEndpoint`, `RawReliableClientTransport`, and their tests and
  suite documentation.

**Actions**

1. Modify `Pontifex/Implementations/Endpoints/Raw/RawEndpoint.cs`:
   - Add `private int _handlerStopped;`.
   - Add `private void NotifyHandlerStoppedOnce(StopReason reason)` that uses
     `Interlocked.Exchange(ref _handlerStopped, 1)`, invokes `_handler.OnStopped(reason)` only when
     it wins, and catches/logs handler exceptions.
   - In `FinishStop`, make `if (_currentStage == State.Stopped) return;` the first statement
     **inside** the queued action, before the `Stopping` assertion. Keep the invalid-stage throw for
     any other non-`Stopping` stage. Replace the direct `_handler.OnStopped(_stopReason!)` call with
     `NotifyHandlerStoppedOnce(_stopReason!)`.
   - In `FailHard`, replace the `_currentStage == State.Started` condition with a condition that
     notifies when the stage is `Starting`, `Started`, or `Stopping`. Leave `Constructed` and
     `Stopped` silent. Use `NotifyHandlerStoppedOnce(reason)`, then set the stage to `Stopped` and
     call `RaiseStopped` in the existing `finally`.
   - Leave the `Stop` action’s `Constructed` branch unchanged: set `Stopped`, call `RaiseStopped`,
     do not notify the handler.
   - Update the XML documentation on `RawEndpoint.FailHard`, `FinishStop`, and `Stop` to state the
     once-only guarantee generically: a start-attempted endpoint notifies the handler stop callback
     at most once, and an unreliable subclass may gate that notification through its handler gate.
     Do not put the reliable-only rule table on `RawEndpoint`, because `RawUnreliableEndpoint`
     deliberately suppresses `OnStopped` until `OnStarted` completes.
   - Add the reliable rule table to
     `Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableEndpoint.cs` XML documentation,
     where the reliable lifecycle is defined.
   - Update the stale `FailHard` XML documentation in `RawReliableEndpoint` and
     `RawUnreliableEndpoint`: both currently say the base notifies “when the endpoint had started”,
     which is no longer accurate now that `Starting` and `Stopping` also notify. State the
     generalized rule and leave the unreliable `HandlerGate` suppression note intact.
2. Modify `Pontifex/Abstractions/Transports/Raw/IRawHandler.cs`:
   - Add one sentence to the `OnStopped` documentation: a transport `Start` that returns `false`
     does not invoke `OnStopped`.
3. Modify `Pontifex/Implementations/Raw/Reliable/RawReliableClientTransport.cs`:
   - Add `private int _startFailed;`.
   - In `TryStart`, immediately before the `Fail("TryStart", ...)` call on the `BeginConnect`
     false path, set `Volatile.Write(ref _startFailed, 1)`. Do not clear it in the `finally` that
     clears `_startInProgress`; `TryStart` clears `_startInProgress` before `Fail` runs, so that
     flag alone cannot identify this path.
   - In `OnStopped`, the `State.Initialized` branch must still call `DestroyTransport(reason)` and
     detach the endpoint, but must call `_handler.OnStopped(reason)` only when both
     `_startInProgress` is false and `_startFailed` is zero.
   - After the `State.Initialized` branch consumes the flag, reset `_startFailed` to zero in a
     `finally` so a throwing `DestroyTransport` cannot leave it latched. Keep the
     `State.Connecting` branch unchanged.
   - Update the class and `DestroyTransport` XML documentation: a failed start destroys carrier
     resources without invoking the client handler’s `OnStopped`; the current sentence “for the
     initialized state this method runs before the handler stop notification” is no longer true.
4. Add tests to `Tests/Pontifex.Tests/Endpoints/RawEndpointTests.cs` using `TestRawEndpoint`:
   - `FailHard_WhileStarting_NotifiesStoppedOnce`: `Start()`, `DrainOne()` to enter `Starting`,
     then `ExposedFailHard(reason)`; assert `StoppedCount == 1` and `StoppedReason` is the reason.
   - `FailHard_WhileConstructed_DoesNotNotifyHandler`: call `ExposedFailHard(reason)` before
     `Start`; assert `StoppedCount == 0`.
   - `FailHard_AfterFinishStop_DoesNotNotifyAgain`: complete start, call
     `StopAndComplete(reason, tryBeGentle: false)`, then `ExposedFailHard`; assert
     `StoppedCount == 1` and the original stop reason is retained. Do not call `CompleteStop`
     directly from the `Started` stage; `FinishStop` requires the `Stopping` stage.
   - `FailHard_WhileStopping_NotifiesStoppedOnce`: complete start, call `Stop(reason, false)` and
     `DrainAll` so the endpoint enters `Stopping` but its `BeginStop` has not completed, then call
     `ExposedFailHard`; assert `StoppedCount == 1` and the fail reason is delivered.
   - Do not add a new `Stop_WhileConstructed_DoesNotNotifyHandler` test: the existing
     `Stop_BeforeStart_TransitionsWithoutHandlerCallback` already covers stop before start with
     `StoppedCount == 0`. Extend that test only if needed and note it in `Coverage.md`.
   - `FailHard_WhileStopped_DoesNotNotifyAgain`: stop while constructed using the existing test’s
     setup, then `ExposedFailHard`; assert `StoppedCount == 0`.
5. Update `Tests/Pontifex.Tests/Transports/Raw/Reliable/Client/RawReliableClientTransportTests.cs`
   for the Start-false rule:
   - `Start_WhenBeginConnectReturnsFalse_DestroysAndNotifiesStopped` becomes
     `Start_WhenBeginConnectReturnsFalse_DestroysWithoutNotifyingHandler`; assert
     `handler.StoppedCount == 0`, `handler.StoppedReason == null`, `DestroyTransportCalls == 1`,
     `callbackReason` is a `TextFail` (the `TryStart` failure reason), and the event log is
     `["beginConnect", "destroyTransport", "startCallback"]`.
   - `Start_WhenBeginConnectAttachesEndpointAndReturnsFalse_NotifiesHandlerAndDetaches` becomes
     `Start_WhenBeginConnectAttachesEndpointAndReturnsFalse_DetachesWithoutNotifyingHandler`;
     assert `handler.StoppedCount == 0`, `handler.StoppedReason == null`, the endpoint is detached,
     and the event log is `["beginConnect", "destroyTransport", "startCallback"]`.
   - `Start_WhenBeginConnectReturnsFalseAndOnStoppedThrows_StillDestroys` becomes
     `Start_WhenBeginConnectReturnsFalse_StillDestroys`; remove the throwing-handler premise,
     assert `handler.StoppedCount == 0`, `handler.ThrownStoppedException == null`, and the event log
     omits `handlerStopped`.
   - `Start_WhenBeginConnectReturnsFalseAndDestroyThrows_StillDetaches`: remove handler-stop
     assertions; keep destroy-throw and detach assertions, and assert `handler.StoppedCount == 0`.
   - `IsConnected_IsFalseDuringOnStopped_WhenStartFails` becomes
     `IsConnected_IsFalseAfterFailedStart_WithoutHandlerStopped`; assert
     `handler.StoppedCount == 0` and `transport.IsConnected == false`.
   - `Start_WhenBeginConnectStopsTransport_ReturnsFalseAndDestroys`: assert
     `handler.StoppedCount == 0`, `handler.StoppedReason == null`, `callbackReason Is.SameAs(reason)`,
     and the event log is `["beginConnect", "destroyTransport", "startCallback"]`.
   - Search the file for every remaining `StoppedCount == 1`, `StoppedReason`, `ThrownStoppedException`,
     or `handlerStopped` expectation on a Start-false path and update it accordingly; the
     transport-level `callbackReason` remains the `TryStart` or `Stop` reason, not a handler reason.
6. Update `Tests/Pontifex.Tests/Endpoints/Coverage.md` and
   `Tests/Pontifex.Tests/Transports/Raw/Reliable/Client/Coverage.md` with the new and renamed
   tests and the explicit Start-false rule.
7. Update `Tests/Pontifex.Tests/Transports/README.md`:
   - Replace the naming example
     `Start_WhenBeginConnectReturnsFalse_DestroysAndNotifiesStopped` with the renamed
     `Start_WhenBeginConnectReturnsFalse_DestroysWithoutNotifyingHandler`.
   - Rewrite the `Start / connect` bullet: a `false` from `BeginConnect` fails and destroys, and
     does **not** notify the handler `OnStopped`.
   - Rewrite the `Stop notification matrix` sentence for the `Initialized` state: the order is
     `DestroyTransport`, detach, and no handler `OnStopped` on a failed start. Keep the
     `Connecting` ordering unchanged.
   - Rewrite the `RawReliableAckClientTransport` contract matrix phrase “failed-start
     notifications are covered by the `RawReliableClientTransport` layer” to “failed-start
     non-notification is covered by the `RawReliableClientTransport` layer”.
   - Rewrite the `Connection ownership` bullet: a failed `BeginConnect` destroys, detaches, and
     does not notify the handler, even when it already attached an endpoint; the transport is not
     left `Initialized`.
   - Update `Tests/Pontifex.Tests/Transports/Raw/Reliable/Client/Ack/Coverage.md`: change
     “failed-start notifications … are not repeated here” to “failed-start non-notification …”.
   - Update `Tests/Pontifex.Tests/Endpoints/Coverage.md`: change the note that `FailHard` notifies
     the handler “only from the started stage” to “from the starting, started, or stopping stage”.

**Verification**

Run:

```bash
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj
```

Expected: the full `Pontifex.Tests` project passes with zero failures. This includes the Endpoints
and Transports suites, so the `RawEndpoint`/`FailHard` change is exercised against reliable, Ack,
and unreliable endpoint layers and the server transport layer, not only the two focused fixtures.

**Definition of done**

The selected tests pass, the new tests prove once-only `OnStopped` on all terminal paths, and the
Start-false client paths no longer notify the client handler.

**Risks / notes**

- `FailHard` and `FinishStop` must agree on a single notification; the `_handlerStopped` flag is the
  authority.
- A `FinishStop` action that arrives after a hard failure must be a no-op, not an invalid-stage
  exception.
- Updating the Start-false tests changes existing asserted behavior; do not keep stale comments
  that claim the handler is notified.

---

### Stage 2 — Add the Ack server `StopOnce` gate and admission-failure API

**Objective**

The Ack server transport wraps every handler returned by its acknowledger in a stop-once gate and
offers admission-failure overloads that notify that handler exactly once.

**Inputs**

- Stage 1.
- Existing `RawReliableAckServerTransport` and its tests.

**Actions**

1. Add `Pontifex/Implementations/Raw/Reliable/StopOnceNotifier.cs`:
   - `internal sealed class StopOnceNotifier` with `private int _stopped` and
     `public bool TryNotify()` implemented with `Interlocked.Exchange`.
2. Add `Pontifex/Implementations/Raw/Reliable/Ack/StopOnceAckServerHandler.cs`:
   - `internal sealed class StopOnceAckServerHandler : IRawReliableAckServerHandler`.
   - Constructor takes `IRawReliableAckServerHandler inner`; store it in `public IRawReliableAckServerHandler Inner { get; }`.
   - `FillAckResponse`, `OnConnected`, `OnReceived`, `OnDisconnected` forward to `Inner`.
   - `OnStopped` calls `Inner.OnStopped(reason)` only when the shared
     `StopOnceNotifier.TryNotify()` wins. Do not add a separate `NotifyStopped` method; the
     carrier fallback calls `handler?.OnStopped(reason)` through the interface, and the gate
     deduplicates.
3. Modify `Pontifex/Implementations/Raw/Reliable/RawReliableServerTransport.cs` only:
   - Add `protected bool TryStartAndRegisterSession(IRawReliableServerEndpointImp session,
     out bool sessionStarted)`; keep the existing one-argument overload delegating with `out _`.
     Set `sessionStarted` to the result of `session.Start()` (including the `TryStartThenStop`
     path in the not-alive branch). Change `TryStartThenStop` to return whether `session.Start()`
     succeeded so that branch can report it. A started session owns its own terminal `OnStopped`;
     the admission fallback must not preempt it.
   - Do not redeclare this overload in `RawReliableAckServerTransport`; it would hide the base
     member and fail CS0108 with warnings-as-errors.
4. Modify `Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckServerTransport.cs`:
   - In `TryAcknowledge`, wrap the acknowledger result:
     `handler == null ? null : new StopOnceAckServerHandler(handler)`.
   - Keep `protected bool CompleteAdmission(IRawReliableServerEndpointImp session)` and make it
     delegate to the new overload with `handler = null` and
     `new TextFail(Name, "Failed to start or register a session")`.
   - Add `protected bool CompleteAdmission(IRawReliableServerEndpointImp session,
     IRawReliableAckServerHandler? handler, StopReason failureReason)`: call `EndHandshake()`,
     call `TryStartAndRegisterSession(session, out bool sessionStarted)`, and call
     `handler?.OnStopped(failureReason)` inside `try`/`catch` with `Log.wtf` only when the
     session did **not** start (`sessionStarted == false`). A session that started but failed to
     register is stopped by `TryStartAndRegisterSession` and owns its own `OnStopped`; the gate
     deduplicates. Return the registration result.
   - Keep `protected void AbandonAdmission()` and make it delegate to the new overload with
     `handler = null` and `new TextFail(Name, "Admission abandoned")`.
   - Add `protected void AbandonAdmission(IRawReliableAckServerHandler? handler, StopReason reason)`
     that calls `EndHandshake()`, `ReleaseSession()`, then `handler?.OnStopped(reason)` inside
     `try`/`catch` with `Log.wtf`.
   - The parameterless `CompleteAdmission(session)` is a temporary compatibility wrapper so Stage 2
     builds before the carriers are updated. The parameterless `AbandonAdmission()` remains in the
     final API because carriers still have no-handler failure paths. Stage 3 deletes only the
     parameterless `CompleteAdmission(session)` after updating both Ack carriers.
   - Update XML documentation for the new overloads and the gate.
5. Modify `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/Ack/Harness/TestRawReliableAckServerTransport.cs`:
   - Add `public bool ExposedTryBeginAdmission() => TryBeginAdmission();`.
   - Add `public bool ExposedCompleteAdmission(IRawReliableServerEndpointImp session,
     IRawReliableAckServerHandler? handler, StopReason failureReason)`.
   - Add `public void ExposedAbandonAdmission(IRawReliableAckServerHandler? handler, StopReason reason)`.
6. Update `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/Ack/RawReliableAckServerTransportTests.cs`:
   - Change both handler-identity assertions — in
     `Init_FirstCallSucceedsSecondFailsAndKeepsFirstAcknowledger` and in
     `TryAcknowledge_WhenStarted_PassesSameDataWithExtraReferenceAndReturnsHandler` — to cast the
     returned handler to `StopOnceAckServerHandler` and assert `Inner` is the expected handler.
   - Add tests:
     - `TryAcknowledge_ReturnsStopOnceWrapper`.
     - `StopOnceWrapper_ForwardsLifecycleCallbacks`: call `OnConnected`, `OnReceived`,
       `OnDisconnected`, `FillAckResponse`; assert the recording handler observed each.
     - `StopOnceWrapper_ForwardsOnStoppedOnce`: call the gate’s `OnStopped` twice through the
       interface; assert `StoppedCount == 1`.
     - `AbandonAdmission_WithHandler_NotifiesOnStoppedOnce`: call `ExposedTryBeginAdmission` once;
       obtain the gate with `ExposedTryAcknowledge(Remote, ackData)` and use that exact gate
       instance for `ExposedAbandonAdmission` and the subsequent direct `handler.OnStopped`
       call; assert `StoppedCount == 1`. Do not pass the raw recording handler or call
       `ExposedAbandonAdmission` twice.
     - `CompleteAdmission_WhenTransportStopping_DoesNotPreemptStartedSession`: reserve admission
       with `ExposedTryBeginAdmission`, stop the transport, then complete admission with a
       `TestRawReliableServerSession` whose start succeeds (the harness completes `BeginConnect`)
       and whose queue is not drained yet. Because `sessionStarted == true`, the admission fallback
       must not fire: assert `StoppedCount == 0` immediately after `CompleteAdmission` returns,
       then drain the session and assert `StoppedCount == 1` and `ActiveSessions == 0`. Do not
       assert `connected`/`disconnected` here; the base server session double does not raise those
       handler callbacks.
     - `CompleteAdmission_WhenSessionCannotStart_NotifiesOnStoppedOnce`: a session whose `Start`
       schedule is rejected reports `sessionStarted == false`; assert the fallback delivers one
       `OnStopped`. Do not add a second rejected-start test under a different name.
7. Update `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/Ack/Coverage.md`:
   - Add the new tests.
   - Change the `TryAcknowledge_WhenStarted_PassesSameDataWithExtraReferenceAndReturnsHandler`
     row from “the handler is returned unchanged” to “a stop-once gate wrapping the handler is
     returned; the gate forwards the callbacks and guarantees one `OnStopped`”.
8. Update `Tests/Pontifex.Tests/Transports/README.md`:
   - Change the `TryAcknowledge hand-off` description from “returns its handler unchanged” to
     “returns a stop-once gate wrapping the acknowledger’s handler; the gate forwards the handler
     callbacks and guarantees one `OnStopped`”.
   - Keep the ownership and reference-count description unchanged.

**Verification**

Run:

```bash
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj \
  --filter "FullyQualifiedName~RawReliableAckServerTransportTests"
```

Expected: all selected tests pass.

**Definition of done**

The Ack server transport returns the gate wrapper, the new admission overloads notify exactly
once, and all Ack server transport tests pass.

**Risks / notes**

- The wrapper changes handler identity; any test that compares the acknowledger-returned handler by
  reference must compare `StopOnceAckServerHandler.Inner`.
- The parameterless `CompleteAdmission(session)` exists only until Stage 3 updates the carriers;
  Stage 3 deletes it. The parameterless `AbandonAdmission()` remains for no-handler failure paths.

---

### Stage 3 — Ack carriers notify the session handler on admission failure

**Objective**

The Direct and TCP Ack server carriers pass the gated handler through endpoint-construction and
admission-failure paths so a created handler receives exactly one `OnStopped`. The shared
admission-failure notification itself is proved by the Stage 2 base-layer tests; this stage proves
the TCP consumer failure path deterministically, inspects the TCP producer hand-off, and proves the
Direct normal-stop sequence. The Direct endpoint-constructor failure path has no injectable factory
and the TCP producer hand-off has no injectable seam, so both are covered by inspection plus the
shared base test, not by fault injection.

**Inputs**

- Stage 2.
- Existing Ack Direct and TCP carriers and integration tests.

**Actions**

1. Modify `Transports/Pontifex.Transport.Direct/Raw/Reliable/Ack/RawReliableAckDirectServerTransport.cs`:
   - In `OnHandshakeMessage`, after `TryAcknowledge` returns a non-null `handler`:
     - On endpoint constructor failure, replace the existing `RejectHandshake(entry)` call with
       `AbandonAdmission(handler, new ExceptionFail(Name, ex, "Failed to create the server endpoint"));`
       followed by `RejectChannel(entry.Channel);`. Do not call both `AbandonAdmission(handler, ...)`
       and `RejectHandshake(entry)`, because the latter performs its own parameterless abandon and
       would release the admission twice.
     - Replace `CompleteAdmission(endpoint)` with
       `CompleteAdmission(endpoint, handler, new TextFail(Name, "Failed to start or register a direct session"))`.
   - Keep `AbandonAdmission()` for paths where no handler exists.
2. Modify `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/RawReliableAckTcpServerEndpoint.cs`:
   - Add `public IRawReliableAckServerHandler? Handler { get; }` to `AckAcceptResult`.
   - Update the internal factory methods and constructors so `Accepted` carries the handler and
     `Faulted` can carry an optional handler. `Rejected` carries `null`.
   - In `AcceptHandshake.CompleteBody`, pass the created `handler` into
     `AckAcceptResult.Accepted(endpoint, handler)`. When the endpoint constructor throws, pass the
     handler into `AckAcceptResult.Faulted(_transportName, ex, handler)`.
3. Modify `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/RawReliableAckTcpServerTransport.cs`:
   - In `OnHandshakeCompleted`, for `Accepted`, call
     `CompleteAdmission(result.Endpoint!, result.Handler, new TextFail(Name, "Failed to start or register a TCP session"))`.
   - For non-accepted results, call
     `AbandonAdmission(result.Handler, result.Reason ?? new TextFail(Name, "The acceptance handshake failed"))`
     instead of the parameterless abandon.
   - Change `OnHandshakeCompleted` from `private` to `internal`. A `protected` member cannot expose
     the internal `AckAcceptResult` type on the public transport (CS0051), so friend-assembly
     access is the only seam that does not publicize the accept result. The test double uses
     `InternalsVisibleTo`; this is a deliberate white-box carrier test, not reflection.
     `CloseHandshakeSockets` and `StopCarrier` remain best-effort socket cleanup; a handler may
     already exist by the time the completion path runs, so the single `OnStopped` guarantee comes
     from the shared gate, not from the absence of a handler.
4. Update the Ack TCP integration tests in
   `Tests/Pontifex.Transport.Net.Tests/Raw/Reliable/Ack/RawReliableAckTcpServerEndpointIntegrationTests.cs`:
   - The private nested `RecordingAckServerHandler` must record `StoppedCount`, `DisconnectedCount`,
     the stop reason, and an ordered event list (`"connected"`, `"disconnected"`, `"stopped"`) in
     addition to setting its events.
   - Any test that constructs or inspects `AckAcceptResult` must still compile; the new `Handler`
     member is additive.
   - Add `ServerHandler_ReceivesOnDisconnectedThenOnStopped_OnNormalStop` and assert the ordered
     event list is `["connected", "disconnected", "stopped"]`.
5. Add `Tests/Pontifex.Transport.Net.Tests/Raw/Reliable/Ack/RawReliableAckTcpServerTransportTests.cs`
   with `[Category("Integration")]`:
   - Define a file-local `TransportRecordingAckServerHandler` with `StoppedCount`,
     `DisconnectedCount`, a stop reason, and an ordered event list; the private nested handler in
     the integration test file is not accessible here.
   - Add an internal test double deriving `RawReliableAckTcpServerTransport` that exposes
     `TryBeginAdmission`, `TryAcknowledge`, and a public wrapper for `OnHandshakeCompleted`.
   - `FaultedHandshakeResult_NotifiesProvidedHandlerOnce`: construct the transport with a loopback
     listener on port 0, `Init` an acknowledger that returns the file-local recording handler,
     `Start`, reserve admission with `ExposedTryBeginAdmission`, obtain the gated handler with
     `ExposedTryAcknowledge`, build
     `AckAcceptResult.Faulted("test", new InvalidOperationException("endpoint ctor failed"), gatedHandler)`,
     call `OnHandshakeCompleted` with a fresh unconnected socket, and assert the recording handler’s
     `StoppedCount == 1`. Reserve admission again and call the same path with the same gated handler;
     assert the count remains `1`.
   - This test proves the consumer side: `AckAcceptResult.Handler` is passed into
     `AbandonAdmission`. The producer side — `AcceptHandshake.CompleteBody` passing the created
     handler into `Faulted`/`Accepted` — is verified by inspection in the manual step below; the
     normal-stop test alone would pass without either change.
6. Update the Ack Direct integration tests in
   `Tests/Pontifex.Transport.Direct.Tests/Raw/Reliable/Ack/RawReliableAckDirectTransportTests.cs`:
   - Add `StoppedCount`, `DisconnectedCount`, and an ordered event list to `RecordingServerHandler`.
   - Add `ServerHandler_ReceivesOnDisconnectedThenOnStopped_OnNormalStop` and assert the ordered
     event list is `["connected", "disconnected", "stopped"]`.
7. After both carriers use the handler-aware completion overload, delete only the parameterless
   `CompleteAdmission(IRawReliableServerEndpointImp session)` overload and any test coverage that
   only exercised it. Keep the parameterless `AbandonAdmission()` because Direct/TCP carriers still
   use it on paths where no handler exists (for example `FailHandshake`, a duplicate channel entry,
   a null handler, or a `BeginAccept` failure).
8. Update the Direct and TCP test coverage documents if those projects maintain coverage files;
   otherwise update the in-file XML comments only. Do not create new suite structures for these
   loose test files.

**Verification**

Run:

```bash
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj \
  --filter "FullyQualifiedName~RawReliableAckDirectTransportTests"
dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj \
  --filter "TestCategory=Integration"
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj \
  --filter "TestCategory=Transports"
dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj
```

Expected: all selected tests pass.

Manual inspection: open
`Transports/Pontifex.Transport.Direct/Raw/Reliable/Ack/RawReliableAckDirectServerTransport.cs`
and confirm that the endpoint-constructor failure catch calls
`AbandonAdmission(handler, reason)` followed by `RejectChannel`, and that the success path calls
`CompleteAdmission(endpoint, handler, reason)`. The Direct carrier has no injectable endpoint
factory, so this path is verified by inspection rather than fault injection.

Also inspect `RawReliableAckTcpServerEndpoint.AcceptHandshake.CompleteBody` and confirm that the
handler created by `handlerFactory` is passed into `AckAcceptResult.Accepted(endpoint, handler)`
and into `AckAcceptResult.Faulted(..., handler)` on the endpoint-constructor failure path. The
Faulted test drives the consumer side; this inspection covers the producer side.

**Definition of done**

The Ack Direct and TCP carriers are updated to pass the gated handler, the TCP consumer failure
path is proved deterministically, the TCP producer hand-off and Direct endpoint-constructor
failure path are inspected, the Direct normal-stop sequence is proved, and the selected tests pass,
including the active Ack stack test plan in `Tests/Pontifex.Transport.Tests`. The shared
admission-failure behavior is covered by Stage 2.

**Risks / notes**

- `AckAcceptResult` is internal but used by tests through `InternalsVisibleTo`; keep the new member
  additive.
- The handshake can fail before a handler exists; those paths must remain handler-free.

---

### Stage 4 — Correct the Ack specification and conformance text

**Objective**

The Ack specification and conformance documentation describe the agreed `OnStopped` behavior and
no longer claim that server sessions never receive `OnStopped`.

**Inputs**

- Stages 1–3.

**Actions**

1. Modify `Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Specification.md`:
   - In §9.3, change the server session sequence to
     `OnConnected -> OnDisconnected -> OnStopped` and state that a session that never reaches
     `OnConnected` receives exactly one `OnStopped` and no `OnDisconnected`.
   - Remove the sentence “the reliable server transport does not invoke `OnStopped` for server
     sessions”.
   - In §9.1, remove the `(client only)` qualifier from the `Disconnected -> Stopped` and
     `Failed -> Stopped` transitions; server sessions also reach `Stopped` and receive `OnStopped`.
   - In §8.2 items 8 and 9, replace “receives no lifecycle callback” with “receives exactly one
     `OnStopped` and no `OnConnected` or `OnDisconnected`”.
   - In §8.3, change the `FillAckResponse` failure wording the same way.
   - In §13.1, change the pre-`OnConnected` failure sentence (the `FillAckResponse` paragraph) the
     same way. The other occurrences are in §8.2 and §8.3 and are covered above.
   - In §12.3, replace the sentence “It is not hit for a client whose `OnConnected` was never
     invoked … in that establishment-failure path” with the corrected gate rule: the gate is hit
     before the single `OnStopped` even when `OnConnected` was never invoked. Note that the
     `grep "for a client"` check will legitimately match §12.2’s client-only
     `BeforeAcknowledgerGate` sentence; do not change that one.
   - In §10.3, remove “for a client” from the local `OnStopped` wording; server sessions also
     receive it.
   - In §13.2, add an explicit “unless `Start` returned false” carve-out to the started-client
     `OnStopped` rule. Do not duplicate §7.3’s existing Start-false sentence; keep it normative.
   - In §13.3, state that a server transport stop disconnects every active session and that each
     session handler observes the documented sequence.
   - Keep §7.3’s existing Start-false sentence as normative; do not duplicate it.
   - Leave §7.2’s transport-level `onStopped` sentence unchanged. The current `AnyTransport.Start`
     clears `_onStopped` before a plain failed start, so it does not invoke the transport callback
     there; it invokes it only when `TryStart` stops the transport synchronously (which is the
     narrow divergence). Add a short `Known divergence` note immediately after that sentence
     stating that the synchronous-stop transport-callback behavior is tracked separately and is not
     changed by this plan. Do not silently “correct” it.
   - Search the document for any other “server ... OnStopped”, “no lifecycle callback”, or
     “does not invoke” sentence and correct it consistently, except the §7.2 transport-level
     note above.
2. Modify
   `Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Conformance/IRawReliableAckEndpointConformanceControl.cs`:
   - Replace the `BeforeHandlerStoppedGate` parenthetical with an explicit statement that, for an
     implementation claiming Carrier-Independent Core Conformance, the gate covers `OnStopped`
     for both client and server sessions.
   - Replace the sentence “Not hit in the establishment-failure path where `OnConnected` was never
     invoked” with the corrected rule: the gate is hit before the single `OnStopped` even when
     `OnConnected` was never invoked.
   - Add a note that the current production Ack endpoints do not claim conformance and do not yet
     raise this control; the conformance suite remains ignored. Do not claim current production
     behavior that does not exist.
3. Modify `Tests/Pontifex.Transports.Tests/Raw/Reliable/Ack/RawReliableAckTestServerHandler.cs`:
   - Make `OnStopped` virtual and record `StoppedReason`, `StoppedCount`, and `IsStopped`; the
     conformance suite currently cannot observe server `OnStopped` because the method is empty and
     non-virtual.
4. Update `Tests/Pontifex.Transports.Tests/Raw/Reliable/Ack/Coverage.md`:
   - Replace the “No OnStopped for server” and “no lifecycle callback” rows with the corrected
     sequence, specifically the `FillAckResponse exception` and
     `ACK response lost before commit` rows.
   - Update the `Successful acceptance` row from `OnConnected -> OnDisconnected` to
     `OnConnected -> OnDisconnected -> OnStopped`.
   - Update the `Session cleanup on reconnection` row so “old handlers never receive additional
     callbacks after their `OnDisconnected`” excludes the final `OnStopped` (which they do receive).
   - Update the `Reason propagation` row: remove “for a client”; server sessions also receive the
     local `OnStopped`.
   - Update the `BeforeHandlerStoppedGate` row to say the gate is an unimplemented conformance
     contract for client and server sessions, not a current production behavior.
   - Add the never-connected server session case, now observable through the updated test handler.
5. Update `Tests/Pontifex.Transports.Tests/Raw/Reliable/Ack/RawReliableAckConformanceTests.cs`:
   - Find any expectation that a server handler receives no `OnStopped` and update it to the
     corrected sequence; add `StoppedCount`/`StoppedReason` assertions where a server session
     reaches `OnStopped`.
   - Add a never-connected server session assertion using the updated handler.
   - The `[Ignore]` attribute lives on
     `Tests/Pontifex.Transports.Tests/Raw/Reliable/Ack/DirectRawReliableAckConformanceAdapterTests.cs`
     (the concrete adapter fixture), not on the abstract suite. Keep that attribute in place; do
     not un-ignore the suite.
6. Update any XML comment in `RawReliableAckServerEndpoint` or
   `RawReliableAckClientEndpoint` that restates the old rule.

**Verification**

Run:

```bash
grep -rn -E "does not invoke .*OnStopped|No OnStopped|no lifecycle callback|Not triggered for server sessions|establishment-failure path|for a client|client only" \
  Pontifex/Abstractions/Transports/Raw/Reliable/Ack \
  Tests/Pontifex.Transports.Tests/Raw/Reliable/Ack
dotnet build Pontifex.sln
```

Expected: the search returns no stale claim; any remaining match is either an explicitly
annotated “not implemented” conformance note or a correct new sentence such as “server sessions
also reach `Stopped` and receive `OnStopped`”. The build succeeds.

**Definition of done**

The Ack specification, control documentation, coverage, and conformance expectations consistently
describe the agreed server and client sequences.

**Risks / notes**

- The conformance suite remains ignored; this stage corrects its expectations, not its
  implementation.
- Do not weaken the document’s other guarantees while editing.

---

### Stage 5 — Replace the NoAck abstractions and add the NoAck specification

**Objective**

Delete the legacy NoAck abstraction and replace it with the endpoint-owned contracts, including the
`TryCreate` server handler factory and the NoAck specification.

**Inputs**

- The agreed contract in this plan.
- No dependency on carrier implementations.

**Actions**

1. Delete these files:
   - `Pontifex/Abstractions/Transports/Raw/Reliable/NoAck/IRawReliableNoAckClient.cs`
   - `Pontifex/Abstractions/Transports/Raw/Reliable/NoAck/IRawReliableNoAckServer.cs`
   - `Pontifex/Abstractions/Transports/Raw/Reliable/NoAck/IRawReliableNoAckClientSession.cs`
   - `Pontifex/Abstractions/Transports/Raw/Reliable/NoAck/IRawReliableNoAckConformanceControl.cs`
   - `Pontifex/Implementations/Raw/Reliable/NoAck/RawReliableNoAckTransport.cs`
   - `Pontifex/Implementations/Raw/Reliable/NoAck/RawReliableNoAckConformanceControl.cs`
2. Create `Pontifex/Abstractions/Transports/Raw/Reliable/NoAck/Client/IRawReliableNoAckClient.cs`:
   ```csharp
   namespace Pontifex.Raw.Reliable.NoAck
   {
       public interface IRawReliableNoAckClient
           : IRawReliableClientTransport<IRawReliableNoAckClientHandler>
       {
       }
   }
   ```
3. Create `Pontifex/Abstractions/Transports/Raw/Reliable/NoAck/Client/IRawReliableNoAckClientHandler.cs`:
   - Extends `IRawReliableClientHandler`.
   - Adds `void OnConnected(IRawReliableEndpoint endPoint);`.
   - XML documentation mirrors `IRawReliableAckClientHandler.OnConnected` minus ack response.
4. Create `Pontifex/Abstractions/Transports/Raw/Reliable/NoAck/Server/IRawReliableNoAckServer.cs`:
   - Extends `IRawReliableTransport`.
   - Adds `bool Init(IRawReliableNoAckServerHandlerFactory<IRawReliableNoAckServerHandler> factory);`.
5. Create `Pontifex/Abstractions/Transports/Raw/Reliable/NoAck/Server/IRawReliableNoAckServerHandlerFactory.cs`:
   - `public interface IRawReliableNoAckServerHandlerFactory<out THandler> where THandler : IRawReliableNoAckServerHandler`.
   - `THandler? TryCreate(IEndPoint remote);`.
   - Document that calls never overlap, that the returned handler owns no buffers, and that
     `null` rejects the connection.
6. Create `Pontifex/Abstractions/Transports/Raw/Reliable/NoAck/Server/IRawReliableNoAckServerHandler.cs`:
   - Extends `IRawReliableServerHandler`.
   - Adds `void OnConnected(IRawReliableEndpoint endPoint);`.
7. Create `Pontifex/Abstractions/Transports/Raw/Reliable/NoAck/IRawReliableNoAckEndpoint.cs`:
   - `public interface IRawReliableNoAckEndpoint : IRawReliableEndpoint { }` with a short marker
     comment tying it to endpoint implementation contracts.
8. Create `Pontifex/Abstractions/Transports/Raw/Reliable/NoAck/Specification.md` with these
   sections:
   - Scope: reliable, connection-oriented, no application acknowledgement step.
   - Adoption: the Ack specification’s endpoint, delivery, ownership, failure, and callback
     sections are the common core; this document overrides admission and handshake only.
   - Public contract: the six interfaces from actions 2–7.
   - Admission: server factory `TryCreate(remote)`, serialized, `null` rejects, exception is a
     rejection, no ack payloads, rejection observed as a close (carrier-defined).
   - Lifecycle: the exact rule table from this plan, including Start-false and server sessions.
   - Carrier notes: TCP is the only in-scope carrier; `tcp-noack` is the canonical scheme; Direct
     NoAck and conformance controls are deferred.
   - Non-goals: no ack handshake, no authentication, no automatic reconnect.
9. Update `Tests/Pontifex.Tests/Transports/Coverage.md`:
   - Remove the planned `RawReliableNoAckTransport` layer row.
   - Add a note that the NoAck transport layers are the new
     `RawReliableNoAckClientTransport` and `RawReliableNoAckServerTransport` layers added in
     Stage 7. Do not leave a reference to a deleted type.
10. Ensure no file references the deleted types:
   - `grep -rnI --exclude=plan.md --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git -E "IRawReliableNoAckClientSession|IRawReliableNoAckConformanceControl|RawReliableNoAckTransport|RawReliableNoAckConformanceControl" .`
   - The only allowed result is this plan file, which is excluded.

**Verification**

Run:

```bash
dotnet build Pontifex.sln
grep -rnI --exclude=plan.md --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git -E "IRawReliableNoAckClientSession|IRawReliableNoAckConformanceControl|RawReliableNoAckTransport|RawReliableNoAckConformanceControl" .
```

Expected: build succeeds and the search finds no matches.

**Definition of done**

The legacy NoAck types are gone, the new contracts compile, and the NoAck specification exists.

**Risks / notes**

- The old flat files use the `Pontifex.Raw.Reliable.NoAck` namespace; keep the same namespace after
  moving into `Client/` and `Server/` folders.
- Do not add compatibility shims.

---

### Stage 6 — Add the NoAck endpoint bases

**Objective**

Provide `RawReliableNoAckClientEndpoint` and `RawReliableNoAckServerEndpoint` as the thin logical
lifecycle layer over the existing reliable endpoint bases, and cover them in the Endpoints suite.

**Inputs**

- Stage 5.
- Existing `RawReliableClientEndpoint`, `RawReliableServerEndpoint`, and their test harnesses.

**Actions**

1. Create `Pontifex/Implementations/Endpoints/Raw/Reliable/NoAck/IRawReliableNoAckClientEndpointImp.cs`:
   - `public interface IRawReliableNoAckClientEndpointImp : IRawReliableClientEndpointImp, IRawReliableNoAckEndpoint`.
2. Create `Pontifex/Implementations/Endpoints/Raw/Reliable/NoAck/IRawReliableNoAckServerEndpointImp.cs`:
   - `public interface IRawReliableNoAckServerEndpointImp : IRawReliableServerEndpointImp, IRawReliableNoAckEndpoint`.
3. Create `Pontifex/Implementations/Endpoints/Raw/Reliable/NoAck/RawReliableNoAckClientEndpoint.cs`:
   - `public abstract class RawReliableNoAckClientEndpoint : RawReliableClientEndpoint, IRawReliableNoAckClientEndpointImp`.
   - It must be public because concrete carrier endpoints in `Pontifex.Transport.Net` derive from it.
   - Constructor mirrors the Ack client endpoint constructor:
     `(string transportName, IEndPoint remoteEndPoint, IRawReliableNoAckClientHandler handler,
     int messageMaxByteSize, long bufferCapacityBytes, IMemoryRental memory, ILogger log,
     RawReliableClientEndpoint.PingOptions? pingOptions)`.
   - Store the handler and add `private int _connectedNotified; private int _disconnectedNotified;`.
   - `protected override bool GetIsConnected()` returns `IsLowLevelConnected` only when
     `_disconnectedNotified == 0`.
   - `protected override void OnConnected()`:
     - Set `_connectedNotified = 1`.
     - Call `_handler.OnConnected(this)` inside `try`/`catch`; on exception, log, call
       `Stop(new ExceptionFail(TransportName, ex, "OnConnected failed"), false)`, and return.
     - Call `base.OnConnected()` to start keep-alive.
   - `protected override void FailHard(StopReason reason)`:
     - Call `NotifyDisconnected(reason)`.
     - Call `base.FailHard(reason)`.
   - `protected override void BeginDisconnect(StopReason reason, bool tryBeGentle)`:
     - Call `NotifyDisconnected(reason)`.
     - Do not call `FinishDisconnect`; the carrier completes the disconnect.
   - `private void NotifyDisconnected(StopReason reason)`:
     - Use `if (Interlocked.Exchange(ref _disconnectedNotified, 1) == 0 && _connectedNotified != 0)`
       so the same flag carries both the disconnected state used by `GetIsConnected` and the
       once-only guarantee.
     - Inside that branch, call `_handler.OnDisconnected(reason)` inside `try`/`catch`.
   - Add XML documentation mirroring the Ack endpoint’s `OnDisconnected`/`OnStopped` guarantees.
4. Create `Pontifex/Implementations/Endpoints/Raw/Reliable/NoAck/RawReliableNoAckServerEndpoint.cs`:
   - `public abstract class RawReliableNoAckServerEndpoint : RawReliableServerEndpoint, IRawReliableNoAckServerEndpointImp`.
   - Mirror action 3, using `IRawReliableNoAckServerHandler` and the `TimeSpan disconnectTimeout`
     constructor parameter.
   - `base.OnConnected()` starts the idle watchdog.
   - `PeriodicTick` is inherited.
5. Add test harness files under `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/NoAck/Harness/`:
   - `TestRawReliableNoAckClientEndpoint.cs`: mirrors `TestRawReliableAckClientEndpoint` but
     disables keep-alive and derives from the NoAck base; expose `DrainAll`, `CompleteConnect`
     (public), `ConnectAndComplete`, `CompleteDisconnect`, `ExposedProcessInbound`,
     `ExposedFailHard`, and captured sends. `CompleteConnect` calls `FinishConnect` and drains the
     queue, so a stop latched while the endpoint is still starting can complete.
   - `RecordingRawReliableNoAckClientHandler.cs`: derives from the existing
     `RecordingRawReliableClientHandler` and re-lists `IRawReliableNoAckClientHandler`; records
     `ConnectedCount` and `ConnectedEndPoint`. Add a `Func<bool>? IsConnectedProbe` and explicitly
     re-implement `IRawReliableHandler.OnDisconnected` (the interface that declares the member; the
     base member is not virtual) so it can capture `IsConnected` during the callback and then call
     `base.OnDisconnected(reason)`. Add `bool ThrowOnConnected` / `Exception? ConnectedException`
     so `OnConnected_WhenHandlerThrows_StopsWithExceptionFail` can be written. `OnConnected` sets
     `_connectedNotified`; `NotifyDisconnected` sets `_disconnectedNotified` before invoking the
     handler, so the throwing `OnConnected` path still delivers `OnDisconnected` and then
     `OnStopped`.
6. Add `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/NoAck/RawReliableNoAckClientEndpointTests.cs`
   with `[Category("Endpoints")]`:
   - `Connect_NotifiesHandlerAndSetsIsConnected`.
   - `Stop_WhenConnected_NotifiesDisconnectedThenStopped`.
   - `Stop_WhileConnecting_ReportsConnectionThenTearsDown`: `Start()` -> `DrainAll()` (the endpoint
     enters `Starting` and its `BeginConnect` only records) -> `Stop(reason, false)` -> `DrainAll()`
     -> `CompleteConnect()`. The reliable base deliberately reports the connection first when a
     stop was latched while connecting, so assert `ConnectedCount == 1`,
     `DisconnectedCount == 1`, then call `CompleteDisconnect()` and assert `StoppedCount == 1`.
   - `FailHard_WhenConnected_NotifiesDisconnectedThenStopped`.
   - `FailHard_WhenConnecting_NotifiesStoppedOnly`: `Start()` -> `DrainAll()` ->
     `ExposedFailHard(reason)`; assert `DisconnectedCount == 0` and `StoppedCount == 1`.
   - `Disconnect_WhenConnected_MakesIsConnectedFalseDuringOnDisconnected` using a handler probe.
   - `OnConnected_WhenHandlerThrows_StopsWithExceptionFail`.
7. Mirror actions 5–6 for the server under
   `Tests/Pontifex.Tests/Endpoints/RawReliable/Server/NoAck/`, using
   `TestRawReliableNoAckServerEndpoint`, `RecordingRawReliableNoAckServerHandler`, and the
   `Now`/`AdvanceNow` seam. Add:
   - `Connect_NotifiesHandler`: assert the NoAck handler’s `OnConnected` and `IsConnected`; do not
     re-test the watchdog start, which the ancestor `RawReliableServerEndpointTests` already
     covers.
   - `PeriodicTick_WhenIdle_NotifiesDisconnectedThenStopped`: the ancestor layer already proves the
     watchdog timeout and stop reason; this derived test covers only the NoAck delta — it must
     assert `DisconnectedCount == 1` with `TimeOut` after the tick and `StoppedCount == 1` after
     `CompleteDisconnect`.
   - The same connect/stop/fail-hard sequence tests as the client, using the server harness’s
     public `CompleteConnect` for the `Stop_WhileConnecting_ReportsConnectionThenTearsDown`
     sequence; the server test must also assert the `OnConnected -> OnDisconnected` order before
     `CompleteDisconnect()` and the final `OnStopped`. `TestRawReliableServerEndpoint.CompleteConnect`
     is private in the ancestor harness, so `TestRawReliableNoAckServerEndpoint` must expose its
     own public `CompleteConnect` (as the client NoAck harness does).
8. Add `Coverage.md` in both new NoAck layers.
9. Update `Tests/Pontifex.Tests/Endpoints/README.md`:
   - Add the two NoAck layers to the inheritance tree, layer table (subject, directory,
     inheritance parent, coverage path), and directory layout.
   - Add contract links for `RawReliableNoAckClientEndpoint`, `RawReliableNoAckServerEndpoint`,
     `IRawReliableNoAckClientHandler`, `IRawReliableNoAckServerHandler`, and
     `IRawReliableNoAckEndpoint` to the “Contract references” list.

**Verification**

Run:

```bash
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj \
  --filter "FullyQualifiedName~RawReliableNoAckClientEndpointTests|FullyQualifiedName~RawReliableNoAckServerEndpointTests"
```

Expected: all selected tests pass.

**Definition of done**

Both NoAck endpoint bases compile and pass their lifecycle tests, and the Endpoints suite README
and coverage describe the new layers.

**Risks / notes**

- `IsConnected` must be false inside `OnDisconnected`; the `_disconnectedNotified` flag and the
  base reliable stage must agree.
- Do not duplicate the inherited reliable tests; only cover the NoAck delta.
- Keep keep-alive disabled in the client harness to avoid timers.

---

### Stage 7 — Add the NoAck transport bases

**Objective**

Provide `RawReliableNoAckClientTransport` and `RawReliableNoAckServerTransport` with the
`TryCreate` factory, stop-once gate, and admission-failure notification, and cover them in the
Transports suite.

**Inputs**

- Stages 5 and 6.
- Existing `RawReliableClientTransport<T>`, `RawReliableServerTransport<T>`, and the Ack server
  transport pattern.

**Actions**

1. Create `Pontifex/Implementations/Raw/Reliable/NoAck/StopOnceNoAckServerHandler.cs`:
   - Internal sealed wrapper implementing `IRawReliableNoAckServerHandler`.
   - `Inner` property; forwards `OnConnected`, `OnReceived`, `OnDisconnected`.
   - `OnStopped` uses `StopOnceNotifier`; no separate `NotifyStopped` method.
2. Create `Pontifex/Implementations/Raw/Reliable/NoAck/RawReliableNoAckClientTransport.cs`:
   - `public abstract class RawReliableNoAckClientTransport
     : RawReliableClientTransport<IRawReliableNoAckClientHandler>, IRawReliableNoAckClient`.
   - It must be public because concrete carrier transports in `Pontifex.Transport.Net` derive from
     it.
   - `Type => TransportType.RawReliableNoAck`.
   - Constructor `(string typeName, ILogger logger, IMemoryRental memory)` passes
     `new RawReliableConformanceControl()` to the base.
   - Abstract `protected IRawReliableNoAckClientEndpointImp CreateEndpoint(IRawReliableNoAckClientHandler handler);`.
   - Sealed `BeginConnect` mirrors `RawReliableAckClientTransport.BeginConnect`: handler null check,
     `CreateEndpoint`, `AttachEndpoint`, `endpoint.Start()`, log failures.
   - `DestroyTransport` stops `AttachedEndpoint` with `Stop(reason, false)` and isolates exceptions.
3. Create `Pontifex/Implementations/Raw/Reliable/NoAck/RawReliableNoAckServerTransport.cs`:
   - `public abstract class RawReliableNoAckServerTransport
     : RawReliableServerTransport<IRawReliableNoAckServerHandler>, IRawReliableNoAckServer`.
   - It must be public because concrete carrier transports in `Pontifex.Transport.Net` derive from
     it.
   - `Type => TransportType.RawReliableNoAck`.
   - Fields: `_initLock`, `_initAttempted`, `_factory`, `_factoryLock`.
   - Constructor `(string typeName, ILogger logger, IMemoryRental memory,
     int maxConcurrentHandshakes, int maxActiveSessions, int tickPeriodMs)` passes
     `new RawReliableConformanceControl()` to the base.
   - `Init(IRawReliableNoAckServerHandlerFactory<IRawReliableNoAckServerHandler> factory)` mirrors
     `RawReliableAckServerTransport.Init`: null throws, one-shot, checks `HasStartBeenAttempted` and
     `IsValid`, publishes the factory, calls `TryMarkInitialized`, returns false on later calls.
   - `protected IRawReliableNoAckServerHandler? TryCreateHandler(IEndPoint remote)`:
     - Return `null` when not valid or not started.
     - Under `_factoryLock`, call `_factory.TryCreate(remote)`.
     - On a non-null result, wrap in `StopOnceNoAckServerHandler`.
     - Catch and log exceptions, return `null`.
   - `protected bool TryBeginAdmission()`: reserve session, then `TryBeginHandshake`; release session
     on handshake failure. Mirror the Ack base.
   - Do not add a parameterless `CompleteAdmission(IRawReliableServerEndpointImp session)` overload;
     it would be dead code. Add only
     `protected bool CompleteAdmission(IRawReliableServerEndpointImp session,
     IRawReliableNoAckServerHandler? handler, StopReason failureReason)`: `EndHandshake`, call
     `TryStartAndRegisterSession(session, out bool sessionStarted)`, and notify
     `handler?.OnStopped(failureReason)` on registration failure only when
     `sessionStarted == false` inside `try`/`catch` with `Log.wtf`. A started session owns its
     terminal notification.
   - `protected void AbandonAdmission()` delegates with `null`.
   - `protected void AbandonAdmission(IRawReliableNoAckServerHandler? handler, StopReason reason)`:
     `EndHandshake`, `ReleaseSession`, `handler?.OnStopped(reason)` inside `try`/`catch` with
     `Log.wtf`.
4. Add test harnesses:
   - `Tests/Pontifex.Tests/Transports/Raw/Reliable/Client/NoAck/Harness/TestRawReliableNoAckClientTransport.cs`
     deriving the NoAck client transport, returning `TestRawReliableNoAckClientEndpoint`, and
     exposing `CreateEndpointCalls` and `AttachedEndpoint` for the start/stop delta tests.
   - `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/NoAck/Harness/TestRawReliableNoAckServerTransport.cs`
     exposing `Init`, `ExposedTryBeginAdmission`, `ExposedTryCreateHandler`,
     `ExposedTryStartAndRegisterSession`, `ExposedCompleteAdmission`,
     `ExposedAbandonAdmission`. Override `LogicDriverFactory` to return the existing
     `ManualPeriodicLogicDriver` from
     `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/Harness/ManualPeriodicLogicDriver.cs`,
     so `Start` does not create a real thread or timer.
   - `RecordingRawReliableNoAckServerHandlerFactory.cs` recording `TryCreate` calls and returning a
     configured handler or null.
5. Add `Tests/Pontifex.Tests/Transports/Raw/Reliable/Client/NoAck/RawReliableNoAckClientTransportTests.cs`
   with `[Category("Transports")]`:
   - Contract/type assertions: `IRawReliableNoAckClient`,
     `IRawReliableClientTransport<IRawReliableNoAckClientHandler>`, `IRawReliableTransport`,
     `IRawTransport`, `ITransport`, and `Type == RawReliableNoAck`.
   - `Start_CreatesAndStartsEndpoint`: `Init` a recording NoAck client handler, `Start`, assert
     the harness’s `CreateEndpointCalls == 1`, `AttachedEndpoint` is the created endpoint, and
     `IsConnected` is false; complete the endpoint connect and assert `IsConnected` is true.
   - `Stop_DestroysAttachedEndpoint`: `Start`, `Stop`, assert the attached endpoint received the
     stop and the transport is not started.
   - Expose `CreateEndpointCalls` and `AttachedEndpoint` on
     `TestRawReliableNoAckClientTransport` and reuse `TestRawReliableNoAckClientEndpoint` from the
     Endpoints harness for these tests.
6. Add `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/NoAck/RawReliableNoAckServerTransportTests.cs`
   with `[Category("Transports")]`:
   - `Init_WithNull_Throws`.
   - `Init_FirstCallSucceeds_SecondFails`.
   - `Init_AfterStartAttempt_ReturnsFalse`.
   - `TryCreateHandler_WhenNotStarted_ReturnsNull`.
   - `TryCreateHandler_ReturnsStopOnceWrapper`.
   - `TryCreateHandler_WhenFactoryThrows_ReturnsNull`.
   - `TryCreateHandler_InvokesFactoryWithRemote`.
   - `TryCreateHandler_CallsDoNotOverlap` using a blocking factory and two threads; mark this
     test `[Category("Transports.Concurrency")]`. The category is already declared in the
     Transports suite README, so no README category change is needed.
   - `AbandonAdmission_WithHandler_NotifiesOnStoppedOnce`: reserve admission; obtain the gate with
     `ExposedTryCreateHandler(remote)` and use that exact gate instance for the abandon call and
     the subsequent direct `handler.OnStopped`; assert one stop. Do not pass the raw recording
     handler.
   - `CompleteAdmission_WhenSessionCannotStart_NotifiesOnStoppedOnce`: reserve admission, construct
     the session with the same `StopOnceNoAckServerHandler` gate passed to the admission overload,
     force the start schedule to fail, and assert one stop.
   - `CompleteAdmission_WhenTransportStopping_DoesNotPreemptStartedSession`: reserve admission,
     stop the transport, complete admission with a `TestRawReliableServerSession` whose start
     succeeds, and assert `StoppedCount == 0` immediately after completion; then drain the session
     and assert `StoppedCount == 1` and `ActiveSessions == 0`. The base server session double does
     not raise `OnConnected`/`OnDisconnected`, so do not assert those callbacks here.
   - `Type_IsRawReliableNoAck`.
7. Add `Coverage.md` in both new NoAck transport layers.
8. Update `Tests/Pontifex.Tests/Transports/README.md`:
   - Add the two NoAck transport layers to the inheritance tree, layer table, and directory layout.
   - Add contract links for `RawReliableNoAckClientTransport`,
     `RawReliableNoAckServerTransport`, `IRawReliableNoAckClient`, `IRawReliableNoAckServer`, and
     `IRawReliableNoAckServerHandlerFactory` to the “Contract references” list.
   - Reword the `Transports.Concurrency` description from “the one server-layer test” to
     “server-layer tests” so it covers both `TryCreateHandler_CallsDoNotOverlap` and the existing
     `RawUnreliableServerTransport` concurrency test.

**Verification**

Run:

```bash
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj \
  --filter "FullyQualifiedName~RawReliableNoAckClientTransportTests|FullyQualifiedName~RawReliableNoAckServerTransportTests"
```

Expected: all selected tests pass.

**Definition of done**

Both NoAck transport bases compile, the factory and admission tests pass, and the Transports suite
README and coverage describe the new layers.

**Risks / notes**

- Factory calls must be serialized; use a dedicated lock, not the transport lifecycle lock.
- The gate must be the handler passed to the endpoint, so failure paths and endpoint teardown share
  the same stop-once state.
- Do not add conformance controls.

---

### Stage 8 — Implement the T3 NoAck TCP client endpoint and client transport

**Objective**

Provide a fresh, handshake-free TCP NoAck client endpoint and client transport, verified against a
raw loopback peer without requiring the NoAck server.

**Inputs**

- Stages 5–7.
- Existing Ack TCP client endpoint and transport as the reference.

**Actions**

1. Create `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/NoAck/RawReliableNoAckTcpClientEndpoint.cs`
   in namespace `Pontifex.Raw.Reliable.NoAck.Tcp`:
   - `internal sealed class RawReliableNoAckTcpClientEndpoint : RawReliableNoAckClientEndpoint`.
   - Copy the socket plumbing from `RawReliableAckTcpClientEndpoint`:
     - fields for `IpEndPoint`, `RemoteHalfClosePolicy`, receive buffer size, message part max
       size, connect timeout, graceful shutdown deadline, send source initial capacity, source,
       receive compositor, low-level socket, socket, connect cancellation, lifecycle/receive gates,
       and the connect/teardown flags.
     - `BeginConnect` with asynchronous `Socket.BeginConnect`, connect timeout, low-level socket
       creation, `FinishConnect(BeginAbortLowLevel)`.
     - `DoSendRaw` with `UnionDataListCompositor.Encode` and `lowLevel.Sender.Pump`.
     - `BeginDisconnect` that first calls `base.BeginDisconnect(reason, tryBeGentle)` (the NoAck
       base raises `OnDisconnected`). When no low-level socket exists (failed-connect path), it may
       call `FinishDisconnect` synchronously on the serial queue, matching the Ack reference. On
       the live-socket path it starts the asynchronous teardown and returns promptly; the
       continuation sends the graceful disconnect frame, shuts down the sender, disposes the
       low-level socket, drains the source, and calls `FinishDisconnect`. Do not block the serial
       action queue on socket I/O on the live-socket path.
     - `FailHard`, `OnReceived`/`OnFramedMessage`, `OnCallbackError`, `OnSocketCompleted`, teardown
       helpers, and allocation-free best-effort helpers.
   - The Ack TCP client class contains no ack-specific members; the ack stage, `FillAckData`,
     `BeginAckConnect`, `FinishAckConnect`, and ack-response handling live in its parent
     `RawReliableAckClientEndpoint`. Copy only the socket plumbing and do not introduce any ack
     member into the NoAck class.
   - `OnConnected` must call `base.OnConnected()` first, then start the low-level receiver only
     when `IsRunning` is still true. A handler exception or a handler-initiated `Disconnect` inside
     `OnConnected` stops the endpoint before the queued stop action runs; do not start the receiver
     for that endpoint. If the receiver start throws, stop with `ExceptionFail`.
   - Do not add a `ToString` override to the endpoint; the Ack endpoint has none. The transport
     owns the `tcp-noack-client[...]` label.
   - Keep keep-alive from the NoAck base; no handshake delays it.
2. Add `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/NoAck/TcpNoAckInfo.cs`:
   - `internal static class TcpNoAckInfo` with `public const string TransportName = "tcp-noack";`,
     `DefaultMessageMaxSize = 1024 * 1024`, `DefaultBufferCapacity = 1024 * 1024 * 100`,
     `ServerConnectionsLimit = 20000`, and
     `DefaultDisconnectTimeout = TimeSpan.FromSeconds(180)`, copied from `TcpInfo`. Do not reuse
     the Ack `TcpInfo` name in NoAck code; the values are the same but the transport name differs.
3. Create `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/NoAck/RawReliableNoAckTcpClientTransport.cs`:
   - `public sealed class RawReliableNoAckTcpClientTransport : RawReliableNoAckClientTransport`.
   - Copy the Ack TCP client transport’s constructor parameter set and validation minus ack
     parameters, `MessageMaxByteSize` property, `BuildPingOptions`, and `CreateEndpoint` returning
     `new RawReliableNoAckTcpClientEndpoint(...)`.
   - Use `TcpNoAckInfo.TransportName` (value `"tcp-noack"`) for `base(typeName, ...)` and in
     `ToString`, which must produce `tcp-noack-client[...]`. Do not inherit the Ack `"tcp"` name,
     because it would mislabel stop reasons, log tags, and diagnostics.
4. Add `Tests/Pontifex.Transport.Net.Tests/Raw/Reliable/NoAck/RawReliableNoAckTcpClientEndpointIntegrationTests.cs`
   with `[Category("Integration")]`:
   - Use a real `TcpListener` on `IPAddress.Loopback`, `AcceptWithTimeout`, and raw peer helpers
     modeled on the Ack TCP integration test.
   - `HappyPath_ConnectsAndNotifiesClientHandler`: start the endpoint, accept the raw peer, assert
     the client handler’s `OnConnected` event is set and the endpoint `IsConnected` is true.
   - `Send_TransmitsPayloadFrameToRawPeer`: send a `UnionDataList` with one value; the raw peer
     reads one framed payload, decodes it with `UnionDataListCompositor`/`ByteSourceFromArray`, and
     asserts the first decoded `UnionData` is `PayloadMessage` (1) and the next value matches. Do
     not assert a raw byte at the frame start; the frame is a serialized `UnionDataList` behind the
     length prefix.
   - `RawPeerPayload_IsDeliveredToHandler`: raw peer sends a payload frame; assert the handler
     receives the value.
   - `RawPeerDisconnect_NotifiesDisconnectedThenStopped`: raw peer sends a disconnect frame (2);
     assert `OnDisconnected` then `OnStopped` exactly once.
   - `RawPeerClose_NotifiesDisconnectedThenStopped`: raw peer closes the socket; assert the same
     sequence.
   - `GracefulStop_SendsDisconnectFrame`: call `Disconnect` on the endpoint; the raw peer reads and
     decodes a frame and asserts its first decoded `UnionData` is `DisconnectMessage` (2).
   - `Send_WhenNotConnected_ReturnsNotConnected`.
   - `HandlerDisconnectsInOnConnected_StopsCleanly`: the recording handler calls
     `endpoint.Disconnect` from `OnConnected`; assert the endpoint stops and the raw peer observes
     a close/disconnect without a receiver-start failure.
   - Add a file-local `RecordingNoAckClientHandler : IRawReliableNoAckClientHandler` with
     `Connected`, `Disconnected`, `Stopped` events, `OnConnected`/`OnDisconnected` counters, a
     received-value list, a stored `Endpoint`, and a `bool DisconnectOnConnected` flag for the
     handler-disconnects test; the Stage 6/7 doubles live in `Pontifex.Tests` and cannot be reused
     here.
   - Use the `Stopped`/`Connected` events and bounded waits (10 seconds) as the Ack tests do.
5. Add XML documentation and comments describing the no-handshake behavior.
6. Add a short coverage note or XML comment for the client endpoint/transport; do not create a new
   suite structure because these are loose integration test files under an existing project.

**Verification**

Run:

```bash
dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj \
  --filter "FullyQualifiedName~RawReliableNoAckTcpClientEndpointIntegrationTests"
```

Expected: all selected tests pass.

**Definition of done**

The NoAck TCP client endpoint connects, sends, receives, disconnects, and stops correctly against a
raw peer, and the client transport compiles.

**Risks / notes**

- This is a fresh copy by design (T3); do not refactor the Ack endpoint into a shared base in this
  stage.
- The receiver must start only after `base.OnConnected()` so `OnReceived` cannot precede
  `OnConnected`.
- The raw peer must decode the same length-prefixed `UnionDataList` framing used by
  `UnionDataListCompositor`.

---

### Stage 9 — Implement the T3 NoAck TCP server endpoint, server transport, and `tcp-noack` binding

**Objective**

Provide the handshake-free TCP NoAck server carrier, the `tcp-noack` factory binding, and
end-to-end client/server tests.

**Inputs**

- Stage 8.
- Existing Ack TCP server endpoint/transport and binding as the reference.

**Actions**

1. Create `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/NoAck/RawReliableNoAckTcpServerEndpoint.cs`
   in namespace `Pontifex.Raw.Reliable.NoAck.Tcp`:
   - `internal sealed class RawReliableNoAckTcpServerEndpoint : RawReliableNoAckServerEndpoint`.
   - The accepted-socket constructor must be `internal` (not `private`): the Ack counterpart is
     private because its static `BeginAccept` is the only caller; the NoAck server transport
     constructs the endpoint directly from `OnClientConnected` in the same assembly.
   - Copy the socket plumbing from `RawReliableAckTcpServerEndpoint`:
     - accepted-socket constructor, socket options, source, receive compositor, low-level socket,
       `BeginConnect`, `DoSendRaw`, `FailHard`, receive handling, socket completion, teardown,
       source draining, and helpers.
     - `BeginDisconnect` must first call `base.BeginDisconnect(reason, tryBeGentle)` (the NoAck
       base raises `OnDisconnected`). On the failed-connect path where no low-level socket exists,
       it may call `FinishDisconnect` synchronously, matching the Ack reference. On the live-socket
       path it starts the asynchronous graceful/abrupt teardown and returns promptly;
       `FinishDisconnect` is called from the teardown continuation, not on the serial action-queue
       thread.
   - Delete every ack-specific member: `AckAcceptStatus`, `AckAcceptResult`, `AcceptHandshake`,
     `BeginAccept`, `SetPendingAckData`, `ReleasePendingAckData`, `FinishAckConnect`,
     `SendAckResponse`, ack response/request `ProcessInboundMessage` overrides, and pending ack
     fields.
   - `OnConnected` must call `base.OnConnected()` first (handler `OnConnected` and watchdog), then
     start the low-level receiver only when `IsRunning` is still true. A handler exception or a
     handler-initiated `Disconnect` inside `OnConnected` must not start the receiver.
   - `Now` returns `DateTime.UtcNow`.
   - Do not add a `ToString` override to the endpoint; the Ack endpoint has none. The transport
     owns the `tcp-noack-server[...]` label.
2. Create `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/NoAck/RawReliableNoAckTcpServerTransport.cs`:
   - `public sealed class RawReliableNoAckTcpServerTransport : RawReliableNoAckServerTransport`.
   - Constructor parameters mirror the Ack TCP server transport minus `ackMaxByteSize` and
     `ackHandshakeTimeout`:
     `(IPAddress ipAddress, int port, int connectionsLimit, TimeSpan disconnectTimeout,
     int? messageMaxSize, ILogger logger, IMemoryRental memory, int handshakeLimit = 200,
     long? bufferCapacityBytes = null, int backlog = 500, int? tickPeriodMs = null,
     RemoteHalfClosePolicy remoteHalfClosePolicy = RemoteHalfClosePolicy.GracefulSendShutdown,
     int? receiveBufferSize = null, int? messagePartMaxSize = null,
     TimeSpan? gracefulShutdownDeadline = null, int? sendSourceInitialCapacity = null)`.
   - Pass `TcpNoAckInfo.TransportName` (value `"tcp-noack"`) to `base(typeName, ...)`; do not
     inherit the Ack `"tcp"` name.
   - Reuse `AsyncServerSocketListener` and `IServerSocketListener` from
     `Pontifex.Raw.Reliable.Ack.Tcp` (same assembly) via
     `using Pontifex.Raw.Reliable.Ack.Tcp;`. They are carrier-neutral accept-loop helpers; do not
     copy them. Record the namespace debt for the future consolidation.
   - `StartCarrier` and `StopCarrier` mirror the Ack TCP server transport minus handshake-socket
     tracking. `StopCarrier` stops the listener only.
   - `ToString` must print `tcp-noack-server[...]`.
   - `OnClientConnected(Socket socket)`:
     - Reject when stopping.
     - `TryBeginAdmission()`; reject when false.
     - Resolve the remote `IEndPoint` from `socket.RemoteEndPoint`; on failure, abandon admission
       and reject.
     - `handler = TryCreateHandler(remote)`; on null, parameterless `AbandonAdmission()` and reject.
     - Create the `RawReliableNoAckTcpServerEndpoint` inside `try`/`catch`; on exception, call
       `AbandonAdmission(handler, new ExceptionFail(Name, ex, "Failed to create the server endpoint"))`
       and reject the socket.
     - On success, call `CompleteAdmission(endpoint, handler,
       new TextFail(Name, "Failed to start or register a TCP session"))`. On false log a warning;
       the gate and endpoint own cleanup.
   - `MessageMaxByteSize` property and `ToString`.
3. Create `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/NoAck/RawReliableNoAckTcpBinding.cs`:
   - `public static class RawReliableNoAckTcpBinding`.
   - `public const string SchemeName = TcpNoAckInfo.TransportName;` (value `"tcp-noack"`).
   - `public static ITransportRegistration CreateRegistration()` returns
     `new TransportRegistration(...)` with `TransportType.RawReliableNoAck` and parameters
     `NetBindingSupport.HostParameter()`, `NetBindingSupport.PortParameter()`, and
     `new IntegerParameter("disconnection_timeout", required: false, positional: false,
     new IntegerType(0, long.MaxValue))`.
   - `Create` resolves the address and timeout and constructs the server or client NoAck transport,
     using `TcpNoAckInfo` defaults and `TcpNoAckInfo.TransportName` for the transport name.
4. Add `Tests/Pontifex.Transport.Net.Tests/Raw/Reliable/NoAck/RawReliableNoAckTcpTransportIntegrationTests.cs`
   with `[Category("Integration")]`:
   - Add a local `GetFreeLoopbackPort()` helper: create a `TcpListener` on
     `IPAddress.Loopback, 0`, read its `LocalEndpoint.Port`, stop the listener, and return the
     port. The NoAck server transport exposes no bound-endpoint property and `ToString()` reports
     the configured port, so every end-to-end test must construct the server on a pre-allocated
     free port and point the client at that port.
   - `ServerAndClient_ConnectAndNotifyBothHandlers`: construct the server transport with a free
     loopback port, `Init` with a factory, `Start`; construct the client transport, `Init` with a
     recording client handler, `Start`; assert the factory is called once with the remote and both
     handlers connect.
   - `BidirectionalMessages_AreDeliveredInOrder`: send 10 values each way and assert order.
   - `ServerSendImmediatelyInOnConnected_ReachesClientAfterClientOnConnected`: server sends in
     `OnConnected`; assert the client handler receives after its `OnConnected`.
   - `ClientSendImmediatelyInOnConnected_ReachesServer`: client sends in `OnConnected`; assert the
     server handler receives.
   - `FactoryReturnsNull_RejectsConnection`: server factory returns null; assert no server handler
     and the client stops.
   - `ServerStop_DisconnectsSessions`: stop the server; assert each server session handler receives
     `OnDisconnected` then `OnStopped`; assert the client stops.
   - `ClientStop_DisconnectsSession`: stop the client; assert `OnDisconnected` then `OnStopped` on
     both sides.
   - `GracefulDisconnect_FromClient_SendsDisconnectFrame`: call `Disconnect` on the client
     endpoint; assert both handlers observe the disconnect.
   - `IdleClient_IsStoppedByServerWatchdog`: configure a short server disconnect timeout and tick
     period, and construct the client with `pingPeriod: TimeSpan.Zero` so the client does not keep
     the session alive with pings; assert the server session stops with `TimeOut`.
   - `KeepAlive_KeepsConnectionAlive`: configure short ping/pong periods and assert no timeout.
   - `EmptyMessage_IsDelivered`.
   - `OversizedMessage_ReturnsMessageTooBig`.
   - `ServerHandlerDisconnectsInOnConnected_StopsCleanly`: the server handler calls
     `endpoint.Disconnect` from `OnConnected`; assert the session stops and the client observes the
     disconnect without a receiver-start failure.
   - Add file-local `RecordingNoAckServerHandler : IRawReliableNoAckServerHandler`,
     `RecordingNoAckServerHandlerFactory : IRawReliableNoAckServerHandlerFactory<IRawReliableNoAckServerHandler>`,
     and `RecordingNoAckClientHandler` with connected/received/disconnected/stopped recording. The
     server handler stores its `Endpoint` and has a `bool DisconnectOnConnected` flag for the
     handler-disconnects test. The Stage 6/7 doubles live in `Pontifex.Tests` and cannot be reused
     in this project.
   - Use 10-second bounded waits and dispose transports in `finally`.
5. Modify `Tests/Pontifex.Transports.Tests/FactoryBindingsTests.cs`:
   - Add `using Pontifex.Raw.Reliable.NoAck.Tcp;`.
   - Add these exact `CompileCases` rows:
     - `Case(Registration.TransportRole.Server, "tcp-noack(\"127.0.0.1\", 9000)",
       TransportType.RawReliableNoAck)`.
     - `Case(Registration.TransportRole.Client,
       "tcp-noack(\"127.0.0.1\", 9000, disconnection_timeout = 10)",
       TransportType.RawReliableNoAck)`.
   - Register `RawReliableNoAckTcpBinding.CreateRegistration()` in `BuildFactory`.
   - Add a canonicalization assertion:
     `"TCP-NOACK(\"127.0.0.1\", 9000)"` -> `"tcp-noack(127.0.0.1, 9000)"`.
   - Add `TcpNoAckBinding_CompilesAndConnects` with `[Category("Integration")]`: add a local
     free-port helper (bind `IPAddress.Loopback, 0`, read `LocalEndpoint.Port`, stop), then
     compile `tcp-noack(\"127.0.0.1\", <port>)` for the server and client roles
     through `BuildFactory`, cast the results to `IRawReliableNoAckServer` and
     `IRawReliableNoAckClient`, `Init` them with small recording handlers/factory, `Start`, wait
     for both handlers to connect, send one message in each direction, assert delivery, and stop.
     This exercises the binding’s role branch and argument order, not just its `Type`.

**Verification**

Run:

```bash
dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj \
  --filter "TestCategory=Integration"
dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj \
  --filter "FullyQualifiedName~FactoryBindingsTests"
```

Expected: all selected tests pass.

**Definition of done**

`tcp-noack` constructs a working server/client pair, all end-to-end cases pass, and the binding
compiles, canonicalizes, and is registered in the test factory. Production registration is deferred
with TestUI/Api: this plan does not add a production composition root for the scheme.

**Risks / notes**

- This is the largest stage because it must include the server and client for a meaningful
  verification; keep the code a close copy of the Ack TCP server minus the handshake.
- The server must reject the socket on every failure path that does not hand it to an endpoint.
- The accept path must not retain `_handshakeSockets`; there is no asynchronous handshake.
- The `disconnectTimeout` must be positive and the tick period must be at least 1, as in Ack.

---

### Stage 10 — Final NoAck documentation, deferred items, and full validation

**Objective**

Close the documentation and coverage gaps, record the deferred Direct/conformance/deduplication
items, and validate the whole change.

**Inputs**

- Stages 1–9.

**Actions**

1. Update `Pontifex/Abstractions/Transports/Raw/Reliable/NoAck/Specification.md`:
   - Confirm it names `tcp-noack` as the only implemented carrier.
   - State that Direct NoAck is reserved but not implemented.
   - State that NoAck conformance controls and suite are deferred.
   - State that the T3 TCP endpoint duplication is intentional and that a future consolidation
     (T2a composition or T2b componentization) is planned.
2. Ensure `Tests/Pontifex.Tests/Transports/README.md` and
   `Tests/Pontifex.Tests/Endpoints/README.md` contain every new NoAck layer, directory, inheritance
   parent, and coverage path.
3. Ensure every new NoAck test layer has `Coverage.md` with covered tests and a deferred section
   listing Direct NoAck and NoAck conformance.
4. Search for stale statements and references:
   ```bash
   grep -rnI --exclude=plan.md --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git -E "IRawReliableNoAckClientSession|IRawReliableNoAckConformanceControl|RawReliableNoAckTransport|RawReliableNoAckConformanceControl" .
   grep -rnI --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git -E "does not invoke .*OnStopped|No OnStopped|no lifecycle callback" Pontifex/Abstractions/Transports/Raw/Reliable
   grep -rnI --exclude=plan.md --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git "direct-noack" .
   ```
   Expected: the first search finds nothing; the second finds only corrected wording or none; the
   third finds only documented reserved-name statements, no registration.
5. Run the full build and the complete solution test suite:
   ```bash
   dotnet build Pontifex.sln
   dotnet test Pontifex.sln
   ```
   If the solution run is impractical in the environment, run every test project individually:
   ```bash
   dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj
   dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj
   dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj
   dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj
   dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj
   dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj
   dotnet test Tests/Pontifex.TransportFormula.Tests/Pontifex.TransportFormula.Tests.csproj
   dotnet test Api/Pontifex.Api.Tests/Pontifex.Api.Tests.csproj
   ```
   Expected: build succeeds; all test runs pass, or any pre-existing failure is reported rather
   than modified.
6. Review the final working tree for unrelated changes. Only files named by this plan may be
   modified.

**Verification**

The commands in actions 4–5 produce the expected results, and the working tree contains only the
planned files.

**Definition of done**

The full build passes, the complete solution test suite passes (or any pre-existing failure is
reported), no legacy NoAck references remain, no Direct NoAck registration exists, and the
documentation/coverage records the deferred items.

**Risks / notes**

- If a pre-existing test unrelated to this plan fails, report it instead of modifying it.
- Do not commit during this stage; the executor handles commits per stage.

## Global definition of done

- The core/Ack `OnStopped` rule is implemented and tested for connected, never-connected,
  admission-failed, and Start-false paths.
- The Ack specification and conformance text agree with the implementation.
- The legacy NoAck abstraction is deleted and replaced by the new contracts, marker interfaces,
  transport bases, and endpoint bases.
- The `tcp-noack` binding exists and is registered in the test factory; it produces a working
  reliable NoAck TCP server/client pair. Production registration is deferred with TestUI/Api.
- No Direct NoAck carrier, NoAck conformance control/suite, or `Pontifex.Api` change exists.
- All new test layers follow `Tests/RULES.md`, including README layer tables and `Coverage.md`.
- The full build and the relevant test projects pass.
