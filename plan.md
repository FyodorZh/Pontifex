# Implement the RR Branch of the Transports Abstract Test Suite

## Goal

After this plan is complete, `Tests/Pontifex.Tests/Transports` covers the full abstract
transport hierarchy rooted at `Pontifex.AnyTransport`, including the `RRTransport` shared
base and both RR families (`RRReliable` and `RRUnreliable`). The existing RRReliable
abstract suite is moved out of `Tests/Pontifex.Transport.Direct.Tests` into the Transports
suite, the RRUnreliable abstract family gains dedicated deterministic coverage, and all
suite documentation, categories, layer tables, and coverage files reflect the new
structure. The concrete Direct RR suites remain in place and keep passing.

## Context

- `Tests/Pontifex.Tests/Transports` is an Abstract Test Suit rooted at
  `Pontifex.AnyTransport`. It currently covers only the Raw branch
  (`RawTransport`, `RawReliableTransport`, `RawUnreliableTransport`).
- The RRReliable abstract suite currently lives under
  `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport` with layers:
  `RRReliableTransport`, `RRReliableTransport<THandler>`, `RRReliableClientTransport`,
  and `RRReliableServerTransport`.
- The RRUnreliable abstract classes (`RRUnreliableTransport`,
  `RRUnreliableTransport<THandler>`, `RRUnreliableClientTransport`,
  `RRUnreliableServerTransport`) have no dedicated abstract suite. They are exercised
  indirectly through `Tests/Pontifex.Transport.Direct.Tests/RR/RRUnreliableDirect`.
- `Pontifex/Pontifex.csproj` grants `InternalsVisibleTo` to both `Pontifex.Tests` and
  `Pontifex.Transport.Direct.Tests`, so internal RR frames, request tables, and test seams
  are accessible after the move.
- The Raw branch already establishes the suite conventions: hand-written doubles,
  deterministic manual queues/timers, public-plus-protected assertions, `Transports`
  categories, and per-layer `Coverage.md`.
- The working tree is clean at `dc926bd more tests`, which contains the previously
  implemented AnyTransport/admission/unreliable-server/endpoint-client tests.

## Constraints

- Test-only. Do not modify production code. If a test exposes a production defect, stop
  and report the failing test, expected/actual behaviour, and file/line evidence.
- Follow `Tests/RULES.md` for suite structure, layers, coverage, harness, categories, and
  test construction.
- NUnit 4; global `using NUnit.Framework`.
- Hand-written test doubles only. No mocking library.
- Every suite fixture carries `[Category("Transports")]`.
- RR fixtures additionally carry `[Category("Transports.RR")]`; RRReliable fixtures carry
  `[Category("Transports.RR.Reliable")]`; RRUnreliable fixtures carry
  `[Category("Transports.RR.Unreliable")]`. Client/server subcategories may be added.
- Genuinely threaded tests carry `[Category("Transports.Concurrency")]`.
- Remove `Direct.*` categories from moved abstract tests.
- Test names use `Scenario_ExpectedResult`.
- Use the suite root namespace `Pontifex.Tests.Transports` for test files and harness
  unless a concrete name collision requires a documented sub-namespace.
- Keep the default run deterministic: no sleeps, real timers, sockets, or uncontrolled
  threads. Use manual queues, clocks, and gated/joined threads with bounded waits.
- Preserve buffer ownership: release every `UnionDataList` the test owns; clone only when
  inspection must outlive the contract's release point.
- Update the owning layer's `Coverage.md` in the same stage as the tests it records.
- Do not create a new shared test-support project. Copy the small `ManualTimerFactory`
  into the Transports harness; keep the Direct copy for Direct tests.
- Do not modify the plan file after implementation starts.
- Do not commit unrelated files or multiple stages in one commit.

## Non-Goals

- Implementing or changing production RR transport behaviour.
- Adding the RR conformance adapter/tests tracked separately in `Pontifex/TODO.md`.
- Changing concrete RR carrier suites (`RRReliableDirect`, `RRUnreliableDirect`) beyond
  moving the abstract `RRReliableServerSession` stress tests to the CoreHelpers suite and
  updating documentation/categories.
- Reworking RRReliable core helper tests beyond the session stress-test relocation.
- Covering Raw transports again or changing the existing Raw layers.

## Assumptions

- The existing RRReliable abstract tests are correct and can be moved without weakening
  assertions. Evidence: they pass in `Tests/Pontifex.Transport.Direct.Tests`.
