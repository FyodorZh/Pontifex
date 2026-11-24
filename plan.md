# Fix all Direct transport audit findings

## Goal

After this plan is complete, every finding in `problems.md` is either fixed in
production code with a regression test, or resolved by an explicit
specification/documentation correction. The Direct RawUnreliable, RawReliable,
RRUnreliable, and RRReliable transports, the shared endpoint/transport bases
they use, the in-process Channel/ChannelManager, and the Netem delivery system
all satisfy their normative specifications. The default Direct test suite is
green and deterministic, and `report.md` records every change, the problem it
solves, and why it was made.

## Context

`problems.md` is a 46-item audit (6 High, 14 Medium, 26 Low) of
`Transports/Pontifex.Transport.Direct`, its shared implementation bases under
`Pontifex/Implementations`, and its tests. The current tests pass, but the
defects are code-level, race-level, and specification-level. The highest-impact
items are in shared code (`AnyTransport`, `RawEndpoint`, `RawUnreliableEndpoint`,
`RawUnreliableServerTransport`, `RawReliableEndpoint`,
`RawReliableClientTransport`, `RRReliableServerSession`, `Channel`,
`ChannelManager`) and in Netem timing. The Direct carriers inherit those
behaviors, so fixing them also affects the Net transports that share the bases.

The repository uses:

- `Pontifex/Abstractions/Transports/.../Specification.md` as normative
  contracts.
- `Transports/Pontifex.Transport.Direct/.../Specification.md` as the Direct
  implementation specifications.
- `Tests/RULES.md` as the normative test-suite structure and determinism rules.
- `AGENTS.md` as the project-wide build settings and specification rules.

## Constraints

1. Every `.csproj` keeps all four settings explicit: `Nullable=enable`,
   `LangVersion=9` for netstandard2.1 and `14` for net10.0,
   `ImplicitUsings=enable` only in tests and `Pontifex.TestUI`,
   `TreatWarningsAsErrors=true` only in libraries.
2. No new third-party packages and no new test framework. Use NUnit and the
   existing hand-written harness style. `Tests/RULES.md` HARNESS-09 forbids
   mocking libraries.
3. No public API removal. Additive optional parameters, additive protected
   virtual/protected seams, and internal seams are allowed. The explicitly
   approved additions are: RawReliable Direct binding gains `message_max_size`,
   `buffer_capacity_bytes`, and `handshake_timeout`; the public
   `IRawReliableHandshakeTimerFactory`; `handshake_max_byte_size` range becomes
   `1..int.MaxValue`; Direct `id` is protocol-scoped; the RawUnreliable Direct
   server reports reliable-debug mode as unsupported. New helper types
   (`MonotonicTime`, `DiscardingDeliverySystem`, `IDispatchQueue<T>`,
   `NetemConfig.Validate`, the new `SerializedCallbackQueue` constructor) are
   internal unless a step explicitly makes them public.
4. Every behavioral change updates the owning implementation specification in
   the same stage. A specification line must not exceed 100 characters outside
   table rows.
5. Every test change updates the owning `Coverage.md` in the same stage. Suite
   structure changes update the suite `README.md`.
6. The default Direct test run must not use `Thread.Sleep`, wall-clock polling,
   real timers, or uncontrolled background tasks where a seam exists.
   Irreducible Direct threaded/integration facets belong to a declared
   category. L-25's scope is the Direct test project; the core and conformance
   suites are not reworked for determinism in this plan.
7. Each stage leaves the repository buildable and the stage's tests green.
   Do not commit a stage that has not passed its verification.
8. Work only on the files named by the active stage, plus files strictly
   necessary to keep the solution compiling.
9. `problems.md` is the task input. Do not rewrite or delete it during
   implementation.
10. Write all new prose, comments, and specifications in English.

## Non-goals

- No redesign of the public transport abstractions beyond the approved
  decisions.
- No changes to TCP/UDP wire protocols except behavior inherited from the
  shared bases and the `AnyTransport` lifecycle rule.
- No new transport features, authentication, encryption, compression, or
  persistence.
- No unrelated refactoring, formatting sweeps, or dependency upgrades.
- No edits to `problems.md` content.
- No implementation of the already-completed Direct transport features beyond
  the audit findings.

## Assumptions

1. The recommendations from the planning conversation are approved: server
   reliable-debug returns `false`; `id` is protocol-scoped; the RawReliable
   binding exposes the three parameters; test seams plus integration categories
   are acceptable; delivery systems are created lazily at start.
2. `Channel` owns installed delivery systems; a client endpoint installs both
   directions, and a Direct server endpoint never installs one. Evidence:
   `RawUnreliableDirectClientEndpoint.Connect`, `Channel.SetClientDeliverySystem`,
   and `Channel.SetServerDeliverySystem`.
3. `IEndPoint` equality and hash code are the route key. Evidence:
   `ChannelManager` and `RawUnreliableServerTransport._routes`.
4. The test projects can access internal production types through the existing
   `InternalsVisibleTo` setup; do not add reflection.
5. .NET SDK 10.0.102 is installed. Evidence: `dotnet --version`.
6. The Direct test project is the default carrier suite; core base behavior is
   also covered by `Tests/Pontifex.Tests` and conformance by
   `Tests/Pontifex.Transports.Tests`.
7. For the new `UnreliableDeliveryBinding.CreateFactory`,
   `ParseNetemConfig` returns a plain `NetemConfig` that can be captured after
   the binding context has been released; the new factory must not capture the
   mutable `ITransportStructValue`. The existing RR binding's factory capture
   is out of scope and is not changed by this plan.

## Prerequisites

- Clean working tree at the start of each stage.
- `problems.md` committed as the task input.
- `dotnet` 10.0.102 available.
- The Direct, core, Net, and conformance test projects restore successfully.
- Read `Tests/RULES.md` before changing any suite.
- Read the relevant `Specification.md` before changing a transport behavior.

## Stage map

| Stage | Outcome | Why it is a separate review checkpoint |
| --- | --- | --- |
| 1 | Shared lifecycle, handler fault isolation, and send-result precedence are contract-compliant. | It changes behavior for every transport, so it must be reviewed and regression-tested before any carrier-specific work. |
| 2 | RawUnreliable routing, stop, size, and conformance-gate semantics are contract-compliant. | It is the shared unreliable server/endpoint boundary and can be verified independently of Direct send ownership. |
| 3 | RawUnreliable Direct outbound admission honors the send-return ordering and message budget. | It is a distinct admission-path outcome with its own race tests. |
| 4 | RawUnreliable Direct delivery systems and debug mode have deterministic ownership. | It is an ownership/lifecycle refactor that must not be mixed with admission races. |
| 5 | RawReliable endpoint framing and connection notification are contract-compliant. | It is a distinct framing/lifecycle outcome shared by both Direct roles. |
| 6 | RawReliable Direct handshake deadlines, size checks, and binding parameters are contract-compliant. | It is the handshake/configuration boundary and needs timer/manual-time verification. |
| 7 | Netem timing, configuration, queueing, and lifetime are correct and testable. | It is a self-contained virtual-delivery outcome with its own clock seam and tests. |
| 8 | Channel and registry teardown, delivery-system ownership, and protocol-scoped ids are correct. | It is the carrier-plumbing boundary used by all four Direct protocols. |
| 9 | RRReliable session-state locking and response/TTL state machines are correct. | It is a distinct reliable-request concurrency outcome. |
| 10 | RRUnreliable Direct stop and pre-start capacity behavior are correct. | It is a distinct unreliable-request outcome. |
| 11 | All remaining specification and suite documentation defects are corrected. | It is a documentation-only checkpoint after the behavior is settled. |
| 12 | The Direct tests are deterministic and use explicit scheduling seams/categories. | It is a cross-cutting test-hygiene outcome that depends on all production seams being in place. |
| 13 | The full verification passes and `report.md` records every change. | It is the final review checkpoint and the only place the complete change record is assembled. |

### Finding traceability

Every audit finding has one owning stage. The stage's tests and the final
`report.md` row are the evidence.

| Finding | Stage | Primary evidence |
| --- | --- | --- |
| H-01 | 7 | `MonotonicTime` overflow test; Netem timing tests |
| H-02 | 2 | Server-stop reason-identity test |
| H-03 | 9 | Concurrent session-map stress test |
| H-04 | 1 | Throwing `OnReceived`/`OnConnected` tests |
| H-05 | 1 | `Init` contract tests |
| H-06 | 1 | Failed-start callback tests |
| M-01 | 2 | Oversized server inbound tests |
| M-02 | 3 | Send-return barrier test |
| M-03 | 2 | Stop-before-flush tests |
| M-04 | 5 | Malformed-frame drop tests |
| M-05 | 6 | Manual handshake-timeout tests |
| M-06 | 5 | Payload boundary tests |
| M-07 | 2 | Declined-endpoint handler-notification tests |
| M-08 | 8 | Throwing delivery-system teardown test |
| M-09 | 8 | Send/dispose leak test |
| M-10 | 4 | Lazy-construction/pre-start-stop tests |
| M-11 | 9 | Finish/Reject state tests |
| M-12 | 9 | Timer-factory throw tests |
| M-13 | 4 | Connect/install exception tests |
| M-14 | 1 | Throwing stop-subscriber test |
| L-01 | 3 | 100-message admission-budget test |
| L-02 | 1 | Combined-condition precedence tests |
| L-03 | 4 | Debug-mode race and server-false tests |
| L-04 | 2 | Conformance-gate hit/not-hit tests |
| L-05 | 6 | Handshake size tests |
| L-06 | 5 | Pre-`OnConnected` payload test |
| L-07 | 6 | Binding range/relationship tests |
| L-08 | 4 | Lazy-factory construction test; double-build path removed |
| L-09 | 4 | Endpoint-construction failure ownership test |
| L-10 | 10 | Stop-race/unbound-handler test |
| L-11 | 10 | Configured pre-start capacity test |
| L-12 | 11 | Spec wording review |
| L-13 | 11 | Spec claim review |
| L-14 | 11 | Spec claim review |
| L-15 | 11 | Completion-row review |
| L-16 | 11 | Line-length check |
| L-17 | 7 | Negative-config schema/validation tests |
| L-18 | 7 | Duplication queue-limit test |
| L-19 | 7 | Repeated/concurrent release tests |
| L-20 | 8 | Throwing server-callback cleanup test |
| L-21 | 8 | Shared-system release test |
| L-22 | 8 | Cross-protocol isolation test |
| L-23 | 11 | `help.md` example review |
| L-24 | 11 | Suite README review |
| L-25 | 12 | Default-suite guard and repeated runs |
| L-26 | 7 | Deterministic drop test |

## Stage 1 — Shared lifecycle, handler isolation, and send-result precedence

**Objective**

Make `AnyTransport`, `RawEndpoint`, `RawReliableEndpoint`, and the shared
send-result helpers obey the abstract lifecycle, fault-isolation, and result
precedence rules.

**Inputs**

- Clean tree. Before editing, run the prerequisite baseline commands and keep
  their output for the Stage 1 report.
- Read `Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md` §7.1,
  §7.2, §7.3, §9, and §11.
- Read `Pontifex/Abstractions/Transports/Raw/Unreliable/Specification.md` §9
  and §11.

**Actions**

