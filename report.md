# Pontifex Direct transport audit — implementation report

All 46 findings from `problems.md` are resolved in code or by an explicit
specification correction. The final verification passes: build succeeds and
every test project is green (core 788, conformance 198, transport 38, Net 333,
Factory 130, TransportFormula 782, API 3, Direct default 217, Direct
integration 168).

## Table of contents

- [Stage 1 — Fix shared lifecycle, handler isolation, and send-result precedence (stage 1)](#stage-1)
- [Stage 2 — Fix RawUnreliable routing, stop, size, and gates](#stage-2)
- [Stage 3 — Enforce RawUnreliable Direct send ordering and admission budget](#stage-3)
- [Stage 4 — Fix RawUnreliable Direct delivery ownership and debug mode](#stage-4)
- [Stage 5 — Fix RawReliable framing and connect notification](#stage-5)
- [Stage 6 — Fix RawReliable handshake deadlines, sizes, and parameters](#stage-6)
- [Stage 7 — Fix Netem timing, configuration, queueing, and lifetime](#stage-7)
- [Stage 8 — Fix Channel and registry correctness](#stage-8)
- [Stage 9 — Fix RRReliable session locking and response state](#stage-9)
- [Stage 10 — Fix RRUnreliable Direct stop and pre-start capacity](#stage-10)
- [Stage 11 — Correct specification and suite documentation](#stage-11)
- [Stage 12 — Make the Direct test suite deterministic](#stage-12)
- [Stage 13 — Add implementation report (stage 13)](#stage-13)
- [Findings](#findings)
- [Tests and specifications](#tests-and-specifications)
- [Residual deviations](#residual-deviations)

<a id="stage-1"></a>
## Stage 1 — Fix shared lifecycle, handler isolation, and send-result precedence (stage 1)

**Objective.** Make `AnyTransport`, `RawEndpoint`, `RawReliableEndpoint`, and the
shared send-result helpers obey the lifecycle, fault-isolation, and precedence
rules. **Commit:** `Fix shared lifecycle, handler isolation, and send-result precedence (stage 1)`.

<a id="stage-2"></a>
## Stage 2 — Fix RawUnreliable routing, stop, size, and gates

**Objective.** Honor the supplied stop reason, reject oversized inbound data
before routing, suppress post-stop delivery, and hit the documented gates.
**Commit:** `b17d14e Stage 2 — Fix RawUnreliable routing, stop, size, and gates`.

<a id="stage-3"></a>
## Stage 3 — Enforce RawUnreliable Direct send ordering and admission budget

**Objective.** A callback for an accepted message cannot begin before `Send`
returns, and the declared 100-message budget is enforced at admission.
**Commit:** `291a67a Stage 3 — Enforce RawUnreliable Direct send ordering and admission budget`.

<a id="stage-4"></a>
## Stage 4 — Fix RawUnreliable Direct delivery ownership and debug mode

**Objective.** Construct delivery systems only at start, make hand-off/release
exactly-once, isolate connect/install failures, and make server reliable-debug
truthful. **Commit:** `04f8a28 Stage 4 — Fix RawUnreliable Direct delivery ownership and debug mode`.

<a id="stage-5"></a>
## Stage 5 — Fix RawReliable framing and connect notification

**Objective.** Drop malformed frames instead of stopping, exclude framing from
`MessageMaxByteSize`, and prevent payload delivery before `OnConnected`.
**Commit:** `1b24a81 Stage 5 — Fix RawReliable framing and connect notification`.

<a id="stage-6"></a>
## Stage 6 — Fix RawReliable handshake deadlines, sizes, and parameters

**Objective.** Give both Direct roles a positive deadline, size-check every
handshake frame including the type byte, and advertise only usable parameters.
**Commit:** `e0daddf Stage 6 — Fix RawReliable handshake deadlines, sizes, and parameters`.

<a id="stage-7"></a>
## Stage 7 — Fix Netem timing, configuration, queueing, and lifetime

**Objective.** Overflow-free monotonic nanoseconds, validated configuration,
bounded duplication, repeat-safe release, and deterministic tests.
**Commit:** `ea8328d Stage 7 — Fix Netem timing, configuration, queueing, and lifetime`.

<a id="stage-8"></a>
## Stage 8 — Fix Channel and registry correctness

**Objective.** Fault-isolated, leak-free channel teardown; exactly-once system
release; failed-connect cleanup; protocol-scoped Direct ids.
**Commit:** `b15590e Stage 8 — Fix Channel and registry correctness`.

<a id="stage-9"></a>
## Stage 9 — Fix RRReliable session locking and response state

**Objective.** Thread-safe session maps and a `Finish`/`Reject`/TTL state machine
that matches abstract §6.6. **Commit:** `2fef902 Stage 9 — Fix RRReliable session locking and response state`.

<a id="stage-10"></a>
## Stage 10 — Fix RRUnreliable Direct stop and pre-start capacity

**Objective.** A request racing a stop fails with `Error`, and the pre-start
bound uses the configured queue capacity.
**Commit:** `4919ec2 Stage 10 — Fix RRUnreliable Direct stop and pre-start capacity`.

<a id="stage-11"></a>
## Stage 11 — Correct specification and suite documentation

**Objective.** Correct every remaining documentation-only finding and align the
Direct specifications with the implemented behavior.
**Commit:** `545f795 Stage 11 — Correct specification and suite documentation`.

<a id="stage-12"></a>
## Stage 12 — Make the Direct test suite deterministic

**Objective.** Remove wall-clock polling from the default Direct run, move
irreducible threaded facets into declared categories, and keep a static guard.
**Commit:** `ba78236 Stage 12 — Make the Direct test suite deterministic`.

<a id="stage-13"></a>
## Stage 13 — Add implementation report (stage 13)

**Objective.** Run the complete verification once and record every change and
audit finding. **Commit:** `Add implementation report (stage 13)`.

<a id="findings"></a>
## Findings

### H-01

**Problem:** The Netem time base multiplied ticks before dividing, overflowing for normal uptimes.

**Change:** Added `MonotonicTime` with split arithmetic and routed `NetemDeliverySystem` and `SlotScheduler` through it, with an injectable clock.

**Why:** Overflow-free nanoseconds are required for latency, jitter, rate, and slot timing.

**Files:** `VirtualDelivery/Netem/MonotonicTime.cs`, `NetemDeliverySystem.cs`, `SlotScheduler.cs`.

**Verification:** `MonotonicTime_ConvertsLargeTicksWithoutOverflow`, deterministic pipeline tests.

### H-02

**Problem:** The Direct server stopped its carrier before its route endpoints, so active handlers observed `UnknownRemoteIntention` instead of the supplied reason.

**Change:** `RawUnreliableServerTransport.OnStopped` stops every removed route endpoint with the supplied reason before `StopCarrier`; the later channel disposal is a one-shot no-op.

**Why:** Abstract §7.2 requires the exact `StopReason` instance at the handler.

**Files:** `RawUnreliableServerTransport.cs`, `Tests/Pontifex.Transport.Direct.Tests/Raw/RawUnreliableDirect/Tests/RawUnreliableDirectServerContractTests.cs`.

**Verification:** `ServerStop_ActiveHandler_ReceivesTheSuppliedReasonInstance`, `Stop_WhenRoutesExist_StopsEveryLiveEndpointWithTransportReason`.

### H-03

**Problem:** `RRReliableServerSession` maps were mutated without its lock.

**Change:** Every read and mutation now takes `SyncRoot`, including the completion bitmap and count.

**Why:** Concurrent request admission, completion, and close could corrupt the maps.

**Files:** `RR/Reliable/RRReliableServerSession.cs`, `RR/RRReliableTransport/Server/RRReliableServerSessionStressTests.cs`.

**Verification:** `ConcurrentMarkCompletedAndQueries_AreLockSafeAndExactlyOnce`, `ConcurrentCloseAndMarkCompleted_AreExactlyOnce`.

### H-04

**Problem:** A throwing RawReliable handler stopped the endpoint and transport.

**Change:** `OnReceived` and `OnConnected` handler exceptions are logged and isolated; the endpoint and session keep running.

**Why:** Abstract §7/§11 require handler failures to abandon only that delivery.

**Files:** `RawEndpoint.cs`, `RawReliableEndpoint.cs`, conformance fixtures.

**Verification:** `ReceiveHandlerException_DoesNotStopTransportOrPreventLaterDelivery`, endpoint tests.

### H-05

**Problem:** RawReliable client `Init` violated its one-time, pre-start contract.

**Change:** `Init(null)` throws without changing state; later/post-start `Init` returns `false` without invalidating or rebinding; only a valid `Init` that cannot bind invalidates.

**Why:** Abstract §7.1 defines the exact `Init` behavior.

**Files:** `Raw/Reliable/RawReliableClientTransport.cs`, client transport tests.

**Verification:** `Init_NullArgument_ThrowsWithoutChangingLifecycle`, `Init_IsOneTime_SecondCallReturnsFalse`, inverted `Init_*` tests.

### H-06

**Problem:** A failed RawReliable client start invoked the user `onStopped`.

**Change:** `AnyTransport` tracks whether the start succeeded and invokes the user callback only for a post-start stop.

**Why:** A failed initial start must invalidate silently.

**Files:** `AnyTransport.cs`, `AnyTransportTests.cs`.

**Verification:** `Start_WhenTryStartReturnsFalse_InvalidatesTransportWithoutNotifications`, stop-during-start test.

### M-01

**Problem:** The Direct RawUnreliable server never enforced `MessageMaxByteSize` on inbound data.

**Change:** The shared `OnCarrierInbound` releases and logs oversized frames before route lookup, factory, or handler.

**Why:** Abstract §8 forbids invoking a factory or handler for oversized input.

**Files:** `RawUnreliableServerTransport.cs`, Direct server tests.

**Verification:** `OversizedInbound_ForNewRoute_DoesNotInvokeFactoryAndKeepsTransportAlive`, existing-route test.

### M-02

**Problem:** A Direct RawUnreliable peer callback could begin before `Send` returned.

**Change:** `SendReturnBarrier` defers the commit worker until the sending `Send` returns; both Direct endpoints use it.

**Why:** Abstract §10 requires the ordering.

**Files:** `Raw/Unreliable/SendReturnBarrier.cs`, `RawUnreliableEndpoint.cs`, Direct endpoints.

**Verification:** `ClientSendReturnBarrier_DefersTheServerCallbackUntilSendReturns`, server version.

### M-03

**Problem:** A stopped endpoint's startup flush could still deliver buffered messages.

**Change:** The flush and every drain step check the latched stop; a scheduled receive job checks `IsRunning`.

**Why:** §7.2 forbids a new `OnReceived` after `Stop` returns.

**Files:** `RawUnreliableEndpoint.cs`, `RawEndpoint.cs`.

**Verification:** stop-from-`OnStarted`, pre-flush stop, scheduled-receive tests, mid-drain integration test.

### M-04

**Problem:** RawReliable stopped the session on a malformed or unknown frame.

**Change:** Missing or unknown frame types are logged, released, and dropped; later traffic flows.

**Why:** §8 requires discard without stopping.

**Files:** `RawReliableEndpoint.cs`, `RawReliableEndpointTests.cs`.

**Verification:** `ProcessInbound_UnhandledFrameType_IsDroppedWithoutStopping`, empty-frame test.

### M-05

**Problem:** Direct RawReliable had no handshake deadline in either role.

**Change:** Added `IRawReliableHandshakeTimerFactory`; both Direct roles enforce the configured positive deadline, with a manual test factory.

**Why:** §6.3 requires a positive configurable deadline.

**Files:** timer factory, handshake endpoints, admission transport, Direct transports.

**Verification:** manual client/server timeout tests, successful-handshake zero-pending assertions.

### M-06

**Problem:** Direct RawReliable rejected payloads at the top of the declared size range.

**Change:** Transport framing is excluded from the payload limit in both directions; the exact body is checked after the type is popped.

**Why:** §8 defines the limit as the application payload.

**Files:** `RawReliableEndpoint.cs`, `RawEndpoint.cs`, `RawReliableFraming.cs`, Direct and TCP endpoints.

**Verification:** `Payload_at_exact_message_max_is_accepted_in_both_directions`, `Send_PayloadAtExactMessageMaxByteSize_IsAccepted`.

### M-07

**Problem:** A factory-created RawUnreliable handler was abandoned when endpoint creation failed.

**Change:** The admission path captures the handler and a terminal reason, leaves the admission lock, and calls `OnStopped` once.

**Why:** The handler otherwise has no terminal callback and leaks resources.

**Files:** `RawUnreliableServerTransport.cs`, abstract/direct specs, Direct tests.

**Verification:** declined/throw tests assert exactly one notification outside the admission lock.

### M-08

**Problem:** `Channel.Dispose` could abort teardown when a delivery system threw.

**Change:** `SafeRelease` isolates clear/dispose failures; both directions are released.

**Why:** Teardown must complete regardless of a misbehaving system.

**Files:** `Channel/Channel.cs`, channel tests.

**Verification:** `Dispose_WithAThrowingDeliverySystem_StillReleasesTheOtherAndRaisesDisposed`.

### M-09

**Problem:** A channel send/dispose race leaked an owned message.

**Change:** Disposal installs a shared `DiscardingDeliverySystem`, so a racing send still releases its message.

**Why:** Ownership must be released on every path.

**Files:** `DiscardingDeliverySystem.cs`, `Channel.cs`.

**Verification:** `SendRacingDispose_ReleasesTheMessage`.

### M-10

**Problem:** A Direct RawUnreliable netem client could construct delivery threads before start.

**Change:** Delivery factories are stored and materialized only at endpoint creation; the binding builds factories, not systems.

**Why:** A pre-start stop must construct nothing.

**Files:** `UnreliableDeliveryBinding.cs`, `RawUnreliableDirectBinding.cs`, client transport.

**Verification:** binding/transport zero-construction tests.

### M-11

**Problem:** RRReliable `Reject` after a successful `Finish` returned `Error` instead of throwing.

**Change:** Added a distinct `Finished` state; `Reject` throws for `Accepted`/`Finished` while live.

**Why:** §6.6 defines the state precedence.

**Files:** `RRReliableResponse.cs`, server transport tests.

**Verification:** `ResponseHandle_MatrixOfFinishRejectAndClosedCalls`, state tests.

### M-12

**Problem:** A timer-factory exception left a bound RRReliable handler with no TTL.

**Change:** `Request` catches schedule/set failures, removes the request, unbinds, and returns `Error`.

**Why:** A bound handler without a TTL leaks until the transport stops.

**Files:** `RRReliableClientTransport.cs`, client tests.

**Verification:** `Request_WhenTheTimerFactoryThrows_ReturnsErrorAndUnbindsTheHandler`.

### M-13

**Problem:** The Direct RawUnreliable client did not isolate manager/install exceptions.

**Change:** `Connect` is wrapped; the channel is removed, uninstalled systems are released once, both directions reset to fresh perfect systems, and the endpoint stays alive.

**Why:** A transient carrier failure must not fail the endpoint permanently.

**Files:** `RawUnreliableDirectClientEndpoint.cs`, transport.

**Verification:** manager-callback and install-throw tests, retry assertions.

### M-14

**Problem:** `Stopped` event invocation was not per-subscriber fault-isolated.

**Change:** `RaiseStopped` iterates the invocation list and catches each subscriber.

**Why:** One throwing subscriber must not skip the others.

**Files:** `RawEndpoint.cs`, channel/channel-dispose isolation.

**Verification:** throwing stop-subscriber tests.

### L-01

**Problem:** The client did not enforce the declared 100-message outbound budget.

**Change:** Added an admission reservation counter in both Direct endpoints; the 101st pending message returns `BufferOverflow` and is released.

**Why:** The declared queue capacity must bind before delivery.

**Files:** `RawEndpoint.cs`, Direct client/server endpoints.

**Verification:** 100/101-message tests, empty-message test.

### L-02

**Problem:** Send-result precedence was wrong in several shared paths.

**Change:** State is checked first, then null/alive, size, and capacity in all four shared paths.

**Why:** §9 defines that order.

**Files:** `RawEndpoint.cs`, `RawReliableEndpoint.cs`, `RawUnreliableServerEndpoint.cs`, RR transports.

**Verification:** combined-condition endpoint tests.

### L-03

**Problem:** The reliable-debug control had contract and race problems.

**Change:** Client-only debug mode, lock-held transition, pre-start stop resets, and a before-start-lock test hook.

**Why:** The mode must not tear across a start and the server cannot enforce it.

**Files:** Direct client transport, `AnyTransport.cs`, conformance adapters.

**Verification:** `TryMakeReliable_BeforeStartLock_AppliesToEveryEndpoint`, `TryMakeReliable_AfterStart_ReturnsFalse`, server `false` test.

### L-04

**Problem:** RawUnreliable conformance gate details did not match the implementation.

**Change:** The handler-stop gate now sits in the forwarding path; the client commit gate checks a disposed channel first; oversized inbound skips the factory gate.

**Why:** The control table must describe the real hit/not-hit conditions.

**Files:** `RawUnreliableEndpoint.cs`, Direct client endpoint, server transport.

**Verification:** gate hit/not-hit tests, oversized factory-not-called test.

### L-05

**Problem:** Handshake reject/response frames were not size-checked.

**Change:** Handshake frames are checked on the complete framed size, including the type byte; an oversized reject falls back to a minimal reject.

**Why:** §6.3 bounds every handshake frame.

**Files:** handshake endpoints, Direct server transport, binding tests.

**Verification:** oversized request/response/reject tests, binding parameter tests.

### L-06

**Problem:** RawReliable could expose a payload before `OnConnected`.

**Change:** A connect-completed latch buffers payloads and drains them after the callback; stop and overflow release them.

**Why:** §6.5 forbids a payload before `OnConnected`.

**Files:** `RawReliableEndpoint.cs`, endpoint tests.

**Verification:** pre-`OnConnected` FIFO, concurrent-arrival, throwing-callback, overflow, stop tests.

### L-07

**Problem:** The binding advertised an unusable `handshake_max_byte_size` range.

**Change:** The range is `1..int.MaxValue`; the binding rejects a handshake maximum above the message maximum and exposes the message/buffer/timeout parameters.

**Why:** Advertised parameters must be accepted by the constructors.

**Files:** `RawReliableDirectBinding.cs`.

**Verification:** `RawReliableDirectBindingParametersTests`.

### L-08

**Problem:** The binding built two delivery systems and leaked when the second build failed.

**Change:** The binding builds two independent factories; no system is constructed before start; builder failures construct nothing.

**Why:** Construction belongs to endpoint creation.

**Files:** `UnreliableDeliveryBinding.cs`, `RawUnreliableDirectBinding.cs`.

**Verification:** binding zero-construction and builder-throw tests.

### L-09

**Problem:** Endpoint construction failure leaked handed-off delivery systems.

**Change:** `CreateEndpoint` releases materialized systems once and resets both directions when construction throws.

**Why:** A retry must not reuse a released instance.

**Files:** Direct client transport, endpoint.

**Verification:** `EndpointFactoryThrow_ReleasesExplicitPairExactlyOnce`.

### L-10

**Problem:** The RRUnreliable Direct client returned `Ok` when its carrier was already stopped.

**Change:** A null carrier releases the frame and returns `Error`; only a live carrier with no peer keeps the no-peer policy.

**Why:** The core must unbind the handler on a stopped carrier.

**Files:** `RRUnreliableDirectClient.cs`.

**Verification:** `SendFrame_WithANullCarrier_ReturnsErrorAndReleasesTheFrame`.

### L-11

**Problem:** The pre-start pending bound ignored the configured queue capacity.

**Change:** `OnInbound` compares against `_queueCapacity`; the pending list uses the same value.

**Why:** Configuration must control the bound.

**Files:** `RRUnreliableDirectServer.cs`.

**Verification:** capacity 10/200 wiring test.

### L-12

**Problem:** RRUnreliable deviations were misstated.

**Change:** Deviation 1 now names a running `OnRequested`; deviations 1/2 say the handle is closed lazily and the observable result is `Error`; deviation 4 says closer to ten seconds for two netem systems.

**Why:** Declared deviations must describe the implementation.

**Files:** `RR/Unreliable/Specification.md`.

**Verification:** manual review of §7.

### L-13

**Problem:** Direct RawUnreliable claims were contradicted by several findings.

**Change:** Stop reason, send-return ordering, post-stop suppression, oversized inbound, and connect isolation are fixed; §7 declares the client-only debug and bounded server reply queue residuals.

**Why:** The specification must match the code.

**Files:** `Raw/Unreliable/Specification.md`.

**Verification:** contract tests plus a §7/§8 review.

### L-14

**Problem:** Direct RawReliable claims were contradicted by the missing timer and size behavior.

**Change:** §5 lists the new parameters, §6 describes the deadline, size checks, and buffering, and §7 no longer claims an infinite deadline.

**Why:** The specification must match the code.

**Files:** `Raw/Reliable/Specification.md`.

**Verification:** handshake/size tests plus a section review.

### L-15

**Problem:** The RRReliable completion row said the response TTL sends nothing.

**Change:** The row now states that the TTL sends `Finish(Failed)`; only a session close sends nothing.

**Why:** The spec contradicted the implementation and abstract §6.5.

**Files:** `RR/Reliable/Specification.md`.

**Verification:** `ResponseTtl_ProducesOneFailedCompletionAndDisposesTheRequestTimer`, document review.

### L-16

**Problem:** Non-table lines in the RRUnreliable spec exceeded 100 characters.

**Change:** The offending paragraphs were rewrapped.

**Why:** The specification form rule requires it.

**Files:** `RR/Unreliable/Specification.md`.

**Verification:** `awk 'length > 100 && $0 !~ /^[[:space:]]*\|/'` returns nothing.

### L-17

**Problem:** Netem accepted negative configuration values.

**Change:** `NetemConfig.Validate` rejects every negative duration, overhead, queue limit, and slot field; the binding schema minima are zero.

**Why:** Invalid configuration must fail before construction.

**Files:** `NetemConfig.cs`, `UnreliableDeliveryBinding.cs`.

**Verification:** `Validate_RejectsEveryNegativeField`, binding rejection tests.

### L-18

**Problem:** Netem duplication could exceed `QueueLimit`.

**Change:** The duplicate count is clamped to the available room; a full queue releases the packet.

**Why:** The queue limit must be an upper bound.

**Files:** `NetemDeliverySystem.cs`.

**Verification:** `QueueLimit_ClampsDuplicationToTheAvailableRoom`.

### L-19

**Problem:** Netem release was not repeat-safe and `Dispose` was not thread-safe.

**Change:** `Dispose` uses an interlocked one-shot gate, `Clear` is a safe no-op after dispose, and a never-started thread is not joined.

**Why:** Repeated/concurrent release must not throw.

**Files:** `NetemDeliverySystem.cs`.

**Verification:** `ClearAndDispose_AreRepeatSafe`, `Dispose_WithQueuedMessages_ReleasesThem`.

### L-20

**Problem:** `ChannelManager.Connect` left a channel registered when the server callback threw.

**Change:** The client entry is removed (and the channel disposed) and `Connect` returns `null`.

**Why:** A failed connect must leave no registry entry.

**Files:** `ChannelManager.cs`.

**Verification:** `Connect_WhenTheServerCallbackThrows_ReturnsNullAndLeavesNoClientEntry`.

### L-21

**Problem:** The same delivery system in both directions was released twice.

**Change:** Disposal and install deduplicate by reference.

**Why:** Ownership is one release per instance.

**Files:** `Channel.cs`.

**Verification:** `Dispose_WithTheSameSystemInBothDirections_ReleasesItOnce`.

### L-22

**Problem:** Direct `id` was shared across protocols.

**Change:** The registry is keyed by `(scheme, IEndPoint)`; every Direct carrier passes its scheme.

**Why:** Cross-protocol ids must be isolated.

**Files:** `ChannelManager.cs`, Raw/RR Direct wires and tests.

**Verification:** `RegisterServer_SameIdUnderDifferentSchemes_BothStartAndStayIsolated`.

### L-23

**Problem:** `help.md` used a non-existent namespace.

**Change:** The Quick Start now uses `Pontifex.VirtualDelivery` and `Pontifex.VirtualDelivery.Netem`.

**Why:** The example must compile conceptually.

**Files:** `VirtualDelivery/Netem/help.md`.

**Verification:** manual compile review.

### L-24

**Problem:** The VirtualDelivery README omitted `NetemDeliverySystem` and `SlotScheduler`.

**Change:** The root subject, layout, groups, manual-pump statement, and out-of-scope sections now match the tests.

**Why:** Suite documentation must match its contents.

**Files:** `Tests/Pontifex.Transport.Direct.Tests/VirtualDelivery/README.md`.

**Verification:** README review against the directory.

### L-25

**Problem:** Direct tests polled wall-clock time instead of using scheduling seams.

**Change:** The default run excludes every declared integration fixture; a static NUnit guard fails if a default fixture polls or starts an unseamed thread; CI runs filtered and integration passes; suite READMEs document both commands.

**Why:** The default suite must be deterministic and the threaded facets clearly declared.

**Files:** `DirectTestDeterminismTests.cs`, Direct suite READMEs, workflow, fixture categories.

**Verification:** default run three times, integration run, exclusion control, guard.

### L-26

**Problem:** The Netem drop test justified its assertion with an invalid one-hour-latency rationale.

**Change:** It now uses the manual pump and asserts a synchronous release with no timing assumption.

**Why:** The test must not depend on a latency value to distinguish drop from queue.

**Files:** `NetemDeliverySystemTests.cs`.

**Verification:** `SaturatedLossWithDuplication_DropsThePacket`.

<a id="tests-and-specifications"></a>
## Tests and specifications

New tests include shared lifecycle and handler-isolation tests, RawUnreliable
routing/stop/size/gate tests, the send-return barrier and 100-message admission
tests, delivery-system ownership and debug-mode tests, RawReliable framing,
pre-connect buffering, payload-boundary and handshake-deadline tests, a
deterministic Netem pipeline suite, channel fault-isolation and protocol-scope
tests, RRReliable session stress and response-state tests, RRUnreliable
stop-race/capacity tests, and the Direct determinism guard.

Changed specifications: the abstract RawUnreliable §6.2/§7.4/§7.6, the Direct
RawUnreliable, RawReliable, RR Unreliable, and RR Reliable specifications, and
the TCP RawReliable specification. Suite documentation was updated for
Endpoints, RawUnreliable endpoint/conformance, ChannelManager, VirtualDelivery,
and every Direct suite README.

<a id="residual-deviations"></a>
## Residual deviations

- Reliable debug mode is client-only for Direct. The server reports it as
  unsupported, and server replies stay on the bounded commit queue, so
  server-to-client FIFO is not unconditional under queue saturation.
- A Netem pre-connect/pipeline buffer cap overflow terminates the session with
  `TextFail` rather than dropping silently; this is documented in the
  RawReliable specification.
- TCP declares one bounded deviation: an unrecoverable length-prefix error that
  cannot be resynchronized tears down the socket. Undecodable frames with a
  valid prefix are logged and dropped.
- The default Direct run excludes fixtures carrying an `*.Integration`
  category. Those fixtures are deterministic where gates exist, but they use
  real threads or timer-like waits and are run by the separate integration
  command.
- `plan.md` remains in the tree; the orchestrator deletes it after this report.