- RRUnreliable abstract behaviour can be tested deterministically through a concrete test
  double and local scripted send/receive seams. Evidence: the production classes expose
  protected `SendFrame`/`OnFrameReceived` and internal test hooks, and
  `InternalsVisibleTo` includes `Pontifex.Tests`.
- The `RRReliableServerSession` stress tests belong to `RRReliableCoreHelpers`, not the
  transport server layer. Evidence: they construct `RRReliableServerSession` directly and
  are documented as a multithreaded stress fixture for the session maps.
- Duplicating `ManualTimerFactory` is acceptable because it is a small test-only helper and
  both test projects need it independently.
- The user has pre-authorized implementation after the plan refinement phase; no further
  permission prompt is required after the audit loop.

## Prerequisites

- .NET SDK capable of `net10.0` tests and `netstandard2.1` library builds.
- Clean working tree at the start of execution.
- `Tests/Pontifex.Tests` and `Tests/Pontifex.Transport.Direct.Tests` build and pass before
  Stage 1.

## Stages

### Stage 1 — Relocate RRReliable and add the RRTransport layer

**Objective**

The Transports suite contains the `RRTransport` layer and the RRReliable family layers;
shared RRTransport tests are separated from reliable-specific tests; the old Direct
abstract suite is removed; the RRReliableServerSession stress tests move to the Direct
CoreHelpers suite.

**Inputs**

- Clean working tree.
- Existing `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport` suite.
- Existing Transports README/Coverage and Raw layers.

**Actions**

1. Create `Tests/Pontifex.Tests/Transports/RR/Coverage.md`,
   `Tests/Pontifex.Tests/Transports/RR/RRTransportTests.cs`, and
   `Tests/Pontifex.Tests/Transports/RR/Harness/TestRRTransport.cs`.
2. Add a concrete `TestRRTransport : Pontifex.RR.RRTransport` double with:
   - configurable `MessageMaxByteSize` and `FrameOverheadBytes`;
   - configurable `StartCarrier` result/throw and recorded `StartCarrierCalls`;
   - recorded `StopCarrierCalls` and `LastStopReason`;
   - recorded `ClearPendingRequests` count and optional callback;
   - exposed protected `TryValidatePayload`, `NextServerRequestId`, `FrameMaxByteSize`, and
     running snapshot.
3. Move the existing RRReliable suite files:
   - `Harness/ScriptedReliableCarrier.cs` → `Transports/RR/Harness/ScriptedReliableCarrier.cs`;
   - `Harness/TestRRReliableTransport.cs` → `Transports/RR/Reliable/Harness/TestRRReliableTransport.cs`;
   - `Generic/*` → `Transports/RR/Reliable/Generic/*`;
   - `Client/*` → `Transports/RR/Reliable/Client/*`;
   - `Server/*` → `Transports/RR/Reliable/Server/*`.
4. Split `RRReliableTransportTests.cs`:
   - Move shared lifecycle, payload-validation, frame-size formula, and
     server-request-id tests into `RRTransportTests.cs`, retargeted at `TestRRTransport`.
     Cover: one-time start/carrier once; pre-start stop; running stop clears pending
     before carrier stop with the same reason; start carrier false; start carrier throws /
     `AbortCarrier`; payload validation precedence (state before null/size, started null,
     valid, oversized, serialization failure); `NextServerRequestId` starts at one and
     increments.
   - Keep in `RRReliableTransportTests.cs`: `Type_IsRRReliable`, the reliable
     `FrameMaxByteSize` assertion using `RRReliableFrame.MaxOverheadBytes`, and a
     timer-factory injection/default test if not already covered.
   - Move `Stop_SchedulesTerminalWorkBeforeOnStoppedBegins` to `RRTransportTests.cs` and
     replace its timer-factory dependency with a local scheduled-action probe. Keep a
     reliable-specific timer-factory test in `RRReliableTransportTests.cs` only if it
     adds reliable delta coverage.
5. Copy `Tests/Pontifex.Transport.Direct.Tests/RR/TestSupport/ManualTimerFactory.cs` to
   `Tests/Pontifex.Tests/Transports/RR/Harness/ManualTimerFactory.cs`. Keep the Direct copy.