1. `Pontifex/Implementations/AnyTransport.cs`:
   - Add a private `_startSucceeded` flag. Do not add a separate
     `_startInProgress` field; it has no reader and would be CS0414 under
     `TreatWarningsAsErrors`.
   - Set `_startSucceeded` to `true` only after `TryStart()` returns `true`,
     before `OnStarted()`.
   - State the internal teardown rule explicitly:
     - a plain `TryStart()` returning `false` produces zero internal
       `OnStopped`; a carrier that acquired resources must release them or
       raise `Fail`/`Stop` before returning false;
     - a `Fail` or `Stop` raised inside `TryStart` produces exactly one
       internal `OnStopped` (the path that releases carrier resources);
     - a successful start followed by `Stop`/`Fail` produces exactly one
       internal `OnStopped`.
   - In the `TryStart()`-returned-false branch, clear `_started` and
     `_onStopped` before calling `Fail("Start", ...)`, so a plain false does
     not receive a spurious internal `OnStopped`.
   - Update the `OnStopped` XML: it is no longer “called only after a
     successful TryStart”; it is called for a start attempt that reached
     `Fail`/`Stop`, and for normal post-start stops.
   - In the branch where `TryStart()` returned true but `_started` became false
     because a stop won inside the attempt, clear `_onStopped` and invalidate
     the transport (call `Fail` or set `_isValid = false`) without invoking the
     user callback. Do not leave `IsValid == true` for that initial attempt.
   - Invoke the user `_onStopped` callback only when `_startSucceeded` is true.
     An **initial** start attempt that returns false invalidates the transport
     and invokes no user callback, whether `TryStart` returned false or a stop
     won inside it. A later `Start` that returns false because the transport is
     already started, already stopped, or invalid does not change validity and
     invokes no hooks.
   - Hit `BeforeStoppedCallbackGate` only around the user `_onStopped` call,
     never around the internal `OnStopped` teardown.
   - Keep the `FailNextStart` path unchanged: it fails before `_started` is
     set, so it has no internal or user terminal callback. The conformance
     `FailNextStart_InvalidatesWithoutStoppedNotification` test must remain
     green.
   - The existing `Start_WhenTryStartReturnsFalse_InvalidatesTransportWithoutNotifications`
     and `Start_AfterFailedStart_ReturnsFalseWithoutHooks` tests keep
     `OnStoppedCalls == 0`; only the stop-during-`TryStart` test gains one
     internal teardown and zero user callbacks.
2. `Pontifex/Implementations/Endpoints/Raw/RawEndpoint.cs`:
   - In `_receiveAction`, catch an `OnReceived` exception, log it, and return
     without calling `FailHard` or setting `_stopReason`.
   - In `RaiseStopped`, iterate `Stopped.GetInvocationList()` and invoke each
     subscriber inside its own `try`/`catch`.
   - In `ScheduleToSend`, check endpoint state through `CanSendNow` before the
     null/alive, size, and capacity checks. `CanSendNow` does not dereference
     the buffer. A null buffer on a stopped endpoint then returns the state
     result, while a null buffer on a running endpoint returns
     `InvalidMessage`. Preserve the ownership rule on every path.
   - Add a protected `IsStopRequested` property that reports
     `_stopReason != null`. Stage 2 uses it; do not add behavior that consumes
     it yet.
3. `Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableEndpoint.cs`:
   - Change `NotifyConnectedCore` so an `OnConnected` exception is logged and
     the method returns `true`; do not call `Stop`.
   - Update the XML remarks to state that handler failures are isolated.
   - In `Send`, check `CanSendNow` and release a non-null buffer before the
     null/alive and payload-size checks. This is the state-first precedence
     required by §9.
4. `Pontifex/Implementations/Endpoints/Raw/Unreliable/RawUnreliableServerEndpoint.cs`:
   - In `Send`, check `CanSendNow` before the null check and the shared budget
     check. On a state rejection, release a non-null buffer and return the
     mapped result. On a null buffer with a running endpoint, return
     `InvalidMessage`.
   - Keep the existing oversized-message path delegated to the base.
5. `Pontifex/Implementations/RR/Reliable/RRReliableTransport.cs` and
   `Pontifex/Implementations/RR/Unreliable/RRUnreliableTransport.cs`:
   - In `TryValidatePayload`, check `IsStarted` before the null check so a
     stopped transport reports `NotConnected` rather than `InvalidMessage`.
     Do not release null. Keep serializability and size checks after validity.
6. `Pontifex/Implementations/Raw/Reliable/RawReliableClientTransport.cs`:
   - Throw `ArgumentNullException` from `Init` for a null handler before taking
     any state-changing action.
   - Return `false` without calling `Fail` for a second `Init`, an `Init` after
     a start attempt, or an `Init` after a normal stop.
   - Call `Fail` only when a valid non-null `Init` cannot bind its handler
     (`SetupHandler` returns null).
   - Update the XML remarks to match §7.1 and §7.2.
7. Tests:
   - Update `Tests/Pontifex.Tests/Transports/AnyTransportTests.cs`. In
     `Start_WhenTryStartStopsTransport_ReturnsFalseWithoutOnStarted`, assert
     internal teardown still ran, the user callback count is zero, the event
     order contains no user `stopped`, and `IsValid` is false.
   - Update
     `Tests/Pontifex.Tests/Transports/Raw/Reliable/Client/RawReliableClientTransportTests.cs`:
     `Init(null)` throws; a second `Init` and `Init` after stop return false
     with `IsValid` unchanged; failed `Start` paths deliver no `onStopped` but
     still destroy the endpoint/carrier.
   - Update `Tests/Pontifex.Tests/Endpoints/RawEndpointTests.cs`:
     `Send_Null_ReturnsInvalidMessage` and
     `Send_TooBig_ReturnsMessageTooBigAndReleasesBuffer` must start the
     endpoint before asserting the validity/size result, or assert
     `NotConnected` for the stopped endpoint. Add the combined-condition
     cases from action 7 here.
   - Update `Tests/Pontifex.Tests/Endpoints/RawReliable/RawReliableEndpointTests.cs`
     and the server/client handshake endpoint fixtures that assert a throwing
     handler stops with `ExceptionFail`:
     `Endpoints/RawReliable/Server/RawReliableServerEndpointTests.cs`,
     `Endpoints/RawReliable/Server/RawReliableHandshakeServerEndpoint/RawReliableHandshakeServerEndpointTests.cs`,
     `Endpoints/RawReliable/Client/RawReliableHandshakeClientEndpoint/RawReliableHandshakeClientEndpointTests.cs`.
   - Update `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport/RRReliableTransportTests.cs`
     where a non-started transport with a null payload now returns the state
     result, not `InvalidMessage`, and the corresponding Coverage rows.
   - Update `Tests/Pontifex.Tests/Endpoints/Coverage.md` so the throw-fails-hard
     entry is no longer planned/expected.
   - Update `Tests/Pontifex.Tests/Transports/AnyTransportTests.cs`:
     `Start_WhenTryStartReturnsFalse_InvalidatesTransportWithoutNotifications`
     and `Start_AfterFailedStart_ReturnsFalseWithoutHooks` keep zero terminal
     callbacks; the stop-during-`TryStart` test records one internal teardown,
     zero user callbacks, and `IsValid == false`. `Start_CalledTwice` and
     `Start_AfterStop` keep their existing validity and no-hook expectations.
     In the `AnyTransport` test double, record internal `OnStopped` and user
     `_onStopped` separately; do not use one event list that cannot distinguish
     them.
   - Add L-02 combined-condition tests: stopped + oversized returns the state
     result; stopped + full budget returns the state result; stopped + null
     returns the state result; running + null returns `InvalidMessage`;
     running + oversized returns `MessageTooBig`.
   - Add tests for: failed initial `Start` with no user `onStopped`; a failure
     after a successful start still calls `onStopped`; `Init(null)` throws and
     does not invalidate; a second `Init` and `Init` after stop return false
     while `IsValid` stays true; a throwing `OnReceived` and `OnConnected` leave
     the endpoint usable; a throwing first `Stopped` subscriber does not skip
     the second.
   - Update the owning `Coverage.md` files where a documented suite exists.
     Loose test files follow `Tests/RULES.md` RULES-03 and need no suite
     documentation.

**Verification**

- `dotnet build Pontifex.sln`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj`
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj`
- Expected: build succeeds, all tests pass, and the changed fixtures report the
  new lifecycle/precedence assertions.

**Definition of done**

A failed initial start is invalid and invokes no user `onStopped`. A plain
`TryStart` false has zero internal teardown; a `Fail`/`Stop` raised inside
`TryStart` has exactly one internal teardown. A second or post-stop `Init`
returns false without changing validity; `Init(null)` throws without changing
state; a valid `Init` that cannot bind its handler invalidates. A second or
post-stop `Start` returns false without changing validity. Post-start failures
invoke `onStopped`; handler exceptions no longer stop endpoints;
per-subscriber stop notification isolation is proven; and send-result
precedence is state-first in all four shared paths.

**Risks / notes**

`AnyTransport.Fail` is used by many carriers. Keep `OnStopped` for internal
teardown and gate only `_onStopped`. Confirm that a failure raised after a
successful start still visits the user callback. Do not alter the
`FailNextStart` path.

## Stage 2 — RawUnreliable routing, stop, size, and conformance gates

**Objective**

Make the shared unreliable server/endpoint honor the supplied stop reason,
reject oversized inbound data before routing, suppress post-stop delivery, and
hit the documented conformance gates.

**Inputs**

- Stage 1 complete.
- Read `Pontifex/Abstractions/Transports/Raw/Unreliable/Specification.md` §6.2,
  §7.2, §7.4, §8, §11, and §14.

**Actions**

1. `Pontifex/Implementations/Raw/Unreliable/RawUnreliableServerTransport.cs`:
   - In `OnStopped`, after building the route snapshot and before
     `StopCarrier(reason)`, unsubscribe each entry and call
     `StopRouteEndpoint(entry.Endpoint, reason)` so the supplied reason latches
     synchronously. Then call `StopCarrier(reason)`. The later channel-disposal
     notification must become a one-shot no-op.
   - In `OnCarrierInbound`, before the existing-route lookup and before
     `BeforeHandlerFactoryGate`, check `trigger.GetDataSize()` against
     `MessageMaxByteSize`; release and log an oversized message and return.
   - When a factory returns a non-null handler but `CreateEndpoint` returns
     null or throws, capture the handler and a
     `TextFail`/`ExceptionFail` reason in locals inside the admission block,
     leave `lock (_admissionLock)`, then call `handler.OnStopped(reason)`
     exactly once, isolate/log any handler exception, and release the trigger.
     Do not call application code under `_admissionLock`; a handler may call
     `transport.Stop()`, which takes the transport lock. Do not publish a
     route.
2. `Pontifex/Implementations/Endpoints/Raw/Unreliable/RawUnreliableEndpoint.cs`:
   - In `FlushInbox`, return before `Handler.OnStarted` when `IsStopRequested`.
   - After `Handler.OnStarted` and `MarkStartCompleted`, return before opening
     the inbox or draining when `IsStopRequested`.
   - Re-check `IsStopRequested` for every dequeued message inside the drain;
     release the remaining buffered messages and stop draining as soon as a
     stop is latched. A stop can be requested after the second check but before
     or during the drain, and `Stop` does not take `_inboxLock`.
   - Keep the inbox in `Buffering` or close it consistently; the `Stopped`
     subscription releases anything not drained.
   - Move the `BeforeHandlerStoppedGate.Hit()` call out of `BeginStop` and into
     the `HandlerGate.OnStopped` path, after the `_startCompleted` check, so it
     hits only when `OnStopped` is about to be forwarded. `HandlerGate` is
     constructed before `_conformance`; pass the conformance control (or a
     callback that hits the gate) into its constructor after creating the
     control, or assign it immediately after construction. Update the remarks.
3. `Transports/Pontifex.Transport.Direct/Raw/Unreliable/RawUnreliableDirectClientEndpoint.cs`:
   - In `CommitSend`, check `_channel == null || channel.IsDisposed` and release
     before `BeforeSendCommitGate.Hit()`, matching the server endpoint. Do not
     hit the gate for a discarded message.
4. Tests:
   - Add to `Tests/Pontifex.Transport.Direct.Tests/Raw/` a test proving a
     server stop with an active handler delivers the exact `StopReason`
     instance to both the handler and `onStopped`.
   - Add oversized inbound tests for a new route and an existing route; assert
     the factory is not called, the handler is not called, the message is
     released, and the endpoint/transport stay alive.
   - Add a test that a factory-created handler is notified exactly once when
     endpoint creation returns null and when it throws. The declined handler
     calls `transport.Stop()` and the test asserts the call returns without
     deadlocking. Add an internal server endpoint-factory hook so the throw
     case is deterministic.
   - Add a test that `Stop` from `OnStarted`, a stop latched before the flush
     action, and a stop requested during the drain cannot deliver buffered
     messages afterwards. Use a gated handler or a large buffered batch to hit
     the mid-drain window.
   - Add gate tests for `BeforeHandlerStoppedGate` on hard failure and not on a
     throwing `OnStarted`, and for the client commit gate on a disposed
     channel.
   - Update `Tests/Pontifex.Tests/Endpoints/RawUnreliable/RawUnreliableEndpointTests.cs`
     where moving the gate changes the expected hit count.
   - Update `Tests/Pontifex.Transports.Tests/Raw/Unreliable` conformance
     expectations. Update `Coverage.md` only for documented suites; the loose
     `Tests/Pontifex.Transport.Direct.Tests/Raw/` files need none.
5. Update `Transports/Pontifex.Transport.Direct/Raw/Unreliable/Specification.md`
   §6, §7, and §8 for the corrected stop ordering, inbound size check, gate
   behavior, and the corrected `onStopped` scheduling wording.
6. Update the abstract
   `Pontifex/Abstractions/Transports/Raw/Unreliable/Specification.md` §6.2,
   §7.4, and §7.6 to define the factory-created-handler / declined-endpoint
   terminal notification, including the callback-matrix row “factory returns a
   handler but endpoint creation fails”. Do not declare a Direct-only deviation
   for this shared rule.
   Cover it with the controllable Direct test that captures `ServerHandler` and
   invokes it after channel disposal; do not add a UDP conformance expectation,
   because the UDP carrier has no deterministic declined-endpoint path.

**Verification**

- `dotnet build Pontifex.sln`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj`
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj`
- Expected: all pass; the new reason/size/stop/gate tests pass; the Direct
  RawUnreliable conformance fixture remains green.

**Definition of done**

Active server handlers observe the supplied stop reason; oversized server
inbound data never reaches a factory or handler; a factory-created handler
whose endpoint is declined receives exactly one `OnStopped` outside the
admission lock; post-stop flush delivery is impossible; the gate table matches
the implementation.

**Risks / notes**

Stopping endpoints before the carrier is safe only because `_stopping` is
published under `_admissionLock` before the snapshot. Keep that ordering.
`MessageMaxByteSize` is the transport-level fixed limit for Direct, but the
check belongs in the base so every carrier gets it.

## Stage 3 — RawUnreliable Direct outbound admission

**Objective**

Guarantee that a callback for a message accepted by `Send` cannot begin before
that `Send` returns, and enforce the declared message-count budget at admission.

**Inputs**

- Stages 1–2 complete.
- Read `Pontifex/Abstractions/Transports/Raw/Unreliable/Specification.md` §9,
  §10, and §11.

**Actions**

1. Add `Pontifex/Implementations/Raw/Unreliable/SendReturnBarrier.cs`:
   - A monitor-based counter with `Enter`, `Exit`, and `Wait`.
   - A per-instance reentrancy depth (for example a `ThreadLocal<int>` field on
     the barrier, not a process-wide `[ThreadStatic]`), so a nested send on a
     second endpoint of the same thread cannot bypass that second barrier.
   - `Wait` blocks until no other send is in flight; it is a no-op when there
     are none.
2. `Pontifex/Implementations/Endpoints/Raw/Unreliable/RawUnreliableEndpoint.cs`:
   - Own a `SendReturnBarrier`.
   - Override `Send` to enter the barrier, call `base.Send`, and exit in a
     `finally`.
   - Expose a protected `WaitForSendReturn()` that the Direct commit workers
     call before delivering.
   - Add an internal `Action? BeforeSendReturnsForTests` hook invoked in the
     `finally` before the barrier exits. A test can block there to keep the
     sender inside `Send`.
   - Add an internal `Action? BeforeSendReturnWaitForTests` hook invoked by
     `WaitForSendReturn()` on the commit worker immediately before it waits.
     The test waits for this worker-side signal, then asserts no peer callback
     has begun, releases `BeforeSendReturnsForTests`, and asserts delivery.
     This makes the test fail if the barrier is missing; a sender-only hook
     cannot distinguish “worker not scheduled” from “barrier held”.
3. `Pontifex/Implementations/Endpoints/Raw/Unreliable/RawUnreliableServerEndpoint.cs`:
   - Wrap its `Send` override with the same barrier (the thread-static depth
     makes the nested base call safe).
4. `Pontifex/Implementations/Endpoints/Raw/RawEndpoint.cs`:
   - Add an internal `SendSlotReservation` token with an idempotent
     `ReleaseOnce()` (`Interlocked.Exchange` on a released flag) and a shared
     no-op token for the default path.
   - Add `protected virtual SendSlotReservation? TryReserveSendSlot()`. The
     default returns the no-op token; null means the budget is exhausted.
   - In `ScheduleToSend`, after the state/validity/size checks and before the
     byte reservation, call `TryReserveSendSlot()`. If it returns null, release
     the buffer and return `BufferOverflow`.
   - Change the internal `_sendAction` argument from `UnionDataList` to a
     `SendWorkItem` carrying the message and its reservation token. On byte
     rollback, schedule failure, or the pre-start drop, call
     `reservation.ReleaseOnce()` exactly once.
   - Add `protected virtual void DoSendRaw(UnionDataList message,
     SendSlotReservation reservation)`; the default calls the existing
     `DoSendRaw(message)` and releases the reservation in `finally`. Existing
     carriers keep their current abstract override.
5. `Transports/Pontifex.Transport.Direct/Raw/Unreliable/RawUnreliableDirectClientEndpoint.cs`
   and `RawUnreliableDirectServerEndpoint.cs`:
   - Add an `int _outstandingCommitMessages` counter and override
     `TryReserveSendSlot` with `Interlocked` operations and the endpoint's
     `queueCapacity`. It increments and returns a token; it rolls back and
     returns null when the new value exceeds `queueCapacity`.
   - Override the token-taking `DoSendRaw`: post a `CommitItem` holding the
     message and token to the commit queue. If the post is rejected or the
     channel is gone, release the token and the buffer.
   - `CommitSend` and the queue disposer both call `token.ReleaseOnce()`; the
     idempotent token makes the race safe.
   - Expose an internal `OutstandingCommitMessagesForTests` getter so the
     server test can observe the counter instead of being shadowed by the
     shared `SendBudget`.
6. `Transports/Pontifex.Transport.Direct/Raw/Unreliable/RawUnreliableDirectClientEndpoint.cs`
   and `RawUnreliableDirectServerEndpoint.cs`:
   - In `CommitSend`, call `WaitForSendReturn()` immediately before
     `SendToServer`/`SendToClient`, outside any lock.
7. Tests:
   - Unit-test the barrier and the reservation/release accounting in
     `Tests/Pontifex.Tests`.
   - Add a Direct test that blocks the commit worker with 100 accepted
     messages, then asserts the 101st `Send` returns `BufferOverflow` and the
     buffer is released. Add a new shared
     `Tests/Pontifex.Transport.Direct.Tests/Raw/TestSupport/BlockingDeliverySystem.cs`
     with a `ManualResetEventSlim` gate and `Release()`; no existing double can
     stall `Deliver`. Unblocking the worker must eventually release all 100
     reservations.
   - Add an empty-message budget test: empty payloads count toward the same
     100-message admission bound.
   - Add a post-then-throw test: the Direct `DoSendRaw` posts successfully and
     then throws; assert the token is released exactly once (the commit queue
     or the exception path, not both) and the counter never goes negative.
   - Add a send-return test using both internal hooks: hold the sender inside
     `Send`, wait for the commit worker at `BeforeSendReturnWaitForTests`,
     assert the peer callback has not started, release the sender, and assert
     the callback then starts.
   - Add the same send-return test for a Direct server endpoint. The commit-slot
     hook applies to both endpoints, and both use `queueCapacity = 100`; add a
     server test that blocks its commit worker with 100 messages, asserts the
     101st `Send` returns `BufferOverflow` and releases the buffer, and then
     unblocks and asserts all 100 reservations are released. Do not rely on the
     shared `SendBudget`, whose deltas are emitted at post time, not delivery.
   - Update `Coverage.md` only where a documented suite exists.
8. Update `Transports/Pontifex.Transport.Direct/Raw/Unreliable/Specification.md`
   §6 and §8 for the 100-message admission window and the commit-queue budget.

**Verification**

- `dotnet build Pontifex.sln`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj`
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj`
- Expected: all pass; the new barrier and budget tests pass.

**Definition of done**

A Direct RawUnreliable send cannot be delivered before its `Send` returns, and
the client rejects a 101st pending message with `BufferOverflow` while counting
empty messages.

**Risks / notes**

Do not hold the barrier while running `Channel.Deliver` on the caller thread;
only the commit worker waits. A handler that calls `Send` synchronously from a
callback must not deadlock; the thread-static depth handles that case.

## Stage 4 — RawUnreliable Direct delivery-system ownership and debug mode

**Objective**

Construct delivery systems only when a client endpoint is created, make
hand-off/release exactly-once, isolate connect/install failures, and make the
server reliable-debug answer truthful.

**Inputs**

- Stages 1–3 complete.
- Read `Transports/Pontifex.Transport.Direct/Raw/Unreliable/Specification.md`
  §5, §6, §7, and §8.

**Actions**

1. `Transports/Pontifex.Transport.Direct/Raw/Unreliable/UnreliableDeliveryBinding.cs`:
   - Add `CreateFactory(ITransportStructValue value, IMemoryRental memory)` that
     validates/parses eagerly and returns `Func<IDeliverySystem>` which
     constructs a fresh system on each call. Capture only the parsed immutable
     config, never the `ITransportStructValue`.
   - Keep `Build` for callers that need an immediate instance.
   - Ensure a second factory call constructs a second independent system.
2. `Transports/Pontifex.Transport.Direct/Raw/Unreliable/RawUnreliableDirectBinding.cs`:
   - Stop calling `Build` twice during construction. Call `CreateFactory`
     twice and pass the factories to the transport through
     `SetDeliverySystemFactory`.
   - Split `Create` into an internal overload that accepts a factory-builder
     delegate (`Func<ITransportStructValue, IMemoryRental, Func<IDeliverySystem>>`),
     with the public `Create` passing the real `CreateFactory`. The internal
     overload lets a binding-level test count or fail factory construction.