6. Move `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport/Server/RRReliableServerSessionStressTests.cs`
   to `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableCoreHelpers/Tests/RRReliableServerSessionTests.cs`.
   Rename the fixture to `RRReliableServerSessionTests`, keep both tests, and categorize
   it `Direct.RRReliableCoreHelpers`. Update `RRReliableCoreHelpers/README.md` and
   `Coverage.md` to add `RRReliableServerSession` to the suite subject and coverage.
7. Update namespaces in moved files to `Pontifex.Tests.Transports` (or a documented
   sub-namespace only if a name collision occurs). Remove all `Pontifex.Tests.RR.*`
   namespaces from moved files.
8. Update categories on every moved fixture:
   - `[Category("Transports")]`;
   - `[Category("Transports.RR")]`;
   - `[Category("Transports.RR.Reliable")]`;
   - add `.Client`/`.Server` where useful;
   - add `[Category("Transports.Concurrency")]` only to tests that use real gated threads;
   - remove `Direct.*` categories.
9. Delete the old `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport` directory.
10. Update `Tests/Pontifex.Tests/Transports/README.md`:
    - extend the selected inheritance tree with the RR branch;
    - add layer-table rows for `RRTransport`, `RRReliableTransport`,
      `RRReliableTransportGeneric`, `RRReliableClientTransport`, and
      `RRReliableServerTransport`;
    - update the directory layout and harness layout;
    - add RRTransport and RRReliable contract matrices;
    - add categories, filter commands, and out-of-scope entries;
    - record the RRUnreliable layers as planned for Stages 2–4.
11. Update `Tests/Pontifex.Tests/Transports/RR/Coverage.md` and every moved Coverage file:
    - move existing rows with the tests;
    - add rows for the shared tests now owned by RRTransport;
    - add rows for any existing RRReliable tests missing from the old coverage tables:
      `Request_WhenTheTimerFactoryThrows_ReturnsErrorAndUnbindsTheHandler`,
      `Finish_WhenTheSendFails_LeavesTheHandleRetryable`,
      `Reject_WhenTheSendFails_LeavesTheHandleRetryable`,
      `Reject_AfterTheResponseTtlClosedTheHandle_ReturnsError`.
12. Update Direct documentation and category references:
    - search for `RRReliableTransport` and `Direct.RRReliableTransport` outside the moved
      files;
    - remove or redirect references to the deleted abstract suite;
    - keep `RRReliableDirect`, `RRUnreliableDirect`, and `RRReliableCoreHelpers` intact.

**Verification**

Run:

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports.Concurrency
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj
git grep -n "Direct.RRReliableTransport"
```

Expected:

- build succeeds with no errors;
- Transports filter passes; total count increases by the moved RRReliable transport tests
  (75 tests: 15 root, 8 generic, 30 client, 22 server), excluding the 2 stress tests that
  move to Direct CoreHelpers;
- Transports.Concurrency passes;
- Direct test project passes;
- the `git grep` finds no remaining `Direct.RRReliableTransport` category or live
  documentation reference;
- the old `RRReliableTransport` directory no longer exists.

**Definition of done**

The RRTransport and RRReliable layers exist under `Transports/RR`, all moved tests pass
under the new categories, the Direct abstract suite is gone, and the CoreHelpers suite
owns the session stress tests.

**Risks / notes**

- Name collisions between Raw harness and moved RR harness in
  `Pontifex.Tests.Transports` must be resolved by renaming the harness type, not by
  reverting to layer-specific namespaces unless necessary.
- `Stop_SchedulesTerminalWorkBeforeOnStoppedBegins` must not depend on
  `ManualTimerFactory` after moving to the shared RRTransport layer; use a local probe.
- Do not leave `Direct.Integration` on moved abstract unit fixtures.

### Stage 2 — Add RRUnreliable base and generic layers

**Objective**

The Transports suite contains `RRUnreliableTransport` and
`RRUnreliableTransport<THandler>` layers with deterministic tests for their delta over
RRTransport.

**Inputs**

- Stage 1 complete.
- `RRUnreliableTransport` production source and RR shared/unreliable specifications.

**Actions**

1. Create `Transports/RR/Unreliable/Coverage.md`,
   `Transports/RR/Unreliable/RRUnreliableTransportTests.cs`, and
   `Transports/RR/Unreliable/Harness/TestRRUnreliableTransport.cs`.
2. Create `Transports/RR/Unreliable/Generic/Coverage.md`,
   `Transports/RR/Unreliable/Generic/RRUnreliableTransportGenericTests.cs`, and
   `Transports/RR/Unreliable/Generic/Harness/TestRRUnreliableTransportGeneric.cs`.
3. Add the RRUnreliable base tests:
   - `Type_IsRRUnreliable` — `Type == TransportType.RRUnreliable`;
   - `FrameMaxByteSize_UsesTheUnreliableOverhead` — message limit +
     `RRUnreliableFrame.OverheadBytes` + 1;
   - `Start_UsesTheSharedRRCarrierLifecycle` — the inherited `RRTransport` lifecycle is
     exercised once through the unreliable double, without repeating all RRTransport
     tests.
4. Add the generic-layer tests:
   - `Init_WithNull_Throws`;
   - `Init_IsOneTime`;
   - `Init_AfterStartAttempt_ReturnsFalse`;
   - `Init_BeforeStart_DoesNotStartCarrier`;
   - `Start_WithoutInit_ReturnsFalseAndInvalidates`;
   - `DispatchHandler_IsGatedByStartAndStop`;
   - `Start_WhenCarrierReturnsFalse_LeavesHandlerUndispatched`;
   - `Stop_WhileRunning_HidesHandlerBeforeOnStopped`;
   - `Init_AfterStop_ReturnsFalse`.
5. Update `Transports/RR/Unreliable/Coverage.md`, the Generic coverage file, and the
   Transports README:
   - add both layer rows;
   - remove their planned entries from the RRTransport/RR branch planned table;
   - add the RRUnreliable contract summary for the base and generic layers;
   - add directory and harness entries.

**Verification**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports
```