3. `Transports/Pontifex.Transport.Direct/Raw/Unreliable/RawUnreliableDirectClientTransport.cs`:
   - Remove the eager `new PerfectDeliverySystem()` field initializers.
     Replace both stored instances with client/server factory delegates that
     return a fresh `PerfectDeliverySystem` for the default configuration.
   - Add an internal `SetDeliverySystemFactory(Func<IDeliverySystem>
     clientFactory, Func<IDeliverySystem> serverFactory)` used by the binding.
     The public `SetDeliverySystem(IDeliverySystem, IDeliverySystem)` wraps
     explicit instances in single-use factories after taking ownership.
   - A second pre-start `SetDeliverySystem` releases the previously configured
     explicit systems exactly once, deduplicating the same instance supplied
     for both directions, then stores the new pair.
   - `TryMakeReliableForDebug` releases any explicit systems that debug mode
     would otherwise never materialize, resets both factories to perfect
     systems, and then sets the debug flag under `_locker`. A configured
     system must not survive into a mode that ignores it.
   - `StopCarrier` releases only materialized systems that were never handed to
     an endpoint; after the debug-mode reset there is nothing stale to release.
   - Introduce an internal per-direction holder (factory + optional
     materialized instance + `ResetToPerfect()`), and pass the two holders or
     their reset delegates to `RawUnreliableDirectClientEndpoint`. The endpoint
     cannot mutate transport fields directly; it asks the holder to reset. A
     failed install resets that direction to a fresh perfect system; a
     configured netem direction is downgraded after a transient install
     failure, and the spec documents that. A netem-factory test must cover the
     post-failure instance and prove it is not the released one.
   - `CreateEndpoint` materializes the two systems during start. A client
     transport creates exactly one endpoint per start and `Start` is one-time,
     so explicit systems are consumed once and no second-endpoint policy is
     needed. If endpoint construction throws, release the systems, reset both
     factories to fresh perfect-system factories, and rethrow so a retry cannot
     use a released instance and `StopCarrier` cannot release it twice.
   - Remove `_handedOffClientDeliverySystem`/`_handedOffServerDeliverySystem`
     or replace them with exact per-direction hand-off state. `StopCarrier`
     releases only systems that were never handed to an endpoint.
   - Override a new `AnyTransport.OnStopBeforeStart` hook. Add it in
     `AnyTransport.Stop` only when `_isValid && !_started && !_hasStarted`
     (a true pre-start stop), not for a second `Stop` after a successful
     start. It releases the explicit systems owned through `SetDeliverySystem`
     and resets both fields to fresh perfect-system factories, so a later
     `Start` is still legal and never installs a released system. A second
     `Stop` after a normal start/stop must not release anything again.
   - Add `protected virtual void OnStartFailed()` called by `AnyTransport` when
     the initial `Start` returns false or is invalidated, including “Start
     before Init”. `RawUnreliableDirectClientTransport` overrides it to release
     owned explicit systems and reset to perfect factories. A failed start must
     not leak systems that were configured before `Init`.
   - In `TryMakeReliableForDebug`, hold `_locker` for the entire transition:
     the `HasStartBeenAttempted` check, the release/reset of any explicit
     systems, and the flag write. `SetDeliverySystem` and its
     “debug already selected” check take the same lock for the check-and-swap,
     so no `Start` can materialize a system between release and flag write.
     In reliable-debug mode, create perfect systems at endpoint creation and
     use an unbounded commit queue so the debug promise cannot be defeated by
     a full bounded queue.
   - Pass an `unboundedCommitQueue` flag to `RawUnreliableDirectClientEndpoint`
     and `RawUnreliableDirectServerEndpoint`; the endpoints choose the
     unbounded `SerializedCallbackQueue` constructor when it is set.
   - Add an internal `Func<...>? EndpointFactoryForTests` hook used only by
     `CreateEndpoint`, so a test can force endpoint construction to throw and
     verify system release. It must default to the real constructor and must
     not change production behavior.
   - Add an internal test-only delivery-factory setter that counts invocations,
     so the “zero systems constructed before start” assertion covers both the
     default and netem configurations.
   - Add an internal `BeforeStartLockForTests` hook in `AnyTransport.Start`,
     invoked before the lifecycle lock. The L-03 race test pauses there, calls
     `TryMakeReliableForDebug`, then releases `Start`; debug mode succeeds and
     every endpoint uses perfect systems. A second sequential test calls
     `Start` first and then `TryMakeReliableForDebug`, which must return false.
     Because `Start` holds `_locker` for the whole transition, there is no
     interleaving that can produce a torn state.
4. `Pontifex/Utils/SerializedCallbackQueue.cs`:
   - Add a second constructor that does not pre-size a `LimitedConcurrentQueue`.
     Use `SystemConcurrentQueue<T>` (the existing Actuarius type used by
     `ReconnectableBaseLogic`) so the queue is genuinely unbounded.
   - Keep the bounded constructor for normal transports. Do not pass
     `int.MaxValue` to `LimitedConcurrentQueue`, which allocates an array of
     that size.
5. `Transports/Pontifex.Transport.Direct/Raw/Unreliable/RawUnreliableDirectServerTransport.cs`:
   - `TryMakeReliableForDebug()` returns `false`; the server has no delivery
     systems and cannot enforce reliability.
6. `Transports/Pontifex.Transport.Direct/Raw/Unreliable/RawUnreliableDirectClientEndpoint.cs`:
   - Track client and server system hand-off separately.
   - Wrap the whole `Connect` sequence (manager connect, handler assignment,
     both installs, subscription, disposed check) in `try`/`catch`.
   - On failure: release each system not taken by the channel exactly once,
     dispose any obtained channel, ask both holders to `ResetToPerfect()` (so
     the endpoint's next `DoSendRaw` retry does not reinstall a released
     system), log, and return null without failing the endpoint. The current
     outbound buffer remains owned by `DoSendRaw`, which already releases it
     when `Connect` returns null; do not release it again inside `Connect`.
   - A manager callback exception and a delivery-system install exception must
     not call `FailHard`.
   - If one install succeeds and the second throws, the channel owns the first;
     dispose the channel and release only the uninstalled system.
7. Tests:
   - Add ownership tests with a construction-counting delivery factory:
     construct with the default configuration and with `delivery = netem`,
     `Stop` before `Start`, and assert zero systems were constructed by the
     transport.
   - Add a separate `SetDeliverySystem(netem, netem)` -> `Stop` -> `Start` test
     proving the explicit systems were released, both fields were replaced with
     fresh perfect systems, and the later start works.
   - Add a binding-level test through the internal `Create` overload with a
     counting factory builder: construct a client with `delivery = netem`,
     `Stop` before `Start`, and assert zero systems were constructed. Make the
     builder throw on its first and second call in separate cases and assert
     no construction happened before start. This is the M-10/L-08 evidence:
     the double-build path is removed, not merely moved.
   - Add the L-03 tests: (a) pause at `BeforeStartLockForTests`, enable debug,
     release Start, and assert every endpoint uses perfect systems; (b) start
     first, then call `TryMakeReliableForDebug` and assert `false`. Do not claim
     an interleaving that `_locker` makes impossible.
   - Add endpoint-construction-throw tests via `EndpointFactoryForTests`
     proving both systems are released exactly once.
   - Add an unbounded-queue test that constructs the new
     `SerializedCallbackQueue` unbounded path and posts many items (for example
     10,000) without rejection; assert the bounded constructor rejects at its
     capacity. Do not assert allocation internals; the behavioural property is
     “unbounded path accepts beyond any fixed capacity”.
   - Add manager-callback-throw and install-throw tests proving the endpoint
     survives, the outbound buffer is released exactly once, the transport does
     not stop, and a subsequent send retries with fresh systems rather than a
     released instance.
   - Add a normal start -> stop release-count test with construction-counting
     systems: each direction is released exactly once after a successful start
     and stop.
   - Add a `SetDeliverySystem(netem, netem)` then `Start` before `Init` test:
     the start fails and both explicit systems are released exactly once.
   - Add a double `SetDeliverySystem` test: the first pair is released once,
     including the same-instance-in-both-directions case, and the second pair is
     the one used.
   - Add a `SetDeliverySystem` followed by `TryMakeReliableForDebug` test: the
     explicit pair is released once and the next endpoint uses perfect systems.
     The reverse order (debug then `SetDeliverySystem`) throws
     `InvalidOperationException`, because reliable-debug mode already selected
     perfect systems; document and test that.
   - Add a server reliable-debug test asserting `false`. Add an adapter
     capability (for example `bool ServerSupportsReliableDebug`) to
     `IRawUnreliableConformanceAdapter`, set it false for Direct and true for
     UDP. Make `EnableReliable` an instance method (or read the capability from
     the fixture) so it can query `CreateAdapter()`; all 30 call sites keep
     their `(client, fixture.Server)` shape. This keeps the UDP server's `true`
     assertion.
   - Update `Coverage.md` only where a documented suite exists.
8. Update `Transports/Pontifex.Transport.Direct/Raw/Unreliable/Specification.md`
   §5, §6, §7, and §8 for lazy construction, exact ownership, the client-only
   debug mode, and the unbounded debug commit path.

**Verification**

- `dotnet build Pontifex.sln`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj`
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj`
- Expected: all pass; no delivery system built by the transport exists before
  start; the reliable-mode conformance tests run with the client capability
  only; the unbounded debug queue does not allocate a huge array.

**Definition of done**

No delivery system is constructed during transport construction or before
start, including through the binding; each system has one owner and is
released once on every path, including an explicit pre-start stop; connect or
install exceptions do not stop the endpoint and retry with fresh systems; the
server reports reliable-debug as unsupported; the conformance adapter still
runs reliable-mode tests through the client.

**Risks / notes**

The explicit `SetDeliverySystem` API and the factory API must not both claim the
same instance. A client transport creates one endpoint per start and `Start` is
one-time, so explicit systems are consumed once. A pre-start `Stop` releases
them and replaces the fields with fresh perfect systems; do not leave a released
instance reachable. Keep `StopCarrier` idempotent.

## Stage 5 — RawReliable endpoint framing and connection notification

**Objective**

Make RawReliable drop malformed frames instead of stopping, exclude transport
framing from `MessageMaxByteSize`, and prevent payload delivery before
`OnConnected` completes.

**Inputs**

- Stages 1–4 complete.
- Read `Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md` §6.5,
  §8, §9, §10, and §11.

**Actions**

1. `Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableEndpoint.cs`:
   - Add a connect-completed latch (for example `_connectCompleted`) set after
     the handler's `OnConnected` callback returns. `NotifyConnectedCore` sets
     it in a `finally`; a separate `MarkConnectedNotified` path does not.
   - In `ProcessInboundMessage(UnionDataList)`, replace the `Stop(...)` on a
     missing/unknown frame type with `Log` + `Release` + return.
   - In `ProcessInboundMessage(MessageType, UnionDataList)`, deliver a payload
     only when the connect-completed latch is set. A payload received before
     the latch is set is buffered in arrival order and drained immediately
     after `OnConnected` returns, not dropped: abstract §10 requires every
     accepted payload to be delivered exactly once in order unless the session
     terminates. Add a dedicated pre-connect inbox guarded by one lock that
     covers the latch, append, and drain. Add a protected virtual limit
     (64 messages and `64 * MessageMaxByteSize` bytes, overridable for tests).
     On overflow, release the buffered messages and stop the session with
     `TextFail(TransportName, "Pre-connect payload buffer exceeded")`; never
     drop silently. Add an overflow test and a concurrent-arrival test to the
     stage.
   - Change `Send` so it checks state, null/alive, and payload size before
     prepending the type byte. Measure `int payloadSize =
     buffer.GetDataSize()` before `PutFirst`, then call a new
     `ScheduleToSend(UnionDataList, int payloadSize)` overload in `RawEndpoint`.
     That overload uses `payloadSize` for the `MessageMaxByteSize` comparison
     and the real `GetDataSize()` for the outbound byte budget. Do not pass a
     fixed framing constant; `PutFirst` adds an element header and can also
     grow the list-count varint.
   - In `ProcessInboundMessage(UnionDataList)`, after `TryPopFirst` and before
     dispatching a payload, compare the remaining body's `GetDataSize()` with
     `MessageMaxByteSize`; release and log an oversized body. This measures
     the true payload and avoids subtracting a guessed frame overhead.
2. `Transports/Pontifex.Transport.Direct/Raw/Reliable/RawReliableDirectClientEndpoint.cs`
   and `RawReliableDirectServerEndpoint.cs`:
   - Remove the exact `MessageMaxByteSize` pre-check on the framed message. The
     authoritative exact check is the post-pop check in the base. Do not
     introduce a coarse upper bound; there is no RawReliable frame-overhead
     constant in the repository, and `RRReliableFrame.MaxOverheadBytes` belongs
     to a different protocol. Keep the handshake-specific limits to Stage 6.
3. Tests:
   - Update `Tests/Pontifex.Tests/Endpoints/RawReliable/RawReliableEndpointTests.cs`:
     invert `Send_FramingCountsTowardMessageMaxByteSize` to accept exactly
     `MessageMaxByteSize`; replace the unhandled/empty-frame stop assertions
     with “released/logged, session stays connected, later traffic flows”.
   - Add malformed-frame and unknown-frame tests: the session stays connected,
     the frame is released and logged, and later traffic still flows.
   - Add payload-at-exactly-`MessageMaxByteSize` and one-byte-over tests in both
     directions.
   - Update every harness that calls `ConnectAndComplete`:
     `Endpoints/RawReliable/Harness/TestRawReliableEndpoint.cs`,
     `Endpoints/RawReliable/Server/Harness/TestRawReliableServerEndpoint.cs`,
     `Endpoints/RawReliable/Client/Harness/TestRawReliableClientEndpoint.cs`,
     and any Direct harness. The connect-completed latch must be armed by a
     test-visible completion path (use `ExposedNotifyConnectedCore` or a
     dedicated `MarkConnectCompletedForTests`), never by
     `MarkConnectedNotified`.
   - Add a test that injects a payload between the handshake response and the
     completion of `OnConnected`; assert it is not exposed before
     `OnConnected` returns and is delivered exactly once, in order, after it
     returns. Add a second payload to prove FIFO drain.
   - Add a pre-connect buffer overflow test using the overridable limit: the
     session stops with the `TextFail` reason, every buffered message is
     released exactly once, and no payload is delivered.
   - Add a concurrent-arrival test: a payload arrives while the latch is being
     set and the drain runs; assert exactly-once, ordered delivery with no loss
     or duplication. Force the interleaving with a gated handler, not a sleep.
   - Update `Coverage.md` only where a documented suite exists.
4. Update `Transports/Pontifex.Transport.Direct/Raw/Reliable/Specification.md`
   §6 and §8 for malformed-frame dropping, framing exclusion, and the
   pre-`OnConnected` payload buffering/drain rule (not dropping).

**Verification**

- `dotnet build Pontifex.sln`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj`
- Expected: all pass; boundary payload tests pass; malformed frames do not stop
  a session.

**Definition of done**

Malformed reliable frames are dropped and logged; an application payload of
exactly `MessageMaxByteSize` is accepted; no payload is exposed before
`OnConnected` returns.

**Risks / notes**

Keep the existing handshake response/reject checks in Stage 6; do not duplicate
their size constants here. The connect-completed latch must not make
`IsConnected` false after a successful connection.

## Stage 6 — RawReliable Direct handshake deadlines, size checks, and parameters

**Objective**

Give both Direct roles a positive handshake deadline, enforce
`HandshakeMaxByteSize` on every handshake frame including its type byte, and
make the binding’s parameter ranges match the constructors.

**Inputs**

- Stage 5 complete.
- Read `Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableHandshakeClientEndpoint.cs`,
  `RawReliableHandshakeServerEndpoint.cs`,
  `Pontifex/Implementations/Raw/Reliable/RawReliableAdmissionServerTransport.cs`,
  and the Direct Reliable specification §4–§7.

**Actions**

1. Add a public handshake-timer seam,
   `Pontifex/Implementations/Raw/Reliable/IRawReliableHandshakeTimerFactory.cs`,
   with `IDisposable Schedule(TimeSpan delay, Action callback)`. It must be
   public because it appears in a protected constructor of a public abstract
   class. Provide a system implementation backed by `System.Threading.Timer`.
   `Schedule` returns a no-op handle for `Timeout.InfiniteTimeSpan`; it must
   never create a timer with an infinite due time.
   Add a manual test double
   `Tests/Pontifex.Transport.Direct.Tests/RR/TestSupport/ManualHandshakeTimerFactory.cs`
   implementing the new interface, with `Fire`/`Dispose` observation, or make
   the existing `ManualTimerFactory` implement both interfaces.
2. `RawReliableHandshakeClientEndpoint`:
   - Accept the timer factory (optional, default system) through its
     constructor.
   - Keep `CreateHandshakeTimer` virtual, but make the default delegate to the
     factory.
   - Remove the Direct override that returns `null`; the Direct client must
     start the deadline with the configured timeout.
3. `RawReliableAdmissionServerTransport`:
   - Expose the timer factory to the concrete carrier through an `internal`
     constructor overload or an `internal` property (not a protected member of
     an internal type). The existing protected constructor stays source
     compatible.
4. `RawReliableDirectServerTransport`:
   - Add a `handshakeTimeout` constructor parameter with a `null` default that
     resolves to 10 seconds. The public constructor rejects
     `Timeout.InfiniteTimeSpan` and non-positive values; only an explicit
     internal/test constructor may disable the deadline. The binding advertises
     positive seconds only.
   - Add an internal test constructor overload that accepts
     `IRawReliableHandshakeTimerFactory`.
   - Start one timer per `HandshakeEntry` when the entry is registered, unless
     the resolved timeout is infinite. On timeout, claim the entry, remove it,
     `AbandonAdmission`, send a `Timeout` reject if the channel is still usable,
     and close the channel. Dispose the timer when the entry is claimed,
     completed, removed, or the server stops.
   - Test with the manual timer factory; inject it into Direct server fixtures
     too, so the default suite never creates a real timer. No real 10-second
     wait.
5. `RawReliableDirectClientTransport`:
   - Add a `handshakeTimeout` constructor parameter and pass it to the endpoint.
   - Add an internal constructor overload or internal factory parameter that
     accepts `IRawReliableHandshakeTimerFactory`, forwarded to
     `RawReliableDirectClientEndpoint`. The Direct tests inject a manual
     factory through this path.
   - Update `RawReliableDirectClientEndpoint.HasHandshakeTimer` so it reflects
     the real timer state, or replace it with the injected factory's observed
     schedule/dispose counts. Remove the hard-coded `=> false`.
6. Handshake size checks:
   - Do not use a fixed `+1`. For outbound frames, measure the actual framing
     delta: `int before = body.GetDataSize(); body.PutFirst(type); int total =
     body.GetDataSize();`. The delta includes the element header and any
     list-count varint growth.
   - On receive, add a protected virtual hook on `RawReliableEndpoint`, for
     example `bool ValidateInboundHandshakeFrame(MessageType type, int
     totalSize)`, called by the sealed dispatcher before it pops the type. The
     handshake client overrides it and compares `totalSize` with
     `_handshakeMaxByteSize` only for `HandshakeResponseMessage` and
     `HandshakeRejectMessage`; every other frame type returns true. Do not add
     a server-session hook for the handshake request: the Direct server
     consumes that request in `RawReliableDirectServerTransport.OnHandshakeMessage`
     before any session endpoint exists, and the existing check there is the
     server-side check. The default returns true. A `false` result for a
     handshake frame stops the endpoint with the existing mapped
     `TextFail`/`ProtocolMismatch`; do not silently drop an oversized
     handshake. This passes the exact total without a shared mutable delta
     field and cannot reject a normal payload above 128 bytes.
   - `RawReliableDirectServerTransport.SendRejection`: build the reject, add
     the type byte, measure the total, and if it is too large send a minimal
     reject with no text or close without sending. Release exactly once.
   - The accepted response path: add a protected virtual
     `ValidateHandshakeResponseFrame(UnionDataList framedResponse)` hook to
     `RawReliableHandshakeServerEndpoint`, called after `PutFirst` and before
     `ScheduleToSend`; default true. Pass `HandshakeMaxByteSize` to
     `RawReliableDirectServerEndpoint` and override the hook there to compare
     the framed size. If it fails, stop with `TextFail` and do not send.
   - The client reject/response checks use the same exact total from the
     inbound hook.
   - The server request check already includes the type byte; keep it.
7. `Transports/Pontifex.Transport.Direct/Raw/Reliable/RawReliableDirectBinding.cs`:
   - Change `handshake_max_byte_size` to `new IntegerType(1, int.MaxValue)`.
   - Add optional `message_max_size` (default `DirectInfo.DefaultMessageMaxSize`)
     and `buffer_capacity_bytes` (default `DirectInfo.DefaultBufferCapacity`).
   - Add optional `handshake_timeout` seconds (default 10; positive).
   - Reject `handshake_max_byte_size > message_max_size` at binding validation
     with a clear `ArgumentException`.
   - Pass all values to both roles.
8. Tests:
   - Add a manual timer test for client timeout before a response and server
     timeout before a request.
   - Add oversized request/response/reject tests, including a reject whose text
     makes it too large; assert the minimal fallback or clean close.
   - Update `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/RawReliableHandshakeClientEndpoint/RawReliableHandshakeClientEndpointTests.cs`
     for the timer-factory constructor and the real deadline.
   - Replace the stale `HasHandshakeTimer` and `HandshakeTimerCreateCalls`
     assertions in
     `Tests/Pontifex.Transport.Direct.Tests/Raw/Reliable/RawReliable/RawReliableDirectTransportTests.cs`
     with the manual-timer state (the Direct client now creates a timer; the
     old override counter becomes zero or is removed).
   - Inject the manual `IRawReliableHandshakeTimerFactory` into every Direct
     RawReliable client fixture, not only the timeout tests, so removing the
     `null` override does not introduce a real `System.Threading.Timer` into
     the default suite.
   - Add binding tests for `int.MaxValue` acceptance with
     `message_max_size = int.MaxValue`, `int.MaxValue + 1` rejection, a
     handshake maximum greater than the default 1 MB message maximum rejection,
     a handshake maximum greater than a configured message maximum rejection,
     the new parameters, and the timeout value.
   - Update `Coverage.md` where a documented suite exists.
9. Update `Transports/Pontifex.Transport.Direct/Raw/Reliable/Specification.md`
   §4, §5, §6, §7, §8, and the checklist. Remove “no handshake timer” and
   “no deviation” claims that are no longer true; declare any residual
   deviation explicitly.

**Verification**

- `dotnet build Pontifex.sln`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj`
- Expected: all pass; the manual timeout tests complete without real-time
  waiting; the binding accepts the full int range and rejects inconsistent
  values.

**Definition of done**

Both Direct roles enforce a deadline; every handshake frame is size-checked
including the type byte; the binding advertises only values the constructors
accept.

**Risks / notes**

The in-process handshake normally completes synchronously. The timer is a
correctness guard for a non-compliant peer, not a normal-path delay. Dispose
every timer exactly once; a claimed handshake must cancel its deadline before
sending the response.

## Stage 7 — Netem timing, configuration, queueing, and lifetime

**Objective**

Fix the Netem time base and make configuration validation, queue limits, and
repeated release/Dispose correct and deterministically testable.

**Inputs**

- Stages 1 and 4 complete (Stage 4 adds `UnreliableDeliveryBinding.CreateFactory`,
  which this stage keeps).
- Read `Transports/Pontifex.Transport.Direct/VirtualDelivery/Netem/help.md`,
  `NetemConfig.cs`, `NetemDeliverySystem.cs`, `SlotScheduler.cs`,
  `RateLimiter.cs`, and `UnreliableDeliveryBinding.cs`.

**Actions**