Expected: build succeeds; Transports filter passes with the new RRUnreliable base/generic
tests included.

**Definition of done**

Both RRUnreliable base and generic layers exist, have coverage files and tests, and pass
in the Transports run.

**Risks / notes**

- The generic layer's `OnStopped` sets `_handler = null`; assert through
  `DispatchHandler`, not through private state.
- Do not duplicate the RRTransport shared lifecycle tests in these layers.

### Stage 3 — Add the RRUnreliableClientTransport layer

**Objective**

The Transports suite covers the RRUnreliable client request/pending/response contract
deterministically through a concrete test double.

**Inputs**

- Stage 2 complete.
- `RRUnreliableClientTransport`, `RRUnreliableRequestTable`, `RRUnreliableFrame`,
  `RRRequestHandler`, and the RRUnreliable specification.

**Actions**

1. Create `Transports/RR/Unreliable/Client/Coverage.md`,
   `RRUnreliableClientTransportTests.cs`, and `Harness/` files:
   - `TestRRUnreliableClientTransport.cs` — concrete client with configurable random seed
     (including a throwing generator), configurable `SendFrame` result/throw/capture,
     exposed `OnFrameReceived`, `PendingCountForTests`, and the three internal test hooks;
   - `RecordingUnreliableClientHandler.cs` — derives from `RRRequestHandler`, implements
     `IRRUnreliableRequestHandler`, records response ids/counts, can throw or cancel from a
     callback, and releases received buffers;
   - `ManualClock.cs` — deterministic `IDateTimeProvider`;
   - `TestMessages.cs` — integer payload and exact-size payload builders.