1. Add `Transports/Pontifex.Transport.Direct/VirtualDelivery/Netem/MonotonicTime.cs`:
   - `ToNanoseconds(long ticks, long frequency)` using split arithmetic:
     `seconds = ticks / frequency`, `remainder = ticks % frequency`,
     `seconds * 1_000_000_000 + remainder * 1_000_000_000 / frequency`.
   - `NowNanoseconds()` using `Stopwatch.GetTimestamp()` and
     `Stopwatch.Frequency`.
   - An optional `Func<long>` clock seam for tests, defaulting to the real
     monotonic source.
2. Replace both duplicated `CurrentTimeNs` formulas in
   `NetemDeliverySystem` and `SlotScheduler` with `MonotonicTime`.
   - Add an internal `SlotScheduler(SlotConfig, DistributionTable?,
     Func<long>)` constructor and use it from `NetemDeliverySystem`, so
     `Reset` and construction use the same test clock.
3. `NetemDeliverySystem`:
   - Accept an optional clock function and an optional manual-pump mode in an
     internal constructor; in manual mode do not start the dequeue thread.
   - Add an internal `PumpDue(long nowNs)` (or equivalent) that performs one
     dequeue decision and invokes `Delivered`/releases exactly like the thread.
   - Start the real dequeue thread lazily on the first `Deliver` if not in
     manual mode. Track whether the thread actually started.
   - Guard `Clear` and `Dispose` with a `Volatile`/`Interlocked` disposed flag;
     make `Clear` a safe no-op after `Dispose`; catch `ObjectDisposedException`
     around `_signal.Set()`.
   - Make `Dispose` idempotent and safe for concurrent callers; do not
     double-dispose the CTS/signal.
   - Skip `_dequeueThread.Join` when the thread was never started; a
     constructed-and-disposed system must not throw `ThreadStateException`.
4. `NetemConfig`:
   - Add `Validate()` that rejects negative `LatencyNs`, `JitterNs`,
     `PacketOverhead`, `CellOverhead`, `QueueLimit`, `Slot.MinDelayNs`,
     `Slot.MaxDelayNs`, `Slot.DistDelayNs`, `Slot.DistJitterNs`,
     `Slot.MaxPackets`, and `Slot.MaxBytes`.
   - Treat cells as enabled exactly when `CellSize > 0`; `CellSize == 0`
     disables them. Do not attempt a negative `CellSize` check: the type is
     `uint` and the binding schema already constrains `cellsize` to
     `0..uint.MaxValue`.
   - Delegate loss-model parameter validation to
     `NetemDeliverySystem.CreateLossModel`: `FourState` requires non-null
     `FourState`, `GilbertElliot` requires non-null `GilbertElliot`, and an
     unknown `LossModelKind` is rejected. Add one valid and one invalid test
     per model.
   - Call it from the `NetemDeliverySystem` constructor.
5. `UnreliableDeliveryBinding`:
   - Change schema minima for `packetoverhead`, `celloverhead`, `queuelimit`,
     `slot.maxpackets`, and `slot.maxbytes` from `int.MinValue` to `0`.
   - `Validate` must validate a `lossmodel` value even when `type = perfect`
     (parse it when present) and must call `NetemConfig.Validate` for netem.
   - Keep `CreateFactory` behavior from Stage 4.
   - Call `UnreliableDeliveryBinding.Validate` from
     `RawUnreliableDirectBinding.Create` before `CreateFactory`, so the
     RawUnreliable registration rejects `perfect` + bogus `lossmodel`. Add a
     RawUnreliable registration test for that case.
6. Queue duplication:
   - In `Deliver`, compute available room `QueueLimit - totalQueued`; if there
     is no room, release and return. Clamp the duplicate count to the available
     room; never enqueue more than the limit.
7. `DeliverySystemLifetime.Release`:
   - Keep `Clear` then `Dispose`, but ensure the Netem implementation is safe
     for repeated release. Do not swallow arbitrary exceptions here; Channel
     fault isolation belongs to Stage 8.
8. Tests:
   - Add `MonotonicTimeTests` proving positive, non-decreasing conversion at
     `frequency = 1_000_000_000` and `10_000_000` for tick values that overflow
     the old formula.
   - Add deterministic Netem pipeline tests using the manual pump: latency,
     jitter, rate pacing, slot windows, duplicate delivery, queue limit, and
     dispose-with-queued-messages.
   - Add `NetemConfig` validation tests for each negative field and an unknown
     `lossmodel` with `type = perfect`.
   - Add `UnreliableDeliveryBindingTests` registration/schema tests: a negative
     `packetoverhead`, `celloverhead`, `queuelimit`, `slot.maxpackets`, and
     `slot.maxbytes` is rejected by the declared range, and an unknown
     `lossmodel` with `type = perfect` is rejected during validation.
   - Add a duplication-at-the-queue-limit test.
   - Add concurrent/repeated `Clear` and `Dispose` tests.
   - Update `Tests/Pontifex.Transport.Direct.Tests/VirtualDelivery/Coverage.md`.
9. Rewrite `SaturatedLossWithDuplication_DropsThePacket` (L-26) so it uses the
   manual pump or a pure decision helper and no longer relies on the invalid
   one-hour-latency rationale. The assertion must still prove synchronous
   drop/release.

**Verification**

- `dotnet build Pontifex.sln`
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --filter "TestCategory=Direct.VirtualDelivery"`
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj`
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj`
- Expected: all pass; no test waits on real Netem timing; the overflow test
  proves positive nanoseconds.

**Definition of done**

Netem latency/jitter/rate/slot timing uses overflow-free monotonic nanoseconds;
invalid configurations are rejected; duplication cannot exceed `QueueLimit`;
repeated release/Dispose is safe; Netem tests are deterministic.

**Risks / notes**

Do not change the public Netem constructor signature. Put the manual seam in an
`internal` constructor used by tests. `MonotonicTime` must target netstandard2.1;
do not use .NET 7+ `Stopwatch.GetElapsedTime`.

## Stage 8 — Channel and registry correctness

**Objective**

Make Channel teardown fault-isolated and leak-free, make delivery-system
release exactly-once, clean up failed connects, and scope Direct `id` by
protocol.

**Inputs**

- Stages 1–4 and 7 complete. Stages 2–4 change the Direct endpoint/transport
  files this stage scopes; Stage 7 supplies the release semantics.
- Read `Transports/Pontifex.Transport.Direct/Channel/Channel.cs`,
  `ChannelManager.cs`, and all four Direct scheme registrations.

**Actions**

1. `Transports/Pontifex.Transport.Direct/VirtualDelivery/DiscardingDeliverySystem.cs`
   (new):
   - `Deliver` releases the message; `Clear` is a no-op; no event subscribers
     are required.
2. `Channel.cs`:
   - Replace the fresh `PerfectDeliverySystem` installed during `Dispose` with
     a shared static `DiscardingDeliverySystem` so a racing send releases its
     message.
   - Add a `SafeRelease(IDeliverySystem)` helper that catches and ignores
     release exceptions; use it in `Dispose`, `SetClientDeliverySystem`, and
     `SetServerDeliverySystem`.
   - In `Dispose`, release `_clientDeliverySystem` and `_serverDeliverySystem`
     only once when both fields reference the same instance.
   - In an install, do not release the old system if it is still referenced by
     the other direction; detach the handler and leave it to the other field or
     disposal.
   - Make `InstallDeliverySystem` no-throw after the `Interlocked.Exchange`.
     Once the exchange succeeds, the channel owns the new system; a later
     release/subscription failure must be caught, and the endpoint must not
     release the system again. Define this ownership boundary in the XML
     remarks and cover it with the install-throw test.
   - Raise `Disposed` after both releases, and never let a throwing subscriber
     skip another subscriber.
3. `ChannelManager.cs`:
   - Add a protocol/scheme scope parameter to `RegisterServer`,
     `UnregisterServer`, `Connect`, `Disconnect`, and `Detach`, and key the
     registry by `(scheme, IEndPoint)` so the same `id` can exist under
     different Direct protocols and a client only sees its own scheme.
   - Do not provide a default shared scope; a default would silently reintroduce
     cross-protocol mixing. Update every production and test call site in this
     stage, including all server-stop `UnregisterServer` calls.
   - In `Connect`, if `InvokeOnChannelCreated` throws, remove the client entry,
     dispose the channel, log nothing (the channel manager has no logger), and
     return null. Do not rethrow: RawReliable client start must not become a
     failed start because a server callback threw, and the RawUnreliable/M04
     rule keeps the endpoint alive. Do not leave a registered channel.
   - This stage owns the manager-callback exception path. Rewrite the Stage 4
     “manager-callback-throw” test to assert the returned-null path; keep the
     Stage 4 install-throw test for the endpoint’s own catch.
4. Update every Direct transport that registers/connects:
   - `RawUnreliableDirectClientEndpoint`/`ServerTransport` uses
     `RawUnreliableDirectInfo.TransportName`.
   - `RawReliableDirectClientEndpoint`/`ServerTransport` uses
     `RawReliableDirectInfo.TransportName`.
   - `DirectRrChannelClient`/`DirectRrChannelServer` use the RR scheme passed
     by their owners (`RRDirectInfo.UnreliableTransportName` or
     `ReliableTransportName`).
   - Make the scope explicit in constructors or constants; do not hard-code
     `"direct"` in the channel manager.
5. Update every test call site of `ChannelManager`:
   - `Tests/Pontifex.Transport.Direct.Tests/ChannelManager/Tests/ChannelTests.cs`
     and `ChannelManagerTests.cs`.
   - `Tests/Pontifex.Transport.Direct.Tests/RR/RRUnreliableDirect/Tests/RRUnreliableDirectCarrierTests.cs`
     (including the eight direct carrier constructions).
   - `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableDirect/Tests/RRReliableDirectLifecycleTests.cs`.
   - `Tests/Pontifex.Transport.Direct.Tests/Raw/Reliable/RawReliable/RawReliableDirectTransportTests.cs`.
   - `Tests/Pontifex.Transport.Direct.Tests/Raw/RawUnreliableDirectDeliverySystemTests.cs`.
6. Tests:
   - Add Channel tests for throwing delivery system, concurrent send/dispose
     release, shared system in both directions, and throwing server callback
     cleanup. Force the send/dispose interleaving with a blocking
     `IDeliverySystem` rather than a bare thread race.
   - Add a protocol-scope test: two servers with the same `id` under different
     schemes both start, and a client of each scheme reaches the correct
     server.
   - Update `Coverage.md`.
7. Update all four Direct specifications:
   - RawUnreliable §6/§7 and RawReliable §6 for protocol-scoped `id`.
   - RR Unreliable §8 and RR Reliable §8 for the same rule and the clear
     failure behavior; update the checklist entries that repeat it.
   - Update the “id uniqueness” statement to “unique within the Direct
     protocol” and document the cross-protocol isolation.

**Verification**

- `dotnet build Pontifex.sln`
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --filter "TestCategory=Direct.ChannelManager"`
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj`
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj`
- Expected: all pass; teardown tests prove both systems are released and
  `Disposed` is raised; cross-protocol tests prove isolation.

**Definition of done**

Channel teardown never aborts on a delivery-system throw, a racing send never
leaks, shared systems release once, a throwing server callback leaves no
registry entry, and cross-protocol `id` collisions are impossible.

**Risks / notes**

`ChannelManager` is internal; changing its signature is acceptable if all
call sites and tests are updated. Do not change `IEndPoint` equality. The
`DiscardingDeliverySystem` must never be cleared into a state where a racing
`Deliver` throws.

## Stage 9 — RRReliable session locking and response state

**Objective**

Make RRReliable session maps thread-safe and make `Finish`/`Reject`/TTL state
transitions match abstract §6.6.