2. Add client tests:
   - `Constructor_WithNullServerEndPoint_Throws`;
   - `Constructor_WithNonPositiveRequestTtl_Throws`;
   - `Constructor_ExposesServerEndPointAndRequestTtl`;
   - `Start_IsOneTimeAndUsesTheRandomSeed`;
   - `Start_WhenRandomSeedGenerationThrows_ReturnsFalseWithoutStartingCarrier`;
   - `Request_WithNullHandler_ThrowsBeforeConsumingPayload`;
   - `Request_WithNonRRHandler_ThrowsBeforeConsumingPayload`;
   - `Request_WithAlreadyBoundHandler_Throws`;
   - `Request_AfterStop_ReturnsNotConnectedAndReleases`;
   - `Request_WithNullPayload_ReturnsInvalidMessage`;
   - `Request_AboveMessageLimit_ReturnsMessageTooBigAndReleases`;
   - `Request_WhenHandlerCancelsBeforePublish_ReturnsErrorAndReleases`;
   - `Request_WhenStopRacesBeforeSend_ReturnsErrorAndUnbinds`;
   - `Request_WhenSendFrameReturnsNonOk_ReturnsResultAndUnbindsWithoutCallback`;
   - `Request_WhenSendFrameThrows_ReturnsErrorAndUnbindsWithoutCallback`;
   - `RequestId_IsPublishedBeforeSendFrame`;
   - `RequestIds_StartAtTheRandomSeedAndWrap`;
   - `Response_FromForeignRoute_IsReleasedWithoutCallback`;
   - `Response_WhenMalformed_IsReleasedWithoutCallback`;
   - `Response_WhenOversized_IsReleasedWithoutCallback`;
   - `Response_ForUnknownId_IsReleasedWithoutCallback`;
   - `Response_AfterRequestTtlExpires_IsDroppedAndHandlerUnbound`;
   - `Response_FirstFrameFixesMessageCount`;
   - `Response_WithInconsistentMessageCount_IsDropped`;
   - `Response_WithMessageIdAtOrAboveCount_IsDropped`;
   - `Response_WithDuplicateMessageId_IsDropped`;
   - `Response_LastMessage_UnbindsHandlerAfterCallback`;
   - `Response_WhenHandlerThrows_IsIsolatedAndStillCompletes`;
   - `Cancel_RemovesPendingRequestWithoutCallback`;
   - `Cancel_BeforeSendAbortsTheRequest`;
   - `Cancel_WhenLastMessageCallbackActive_DefersUnbind`;
   - `Stop_ClearsPendingWindowWithoutCallbacks`;
   - `Stop_WhenRequestRacesStop_ReturnsErrorOrNotConnectedAndUnbinds`;
   - `Response_WhenTransportStopped_IsDropped`.
3. If `BufferOverflow` cannot be produced without a public prefill seam, record it under
   `Deferred by design` with the reason that the table is internal and the only public
   path would need 65,536 accepted requests. Do not use reflection.
4. Update the layer Coverage.md, Transports README layer table/directory/harness,
   RRUnreliable contract matrix, and remove the client planned entry.

**Verification**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports.Concurrency
```

Expected: build succeeds; all client tests pass; no real timers, sockets, or sleeps are
introduced.

**Definition of done**

The RRUnreliable client layer exists with deterministic coverage of request acceptance,
pending-window TTL/cancel/stop, response correlation, message-count fixing, duplicate
suppression, callback isolation, and send-result paths.

**Risks / notes**

- `Request` may call the response callback before returning; tests must use the callback
  signal and the captured return value, not assume ordering.
- `Response` callbacks run under the state lock; a test that cancels from inside a
  callback must not deadlock.
- Release every captured response payload; the handler owns received frames.

### Stage 4 — Add the RRUnreliableServerTransport layer and finalize the RR branch

**Objective**

The Transports suite covers the RRUnreliable server dispatch, response handle, message
sequence, TTL, and stop contract; the RR branch documentation is complete and the full
solution is green.

**Inputs**

- Stage 3 complete.
- `RRUnreliableServerTransport`, `RRUnreliableResponse`, `RRUnreliableFrame`, and the
  RRUnreliable specification.

**Actions**

1. Create `Transports/RR/Unreliable/Server/Coverage.md`,
   `RRUnreliableServerTransportTests.cs`, and `Harness/` files:
   - `TestRRUnreliableServerTransport.cs` — concrete server with configurable
     `SendFrame` result/throw/capture, exposed `OnFrameReceived`, and a manual clock;
   - `RecordingUnreliableServerHandler.cs` — implements `IRRUnreliableServerHandler`,
     records requests and handles, accepts/rejects/throws, and releases requests;
   - reuse the Stage 3 manual clock and message builders by moving them to the nearest
     common ancestor `Transports/RR/Harness` if both client and server layers need them.
2. Add server tests:
   - `Constructor_WithNonPositiveResponseTtl_Throws`;
   - `Constructor_ExposesResponseTtl`;
   - `OnFrameReceived_WithNullFrame_DoesNothing`;
   - `OnFrameReceived_WithMalformedFrame_ReleasesAndDoesNotPresent`;
   - `OnFrameReceived_WithOversizedFrame_ReleasesAndDoesNotPresent`;
   - `OnFrameReceived_BeforeStart_ReleasesWithoutPresenting`;
   - `OnFrameReceived_WhenHandlerNotInitialized_ReleasesWithoutPresenting`;
   - `OnRequested_WhenHandlerAccepts_KeepsHandleOpen`;
   - `OnRequested_WhenHandlerRejects_ClosesHandleAndLaterResponseReturnsError`;
   - `OnRequested_WhenHandlerThrows_LogsRejectsAndTransportStaysValid`;
   - `OnRequested_AssignsServerGlobalIdsFromOne`;
   - `OnRequested_IsSerialized`;
   - `Response_SendsClientRequestIdMessageIdAndCount`;
   - `Response_DefaultMessageCountIsOne`;
   - `SetMessageCount_AcceptsOneTo256`;
   - `SetMessageCount_RejectsOutOfRange`;
   - `SetMessageCount_IsOneTimeBeforeFirstResponse`;
   - `SetMessageCount_AfterFirstResponseThrows`;
   - `SetMessageCount_AfterCloseThrows`;
   - `SetMessageCount_AfterExpiryThrows`;
   - `SetMessageCount_AfterStopThrows`;
   - `Response_WhenSendFrameReturnsNonOk_DoesNotAdvanceAndCanRetry`;
   - `Response_WhenSendFrameThrows_ReturnsErrorAndCanRetry`;
   - `Response_WithNullPayload_ReturnsInvalidMessage`;
   - `Response_WithOversizedPayload_ReturnsMessageTooBigAndReleases`;
   - `Response_AfterLastMessage_ReturnsError`;
   - `Response_AfterResponseTtlExpires_ReturnsErrorAndCloses`;
   - `Response_AfterTransportStopped_ReturnsError`;
   - `OnFrameReceived_AfterStop_ReleasesWithoutPresenting`;
   - `Response_ConcurrentCallsProduceOneSequence` (categorized
     `Transports.Concurrency`).
3. Update the layer Coverage.md, Transports README layer table/directory/harness,
   RRUnreliable contract matrix, categories, and planned/deferred tables.
4. Finalize the Transports README:
   - selected tree shows the complete RR branch;
   - every RR layer has a row and a coverage link;
   - RR contract matrices for shared, reliable, unreliable client, and unreliable server;
   - categories and commands;
   - out-of-scope: concrete RR carriers, Direct RR suites, conformance, real timing.
5. Ensure no `Planned` entry claims a layer that now exists. Record any genuinely missing
   test under the owning layer's `Planned` or `Deferred by design`.
6. Update Direct docs only where they reference moved files/categories; keep concrete
   suites intact.

**Verification**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports.Concurrency
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports.RR
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports.RR.Unreliable
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj
```

Expected: all commands pass; the RR filters run only RR fixtures; no `Direct.*` category
is attached to a Transports fixture; the Direct project still passes.

**Definition of done**

The RR branch is fully represented in the Transports suite, RRUnreliable server coverage
is complete, the README and every Coverage file match the tests, and the full verification
set is green.

**Risks / notes**

- `RRUnreliableResponse` is internal; tests use the public `IRRUnreliableResponse` surface
  and assert captured encoded frames.
- `SetMessageCount` range validation must precede state validation; assert the exception
  type for both an out-of-range value and a closed handle.
- A response send that returns non-`Ok` does not commit the message id; retry must reuse
  the same id.
- The server-global id is local bookkeeping; assert it through the response handle's
  `RequestId`, not through the wire.

## Global Definition of Done

- `Tests/Pontifex.Tests/Transports` is an Abstract Test Suit that covers the full
  `AnyTransport` tree: Raw and RR.
- The RRReliable abstract suite no longer exists in
  `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport`; its layers live under
  `Tests/Pontifex.Tests/Transports/RR/Reliable`.
- `RRTransport`, `RRReliableTransport`, `RRReliableTransport<THandler>`,
  `RRReliableClientTransport`, `RRReliableServerTransport`, `RRUnreliableTransport`,
  `RRUnreliableTransport<THandler>`, `RRUnreliableClientTransport`, and
  `RRUnreliableServerTransport` each have a layer, tests, `Coverage.md`, and a README row.
- Every moved/added fixture carries the `Transports` category and the RR subcategories,
  and no moved fixture carries a `Direct.*` category.
- `RRReliableServerSessionTests` lives in the Direct CoreHelpers suite and the CoreHelpers
  README/Coverage list it.
- The full verification set passes:
  - solution build;
  - `Transports`;
  - `Transports.Concurrency`;
  - `Transports.RR`;
  - `Transports.RR.Unreliable`;
  - full `Pontifex.Tests`;
  - full `Pontifex.Transport.Direct.Tests`.
- No production files are modified.
- The plan file is deleted and the deletion committed at completion.