**Inputs**

- Stage 1 complete.
- Read `Pontifex/Abstractions/Transports/RR/Reliable/Specification.md` §6.5,
  §6.6, and §10; `RRReliableServerSession.cs`, `RRReliableResponse.cs`,
  `RRReliableServerTransport.cs`, and `RRReliableClientTransport.cs`.

**Actions**

1. `RRReliableServerSession.cs`:
   - Lock `SyncRoot` inside `IsClosed`, `IsFull`, `IsLive`, `IsCompleted`,
     `AddLive`, both `TryTakeLive` overloads, `MarkCompleted`, and `Close`.
   - Keep the completion bitmap read-modify-write and `_completedCount++` under
     the same lock.
   - Document that callers may still take the lock reentrantly, but the class
     is safe without an external lock.
2. `RRReliableResponse.cs`:
   - Add a distinct `ResponseState.Finished` (or equivalent flag) so `Finish`
     is distinguishable from a generic `Closed` caused by TTL/session close.
   - `Finish` closes with `Finished` only when the carrier send returns `Ok`;
     on a non-`Ok` result, leave the handle usable for a retry.
   - Apply abstract §6.6 state precedence per method:
     - `Response`/`Finish`: transport stopping/stopped → `Error`; then
       `Rejected` → throw; then `Finished`/TTL/session/disposal close →
       `Error`; then invalid message, too big, carrier result.
     - `Reject`: transport stopping/stopped → `Error`; then `Rejected` →
       `Ok`; then `Finished` or `Accepted` → throw; then TTL/session/disposal
       close → `Error`; then send.
     - `Accepted` + live is not terminal; `Reject` throws, `Response`/`Finish`
       proceed.
   - `Reject`:
     - if the transport is stopping/stopped, return `Error` even when the
       handle is already `Rejected`;
     - otherwise return `Ok` when already `Rejected`;
     - throw `InvalidOperationException` when `Accepted` or `Finished`;
     - return `Error` when closed by TTL/session/disposal;
     - close with `Rejected` only on a successful send.
   - `Response`/`Finish` throw `InvalidOperationException` after a successful
     `Reject` while the transport is live; if the transport later stops, all
     calls return `Error` instead.
   - Update the remarks.
3. `RRReliableClientTransport.cs`:
   - In `Request`, wrap `TimerFactory.Schedule` and `TrySetTimer` in
     `try`/`catch`. On failure, remove the request from the table, mark it
     terminal, release provisional/buffered data, unbind the handler, and
     return `Error`.
   - Ensure no request remains bound without a TTL after the exception.
4. Add a deterministic dispatch-queue seam to `RRReliableServerTransport`:
   - Add an internal `IDispatchQueue<T>` interface in the `Pontifex` assembly
     next to `SerializedCallbackQueue<T>` (`Pontifex/Utils/`) that extends
     `ISerializedCallbackQueue<T>` and adds
     `event Action<Exception>? ExceptionHandler`. Implement it on
     `SerializedCallbackQueue<T>` (the event already exists).
   - Change the nested `DispatchItem` type from `private` to `internal` so the
     Direct test harness can name it. Keep the existing `protected` constructor
     unchanged.
   - Add a separate `internal` constructor overload that takes
     `Func<int, string, Action<DispatchItem>, Action<DispatchItem>,
     ISerializedCallbackQueue<DispatchItem>> dispatchQueueFactory`. The
     internal constructor plus internal item type keeps the signature legal.
   - Default to `new SerializedCallbackQueue<DispatchItem>(...)`; store the
     field as `ISerializedCallbackQueue<DispatchItem>`.
   - Add `ManualCallbackQueue<T>` in the shared non-suite
     `Tests/Pontifex.Transport.Direct.Tests/RR/TestSupport/` folder, next to
     `ManualTimerFactory.cs`; both RR suites reference it. It records posted
     items and exposes `RunAll()`/`RunOne()`. It must implement
     `IDispatchQueue<T>` as well as `ISerializedCallbackQueue<T>`, so Stage 10
     can reuse it. Tests keep their own reference to call it; do not poll a
     worker.
   - Add an internal `RRReliableDirectServer` constructor overload that
     forwards the dispatch-queue factory, so the Direct wiring test can inject
     `ManualCallbackQueue<T>`.
5. Tests:
   - Put the concurrent session-map stress and response/TTL state tests in the
     `RR/RRReliableTransport/Server/` layer (the suite owns
     `RRReliableServerTransport` and `RRReliableResponse`); carry the suite
     root category. Put the Direct carrier wiring test in
     `RR/RRReliableDirect/Tests/`.
   - Add concurrent session-map stress: new requests on multiple threads,
     completion on manual dispatch/timers, cancel, and session close; assert no
     exceptions, exactly-once completion, and the 65,536 cap. Put this
     multithreaded fixture in category `Direct.RRReliable.Integration`; the
     deterministic state tests stay in the default run.
   - Update `RRReliableTransport/Server/RRReliableServerTransportTests.cs`
     `ResponseHandle_MatrixOfFinishRejectAndClosedCalls` and its Coverage row:
     `Finish` then `Reject` now throws `InvalidOperationException` while live;
     post-stop calls remain `Error`.
   - Add state tests: `Finish` then `Reject` throws; failed `Finish` can be
     retried; failed `Reject` can be retried; TTL close then `Reject` returns
     `Error`; successful `Reject` then `Response`/`Finish` throw while live;
     after the transport stops, all three return `Error`, including a repeated
     `Reject`.
   - Add a client TTL-factory throw test and a null-timer test; assert the
     handler is unbound and the table is empty.
   - Update `Coverage.md`.
6. Update `Transports/Pontifex.Transport.Direct/RR/Reliable/Specification.md`
   §6 (Lifecycle and teardown) and §8 (Completion transport row) in this stage
   for the corrected `Finish`/`Reject`/TTL state machine. The Direct spec has
   no `§6.6`; do not reference that section number. Stage 11 may refine the
   wording but must not be the only place the behavior change is documented.

**Verification**

- `dotnet build Pontifex.sln`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --filter "TestCategory=Direct.RRReliable|TestCategory=Direct.RRReliableCoreHelpers|TestCategory=Direct.RRReliableTransport"`
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj`
- Expected: all pass; the concurrency stress test is repeatably green.

**Definition of done**

Session maps are safe under concurrent access, completion is exactly once,
`Finish`/`Reject` state transitions match §6.6, and a timer-factory exception
cannot leave a bound handler without a TTL.

**Risks / notes**

All response-handle methods may hold `_gate` while taking `SyncRoot`; no code
may take `SyncRoot` and then call a response method that takes `_gate`. The
existing `HandleRequest` calls `AttachTimer` and `CloseForDisposal` under
`SyncRoot`; those deliberately do not take `_gate`, so they stay valid. Keep
that lock order. Do not make `MarkCompleted` lock the response handle.

## Stage 10 — RRUnreliable Direct stop and pre-start capacity

**Objective**

Make a request that races a Direct RRUnreliable stop fail with `Error` instead
of being silently accepted, and make the pre-start pending bound use the
configured queue capacity.

**Inputs**

- Stages 1, 8, and 9 complete. Stage 9 adds the shared
  `ManualCallbackQueue<T>` in `Tests/Pontifex.Transport.Direct.Tests/RR/TestSupport/`.
- Read `Pontifex/Implementations/RR/Unreliable/RRUnreliableClientTransport.cs`,
  `RRUnreliableDirectClient.cs`, `RRUnreliableDirectServer.cs`, and
  `Transports/Pontifex.Transport.Direct/RR/Unreliable/Specification.md` §5–§7.

**Actions**

1. `Transports/Pontifex.Transport.Direct/RR/Unreliable/RRUnreliableDirectClient.cs`:
   - When `_carrier == null`, release the frame and return `SendResult.Error`,
     not `Ok`. The no-peer policy applies while a carrier exists; a stopped
     carrier must reject the request so the core unbinds the handler.
   - Add an internal `SendFrameForTests` entry point (or a test-only carrier
     setter) so the null-carrier branch is testable without reflection.
2. `Pontifex/Implementations/RR/Unreliable/RRUnreliableClientTransport.cs`:
   - Add a one-way `private volatile bool _stopping` latch. Set it at the start
     of `ClearPendingRequests`, before clearing the table. Do not add a second
     mutex or reach into `RRUnreliableRequestTable._locker`.
   - Check it in `Request` before binding/after binding and after the send, so
     a request that races the stop is removed, the handler is unbound, and
     `Error` (or `NotConnected`) is returned.
   - Ensure `IsStarted` remains the state result for a request that starts
     after the transport stopped.
3. `Transports/Pontifex.Transport.Direct/RR/Unreliable/RRUnreliableDirectServer.cs`:
   - Replace the `RRDirectInfo.DispatchQueueCapacity` comparison in
     `OnInbound` with `_queueCapacity`.
   - Use the `IDispatchQueue<T>` interface added in Stage 9 (Pontifex/Utils).
     Do not redeclare it in the Direct assembly.
   - Add an internal `PendingCountForTests` accessor (or test driver) so the
     configured pre-start bound can be observed without reflection.
   - Add an optional internal dispatch-queue factory delegate, defaulting to
     `new SerializedCallbackQueue<InboundFrame>(...)`. Store the field as
     `IDispatchQueue<InboundFrame>` so both the exception subscription and the
     manual queue compile. Tests keep their own `ManualCallbackQueue` reference
     to call `RunAll()`.
4. Tests:
   - Add a stop-race test: block the send path or use a carrier hook, call
     `Stop` concurrently with `Request`, and assert the handler is unbound and
     no request remains in the pending window. Put it in
     `Direct.RRUnreliable.Integration`, or make it fully gated and declare it a
     deterministic default test; do not leave it uncategorised and racing.
   - Add a test that `SendFrame` with a null carrier returns `Error` and
     releases the frame.
   - Add pre-start bound tests with `queueCapacity = 10` and `queueCapacity = 200`;
     assert the pending list uses the configured value and the surplus is
     released.
   - Update `Coverage.md`.
5. Update `Transports/Pontifex.Transport.Direct/RR/Unreliable/Specification.md`
   §6 and §7 for the corrected stop behavior and configured capacity.

**Verification**

- `dotnet build Pontifex.sln`
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj`
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --filter "TestCategory=Direct.RRUnreliable"`
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj`
- Expected: all pass; the stop-race test is repeatably green.

**Definition of done**

A request racing a stop is never silently accepted, the handler is always
unbound on failure, and the pre-start pending bound equals the configured queue
capacity.

**Risks / notes**

Use the one-way volatile `_stopping` latch; do not add a second mutex that could
invert with the pending table. A null carrier is not the same as a live carrier
with no peer.

## Stage 11 — Specification and suite documentation corrections

**Objective**

Correct every remaining documentation-only audit finding and align the Direct
specifications with the behavior established in Stages 1–10.

**Inputs**

- Stages 1–10 complete.

**Actions**

1. `Transports/Pontifex.Transport.Direct/RR/Unreliable/Specification.md`:
   - Correct deviation 1: a request can wait behind a running `OnRequested`,
     not only `OnStarted`.
   - Correct deviations 1 and 2: the handle is closed lazily; the observable
     result is `Error`, not a literally pre-closed handle.
   - Correct deviation 4: two netem systems make the teardown bound closer to
     ten seconds.
   - Wrap the over-long non-table lines to ≤100 characters (L-16). Locate them
     by content rather than fixed line numbers; earlier stages may shift the
     file.
2. `Transports/Pontifex.Transport.Direct/RR/Reliable/Specification.md`:
   - Correct the completion-transport row in §8: the response TTL sends
     `Finish(Failed)`; only session close sends nothing.
   - Align the §6 lifecycle text and the §8 completion row with the Stage 9
     state machine. The Direct spec has no `§6.6`; do not reference it.
3. `Transports/Pontifex.Transport.Direct/Raw/Unreliable/Specification.md`:
   - Re-audit every §7/§8 claim after Stages 2–4 and 8.
   - Ensure the “no deviation” statement is true, or declare any residual
     deviation explicitly.
4. `Transports/Pontifex.Transport.Direct/Raw/Reliable/Specification.md`:
   - Ensure §5 lists the new parameters, §6 describes the timer, §7 declares
     no deviation only if true, and the checklist matches the code.
5. `Transports/Pontifex.Transport.Direct/VirtualDelivery/Netem/help.md`:
   - Remove the non-existent `Pontifex.Transports.Direct.Delivery` using.
   - Use `Pontifex.VirtualDelivery.Netem` and, if needed,
     `Pontifex.VirtualDelivery` for `PerfectDeliverySystem`.
   - Ensure the Quick Start example compiles conceptually against the current
     constructors.
6. `Tests/Pontifex.Transport.Direct.Tests/VirtualDelivery/README.md`:
   - Add `NetemDeliverySystem` and `SlotScheduler` to the root subject.
   - Align the “Out of scope” and “Groups” sections with the tests that exist.
   - State which pipeline tests use the deterministic manual clock/pump.
7. Run a repository-wide line-length and link check over the changed
   specifications:
   - `awk 'length > 100 { print FILENAME ":" FNR ":" length }'` on each changed
     file; table rows may exceed the limit only where the specification allows
     it.
   - Re-read every changed link target and confirm it exists.

**Verification**

- `git diff --check`
- The line-length command above returns no non-table violations.
- `dotnet build Pontifex.sln`
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --filter "TestCategory=Direct.VirtualDelivery"`
- Expected: build/tests pass; no broken links; no specification claim
  contradicts the code.

**Definition of done**

All documentation-only findings (L-12, L-13, L-14, L-15, L-16, L-23, L-24) are
resolved, and the Direct specifications match the implemented behavior.

**Risks / notes**

Do not rewrite specifications wholesale. Make targeted corrections and keep
the existing structure. If a claim is still false because a Stage 1–10 change
was incomplete, stop and route the defect back to the owning stage; do not
change production code in this documentation-only stage, and do not paper over
the gap with a deviation.

## Stage 12 — Deterministic Direct test suite

**Objective**

Remove wall-clock polling and uncontrolled scheduling from the default Direct
test run, and move irreducible threaded/integration facets into declared
categories.

**Inputs**

- Stages 1–11 complete. The Netem clock/pump (Stage 7), the handshake timer
  factory (Stage 6), and the RR dispatch-queue factories (Stages 9 and 10)
  already exist.

**Actions**

1. Inventory the default Direct fixtures that use `Thread.Sleep`, `WaitFor`,
   `SpinWait`, `DateTime.UtcNow`, or real timers. At minimum:
   `Raw/RawUnreliableDirectChannelOwnershipTests.cs`,
   `Raw/RawUnreliableDirectInboundLimitTests.cs`,
   `Raw/Reliable/RawReliable/RawReliableDirectTransportTests.cs`,
   `RR/RRUnreliableDirect/Tests/RRUnreliableDirectCarrierTests.cs`,
   `RR/RRUnreliableDirect/Tests/RRUnreliableDirectLifecycleTests.cs`,
   `RR/RRReliableDirect/Tests/RRReliableDirectRoundTripTests.cs`, the
   `RRReliableTransport` client/server fixtures, and
   `ChannelManager/Tests/ChannelTests.cs` (gate the concurrent send/dispose
   test with a blocking delivery system).
2. Use the seams introduced earlier:
   - Netem manual clock/pump for VirtualDelivery tests.
   - Manual handshake timer for RawReliable Direct timeout tests.
   - `ManualCallbackQueue<T>` for RR server and RRUnreliable Direct tests.
     Add the dispatch-queue factory parameter to
     `TestReliableServerTransport`; add a manual-run/post-unbind hook to
     `TestReliableClientTransport` or its callback pump so client tests do not
     need `SpinWait`.
   - Existing `ManualTimerFactory` for RR TTL tests.
   - Explicit `TaskCompletionSource`/callback hooks where a callback is the
     only completion signal.
3. Replace `WaitFor`/`SpinWait` in unit-level tests with explicit completion
   hooks. A test must fail with a clear assertion if the expected callback
   never arrives; it must not poll wall time.
4. Define the default run once:
   - Define the default run with the CLI filter
     `--filter "TestCategory!~Integration"`; do not depend on a runsettings
     filter that the adapter may ignore.
   - Positive/negative check: run one known integration fixture with
     `--filter "FullyQualifiedName~RRReliableDirectIntegrationTests&TestCategory!~Integration"`
     and assert the output reports no matching tests; run the same fixture
     without the category filter and assert it executes.
   - List every integration category in use: currently
     `Direct.RRReliable.Integration` and `Direct.RRUnreliable.Integration`,
     plus any `*.Integration` category added by this stage. The `!~` filter
     excludes all of them.
   - Update `.github/workflows/build-test-deploy.yml` with explicit per-project
     commands: a filtered default pass for every test project using
     `TestCategory!~Integration` (label it “filtered”, not “fully
     deterministic”, because Net/Transports have real-I/O tests without that
     category), then an integration pass for the Direct, Net, and Transports
     projects using `TestCategory~Integration`. Do not replace the
     solution-wide test step with Direct-only commands.
   - Document the two commands in the Direct suite READMEs.
5. Add a static test-suite guard as an NUnit fixture, for example
   `Tests/Pontifex.Transport.Direct.Tests/DirectTestDeterminismTests.cs`:
   - Locate the repository root by walking up from
     `TestContext.CurrentContext.TestDirectory` until a directory containing
     `Pontifex.sln` is found.
   - Scan the Direct test project's `*.cs` files, excluding `bin/`, `obj/`,
     and the guard file itself by full path.
   - Skip a file when its fixtures carry an `*.Integration` category.
   - Fail when a default fixture contains polling forms: `Thread.Sleep`,
     `WaitFor`, `SpinWait`, a `DateTime.UtcNow` deadline, `Task.Delay`,
     `SemaphoreSlim.Wait(...timeout...)`, `Monitor.Wait(..., timeout)`, or a
     real `System.Threading.Timer`/`new Timer`. Do not flag a one-shot
     `Stopwatch` duration assertion (the Direct reject latency check); only a
     repeated `Stopwatch` polling loop is forbidden. Match polling call
     patterns, not the `WaitForSendReturn` seam or the guard's own literals.
   - The guard is a normal test in the default run; no external script or new
     package.
6. Update `Tests/Pontifex.Transport.Direct.Tests/VirtualDelivery/README.md`,
   the RR suite READMEs, and every affected `Coverage.md`.
7. Re-run the default suite multiple times under load and record the result in
   the Stage 12 report.

**Verification**

- `dotnet build Pontifex.sln`
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --filter "TestCategory!~Integration" --logger "console;verbosity=normal"`
- Run the above command three times; all must pass, and its output must not
  contain `RRReliableDirectIntegrationTests` or `RRUnreliableDirectIntegrationTests`.
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --filter "TestCategory~Integration" --logger "console;verbosity=normal"`
- Run the integration command once; it must pass with zero failures. This is
  the positive control that the integration tests are runnable; the exclusion
  control is the `FullyQualifiedName~...&TestCategory!~Integration` command
  from Action 4, which must report no matching tests.
- The determinism guard must pass in the default run.
- `git diff .github/workflows/build-test-deploy.yml` shows both the filtered
  deterministic command and the separate `TestCategory~Integration` command.
  A missing workflow edit fails the stage.

**Definition of done**

The default Direct suite has no wall-clock polling or uncontrolled background
sleeps; integration facets are categorised and documented; the CI workflow runs
the filtered default and a separate integration pass; repeated runs are stable.

**Risks / notes**

Do not weaken assertions to make a test deterministic. If a behavior can only
be observed asynchronously, add a completion hook in the harness or the
production seam. Keep all categories declared in the suite README.

## Stage 13 — Completion report and full verification

**Objective**

Run the complete verification once and produce the user-requested `report.md`
with one entry per change and per audit finding.

**Inputs**

- Stages 1–12 complete and committed.

**Actions**

1. Run the full verification:
   - `dotnet build Pontifex.sln`
   - `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj`
   - `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj`
   - `dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj`
   - `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj`
   - `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --filter "TestCategory!~Integration"`
   - `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --filter "TestCategory~Integration"`
   - Record the exact commands and results.
2. Write `report.md` at the repository root. It must contain:
   - a short title and the final verification result;
   - a table of contents;
   - one section per stage with its objective and commit message;
   - one `###` subsection per audit finding ID, in the order H-01…L-26, with
     the exact headings `### H-01` through `### L-26`. Each subsection must
     contain the labels `Problem:`, `Change:`, `Why:`, `Files:`, and
     `Verification:`;
   - a section for new or changed tests and specification documents;
   - a section for residual deviations or known limitations, or an explicit
     “none”.
3. Cross-check the report against the finding traceability table. Every one of
   the 46 IDs must appear exactly once; a missing or duplicate ID fails the
   stage.
4. Commit `report.md` with a message such as
   `Add implementation report (stage 13)`.
5. Do not delete `plan.md` in this stage; the plan-executor completion step
   deletes it after the stage is committed.

**Verification**

- `test -f report.md`
- Extract the bare finding IDs and compare the sorted set with `problems.md`:
  `diff <(grep -o '^### [HML]-[0-9][0-9]' report.md | sed 's/^### //' | sort) <(grep -o '^### [HML]-[0-9][0-9]' problems.md | sed 's/^### //' | sort)`.
  The diff must be empty. Report headings are the bare IDs (`### H-01`), not the
  full `problems.md` title text.
- For every finding subsection, scan from its `### <ID>` heading to the next
  `###` heading (not a fixed 20-line window) and require the five labels
  `Problem:`, `Change:`, `Why:`, `Files:`, and `Verification:`. Loop over the
  46 IDs and fail on a missing label.
- Require the table of contents and one `## Stage N —` heading for every stage
  1–13; each stage section must contain its objective and commit message.
- Require a `## Tests and specifications` section and a
  `## Residual deviations` section (with `none` when empty).
- Re-run the full verification commands from Action 1; all pass.
- `git status --porcelain` is clean after the report commit.

**Definition of done**

`report.md` exists, covers all 46 findings and every stage, the full build/test
matrix passes, and the working tree is clean.

**Risks / notes**

The report is evidence, not a changelog of attempts. Do not list failed drafts.
If a finding was resolved by a documentation correction rather than code, say
so explicitly. Do not claim a deviation is fixed if the code still exposes it.

## Global definition of done

- Every finding in `problems.md` is resolved in code or documentation, with a
  regression test for every behavioral fix, and listed in `report.md`.
- `dotnet build Pontifex.sln` succeeds.
- `dotnet test Tests/Pontifex.Tests`,
  `Tests/Pontifex.Transports.Tests`, `Tests/Pontifex.Transport.Tests`,
  `Tests/Pontifex.Transport.Direct.Tests` (default and integration), and
  `Tests/Pontifex.Transport.Net.Tests` pass.
- The Direct RawUnreliable conformance fixture passes.
- The Direct specs and `Tests/RULES.md` suite documentation are consistent.
- No default Direct-suite test depends on wall-clock polling or an unseamed
  thread.
- `report.md` exists at the repository root and lists every change with the
  problem it solves and what changed and why.
- `plan.md` is deleted after completion by the plan-executor completion step,
  the deletion is committed, and the working tree is clean afterwards.
