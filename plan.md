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
- Remove `Direct.*` categories from moved abstract tests. The exception is the
  `RRReliableServerSession` stress tests relocated to the Direct CoreHelpers suite: they
  keep `Direct.RRReliable.Integration` and gain `Direct.RRReliableCoreHelpers`.
- Test names use `Scenario_ExpectedResult`.
- Do not add `[TestCase]`/parameterized tests in the new RR layers; loop inside one test
  instead, so the expected `Passed:` counts equal the listed test methods.
- Use the suite root namespace `Pontifex.Tests.Transports` for test files and harness
  unless a concrete name collision requires a documented sub-namespace. The relocated
  CoreHelpers stress test uses `Pontifex.Tests.RR.RRReliableCoreHelpers`.
- Keep the default run deterministic: no sleeps, real timers, sockets, or uncontrolled
  threads. Use manual queues, clocks, and gated/joined threads with bounded waits.
- Preserve buffer ownership: release every `UnionDataList` the test owns; clone only when
  inspection must outlive the contract's release point.
- Update the owning layer's `Coverage.md` in the same stage as the tests it records.
- Do not create a new shared test-support project. Copy the small `ManualTimerFactory`
  into `Transports/RR/Reliable/Harness`; keep the Direct copy for Direct tests.
- Do not revise or rewrite the plan after implementation starts. The only permitted plan
  change is the final deletion at completion.
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
- The RRUnreliable client has no injectable request-id seed: `CreateRandomRequestId` is
  `private static` and `_nextRequestId` is private. Seed determinism, wraparound, and
  generator-failure tests are therefore deferred by design rather than attempted through
  reflection or a production change.
- RRUnreliable server `OnRequested` serialization is carrier-owned, not owned by
  `RRUnreliableServerTransport`. The Direct carrier covers it with
  `ServerHandler_IsNeverConcurrent`; the abstract layer records it as deferred.
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
   - exposed protected `TryValidatePayload`, `NextServerRequestId`, and `FrameMaxByteSize`.
     Do not expose the internal running snapshot; no Stage 1 test needs it.
3. Move the existing RRReliable suite files:
   - `Harness/ScriptedReliableCarrier.cs` → `Transports/RR/Reliable/Harness/ScriptedReliableCarrier.cs`;
   - `Harness/TestRRReliableTransport.cs` → `Transports/RR/Reliable/Harness/TestRRReliableTransport.cs`;
   - `Coverage.md` → `Transports/RR/Reliable/Coverage.md`;
   - `RRReliableTransportTests.cs` → `Transports/RR/Reliable/RRReliableTransportTests.cs`;
   - `Generic/*` → `Transports/RR/Reliable/Generic/*`;
   - `Client/*` → `Transports/RR/Reliable/Client/*`;
   - `Server/*` → `Transports/RR/Reliable/Server/*`, excluding
     `RRReliableServerSessionStressTests.cs`, which action 6 moves directly to CoreHelpers.
4. Split `RRReliableTransportTests.cs`:
   - Move shared lifecycle, payload-validation, frame-size formula, and
     server-request-id tests into `RRTransportTests.cs`, retargeted at `TestRRTransport`.
     Cover: one-time start/carrier once; pre-start stop; running stop clears pending
     before carrier stop with the same reason; start carrier false; start carrier throws /
     `AbortCarrier`; payload validation precedence (state before null/size, started null,
     valid, oversized, serialization failure); `NextServerRequestId` starts at one and
     increments.
   - The serialization-failure test uses a list containing `PutLast(default(UnionData))`;
     `UnionData.GetDataSize()` throws for `UnionDataType.Unknown`, and
     `TryValidatePayload` must release the list and return `SendResult.InvalidMessage`.
   - Keep in `RRReliableTransportTests.cs`: `Type_IsRRReliable`, the reliable
     `FrameMaxByteSize` assertion using `RRReliableFrame.MaxOverheadBytes`, and
     `TimerFactory_IsInjected` (the test double exposes the protected `TimerFactory` and
     asserts constructor injection). These become the reliable layer's delta.
   - Move `Stop_SchedulesTerminalWorkBeforeOnStoppedBegins` to `RRTransportTests.cs` as
     `Stop_ClearsPendingBeforeOnStoppedBegins` and replace its timer-factory dependency
     with a local `ClearPendingRequests` callback that sets a bool. Do not add a
     reliable-specific timer-factory test beyond `TimerFactory_IsInjected`.
   - Strengthen the moved
     `Stop_StopsTheCarrierWithTheSuppliedReasonAfterClearingPending` test: inside the
     `ClearPendingRequests` callback, assert `StopCarrierCalls == 0` so the test proves
     clearing happens before the carrier stop, not merely that both ran.
5. Copy `Tests/Pontifex.Transport.Direct.Tests/RR/TestSupport/ManualTimerFactory.cs` to
   `Tests/Pontifex.Tests/Transports/RR/Reliable/Harness/ManualTimerFactory.cs`; change the
   copied file's namespace to `Pontifex.Tests.Transports`. Keep the Direct copy.
6. Move `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport/Server/RRReliableServerSessionStressTests.cs`
   to `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableCoreHelpers/Tests/RRReliableServerSessionTests.cs`.
   Rename the fixture to `RRReliableServerSessionTests`, use the
   `Pontifex.Tests.RR.RRReliableCoreHelpers` namespace, keep both tests, keep
   `Direct.RRReliable.Integration`, add `Direct.RRReliableCoreHelpers`, and remove
   `Direct.RRReliableTransport`. Update `RRReliableCoreHelpers/README.md` directory layout
   and `Coverage.md` to add `RRReliableServerSession` to the suite subject and coverage.
   This is a mechanical relocation grouped with the suite move because the plan-executor
   granularity rule groups mechanical moves; it is not a separate behavioral checkpoint.
7. Update namespaces in files moved into `Transports` to `Pontifex.Tests.Transports` (or a
   documented sub-namespace only if a name collision occurs). Do not apply this step to
   the CoreHelpers stress test from action 6.
8. Update categories on every fixture moved into `Transports`:
   - `[Category("Transports")]`;
   - `[Category("Transports.RR")]`;
   - `[Category("Transports.RR.Reliable")]`;
   - add `.Client`/`.Server` where useful;
   - add `[Category("Transports.Concurrency")]` to every moved test that contains
     `Task.Run`, `SpinWait`, `WaitForRequestCount`, or starts/stops the production dispatch
     thread. The complete list is:
     client: `TerminalKeepsTheHandlerBoundUntilTheFinishCallbackReturns`,
     `CancelOutsideACallback_DeliversOneCanceledAndSuppressesLateResponses`,
     `Stop_SchedulesOneFailedPerPendingRequestBeforeOnStoppedAndDeliversAfter`,
     `ConcurrentResponsesForDifferentRequests_AreDeliveredSafely`;
     server: `ResponseAndFinish_ProduceOrderedFramesWithServerIdsFromOne`,
     `ResponseOrdinal_IsCommittedOnlyAfterOk`,
     `ResponseHandle_MatrixOfFinishRejectAndClosedCalls`,
     `Finish_WhenTheSendFails_LeavesTheHandleRetryable`,
     `Reject_WhenTheSendFails_LeavesTheHandleRetryable`,
     `Reject_AfterTheResponseTtlClosedTheHandle_ReturnsError`,
     `Reject_ClosesTheHandleAndRepeatedRejectReturnsOk`,
     `Cancel_ClosesTheLiveHandleAndSuppressesLaterResponses`,
     `FullDispatchQueue_ReleasesTheOverflowingRequest`,
     `DuplicateRequest_IsSuppressedLiveAndAfterEveryTerminalCause`,
     `TwoClientsWithTheSameClientLocalId_DoNotSuppressEachOther`,
     `OnRequested_IsSerializedAcrossRequests`,
     `ThrowingHandler_RejectsTheUndecidedHandle`,
     `RequestBeforeOnStarted_IsHeldUntilTheReadyGateOpens`,
     `FullPendingListBeforeStart_ReleasesTheOverflowingFrame`,
     `StopDispatch_ReleasesARequestStillQueued`,
     `OnSessionClosed_ClosesOnlyThatSession`,
     `BlockedResponse_WithConcurrentStop_DoesNotHang`,
     `TwoThreadsOnOneHandle_ProduceOneTerminalResult`;
   - remove `Direct.*` categories from these moved files.
9. Delete the old `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport` directory.
10. Update `Tests/Pontifex.Tests/Transports/README.md`:
    - extend the selected inheritance tree with the RR branch;
    - add layer-table rows for `RRTransport`, `RRReliableTransport`,
      `RRReliableTransportGeneric`, `RRReliableClientTransport`, and
      `RRReliableServerTransport`;
    - update the directory layout and harness layout;
    - add RRTransport and RRReliable contract matrices;
    - add categories, filter commands, and out-of-scope entries.
11. Update `Tests/Pontifex.Tests/Transports/RR/Coverage.md` and every moved Coverage file:
    - move the old root coverage to `Transports/RR/Reliable/Coverage.md` and trim the
      shared rows that move to `Transports/RR/Coverage.md`;
    - re-base every moved Coverage file: update the layer/subject/directory, inheritance
      parent, dependency field, and every relative contract link for its new depth; remove
      the stale `RRTransport outside the selected tree` deferred row;
    - add rows for the shared tests now owned by RRTransport;
    - add rows for any existing RRReliable tests missing from the old coverage tables:
      `Request_WhenTheTimerFactoryThrows_ReturnsErrorAndUnbindsTheHandler`,
      `Finish_WhenTheSendFails_LeavesTheHandleRetryable`,
      `Reject_WhenTheSendFails_LeavesTheHandleRetryable`,
      `Reject_AfterTheResponseTtlClosedTheHandle_ReturnsError`;
    - add all four missing RRUnreliable layers (`RRUnreliableTransport`,
      `RRUnreliableTransport<THandler>`, `RRUnreliableClientTransport`, and
      `RRUnreliableServerTransport`) to the `Planned` section of
      `Transports/RR/Coverage.md`; the Unreliable layer files do not exist until Stage 2.
      Do not put planned coverage status in the README (README-16).
12. Update Direct documentation and category references:
    - search for `RRReliableTransport`, `Direct.RRReliableTransport`, and plain
      `RRReliableTransport` prose outside the moved files;
    - remove or redirect references to the deleted abstract suite, including
      `RRReliableCoreHelpers/README.md`, `RRReliableCoreHelpers/Coverage.md`, and
      `RRReliableDirect/README.md`;
    - add the retained `Direct.RRReliable.Integration` category and its filter command to
      the CoreHelpers README categories section;
    - keep `RRReliableDirect`, `RRUnreliableDirect`, and `RRReliableCoreHelpers` intact.

**Verification**

Run:

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports.Concurrency
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports.RR
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports.RR.Reliable
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --filter "TestCategory=Direct.RRReliableCoreHelpers"
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --filter "TestCategory=Direct.RRReliable.Integration"
grep -rn 'Category("Direct' Tests/Pontifex.Tests/Transports
git grep -n "Direct.RRReliableTransport" -- ':!plan.md'
grep -rn "class RRReliableServerSessionTests" Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableCoreHelpers/Tests
test ! -d Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport
test -f Tests/Pontifex.Tests/Transports/RR/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Reliable/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Reliable/Generic/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Reliable/Client/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Reliable/Server/Coverage.md
```

Expected:

- build succeeds with no errors;
- `Transports` = **Passed: 313** (235 baseline + 75 moved + 3 new);
- `Transports.Concurrency` = **Passed: 28** (5 baseline + 23 moved threaded tests);
- `Transports.RR` = **Passed: 78**;
- `Transports.RR.Reliable` = **Passed: 63**;
- full `Pontifex.Tests` = **Passed: 908** (830 baseline + 78);
- Direct default = **Passed: 314** (389 baseline − 75 moved);
- `Direct.RRReliableCoreHelpers` = **Passed: 60** (58 + 2 moved stress tests);
- `Direct.RRReliable.Integration` = **Passed: 3** (the two moved stress tests keep the
  category);
- `grep -rn 'Category("Direct'` prints no output;
- `git grep "Direct.RRReliableTransport"` prints no match outside `plan.md`;
- `grep "class RRReliableServerSessionTests"` finds exactly the moved file;
- the old `RRReliableTransport` directory no longer exists;
- all five RRTransport/RRReliable coverage files exist.

**Definition of done**

The RRTransport and RRReliable layers exist under `Transports/RR`, all moved tests pass
under the new categories, the Direct abstract suite is gone, and the CoreHelpers suite
owns the session stress tests.

**Risks / notes**

- Name collisions between Raw harness and moved RR harness in
  `Pontifex.Tests.Transports` must be resolved by renaming the harness type, not by
  reverting to layer-specific namespaces unless necessary.
- `Stop_ClearsPendingBeforeOnStoppedBegins` must not depend on `ManualTimerFactory`
  after moving to the shared RRTransport layer; use a local probe.
- The moved RRReliable client/server fixtures must not keep fixture-level
  `Direct.Integration`; tag only the enumerated concurrency list with
  `Transports.Concurrency`.
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
     `RRUnreliableFrame.OverheadBytes` + 1.
   Do not add an inherited-lifecycle test; `RRTransportTests` owns that coverage.
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
   - remove the `RRUnreliableTransport` and `RRUnreliableTransport<THandler>` rows from the
     `Planned` table in `Transports/RR/Coverage.md`;
   - add `RRUnreliableClientTransport` to the `Transports/RR/Unreliable/Coverage.md`
     Planned table and `RRUnreliableServerTransport` to the
     `Transports/RR/Unreliable/Generic/Coverage.md` Planned table (nearest existing
     ancestor);
   - add the RRUnreliable contract summary for the base and generic layers;
   - add directory and harness entries. Do not put planned coverage status in the README.

**Verification**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports.RR.Unreliable
```

Expected: build succeeds; `Transports` = **Passed: 324** (313 + 2 base + 9 generic);
`Transports.RR.Unreliable` = **Passed: 11**.

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
   - `TestRRUnreliableClientTransport.cs` — concrete client with configurable `SendFrame`
     result/throw/capture, exposed `OnFrameReceived`, `PendingCountForTests`, and the three
     internal test hooks. Do not add a random-seed configuration; the production seed is
     private static and a production change is out of scope;
   - `RecordingUnreliableClientHandler.cs` — derives from `RRRequestHandler`, implements
     `IRRUnreliableRequestHandler`, records response ids/counts, can throw or cancel from a
     callback, and releases received buffers;
   - put `ManualClock.cs` and `TestMessages.cs` in the nearest common ancestor
     `Transports/RR/Unreliable/Harness` so the server layer can reuse them.
2. Add client tests:
   - `Constructor_WithNullServerEndPoint_Throws`;
   - `Constructor_WithNonPositiveRequestTtl_Throws` — assert
     `ArgumentOutOfRangeException` with `ParamName == "ttl"`;
   - `Constructor_ExposesServerEndPointAndRequestTtl`;
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
   - `Response_WhenDeliveredBeforeRequestReturns_IsStillDelivered` — the test double's
     `SendFrame` calls `Receive` before returning, proving the response may arrive during
     `Request`;
   - `Response_FromForeignRoute_IsReleasedWithoutCallback`;
   - `Response_WhenMalformed_IsReleasedWithoutCallback`;
   - `Response_WhenOversized_IsReleasedWithoutCallback` — build the payload above
     `MessageMaxByteSize` first, then encode the response header; the base checks the
     payload size after the header is popped;
   - `Response_ForUnknownId_IsReleasedWithoutCallback`;
   - `Response_AfterRequestTtlExpires_IsDroppedAndHandlerUnbound` — the client table
     expires at `ExpiresAt <= Now`, so advancing by exactly the TTL is sufficient;
   - `Response_FirstFrameFixesMessageCount`;
   - `Response_WithInconsistentMessageCount_IsDropped`;
   - `Response_WithMessageIdAtOrAboveCount_IsDropped` — build the invalid header manually
     because `RRUnreliableFrame.EncodeResponse` rejects `messageId >= messageCount`;
   - `Response_WithDuplicateMessageId_IsDropped`;
   - `Response_LastMessage_UnbindsHandlerAfterCallback` — reuse the same handler for a
     second request after the last callback to prove unbinding;
   - `Response_WhenHandlerThrows_IsIsolatedAndStillCompletes`;
   - `Response_Callbacks_AreSerializedPerRequest` — category
     `Transports.Concurrency`;
   - `Stop_FromInsideAResponseCallback_DoesNotDeadlock` — category
     `Transports.Concurrency`;
   - `Request_FromInsideAResponseCallback_IsAccepted` — category
     `Transports.Concurrency`;
   - `Cancel_RemovesPendingRequestWithoutCallback`;
   - `Cancel_BeforeSend_RemovesPendingEntryAndDropsLaterResponse` — after the
     `BeforeSendForTests` hook cancels, `Request` still returns `Ok` because production does
     not re-check cancellation after that hook; assert the pending count is zero and a later
     response produces no callback;
   - `Cancel_WhenLastMessageCallbackActive_DefersUnbind`;
   - `Stop_ClearsPendingWindowWithoutCallbacks`;
   - `Stop_WhenRequestRacesStop_ReturnsNotConnectedAndUnbinds` — the synchronous stop at
     `BeforeSendForTests` leaves `IsStarted == false`, so the deterministic result is
     `NotConnected`;
   - `Response_WhenTransportStopped_IsDropped`;
   - `ExpiredRequest_AllowsTheSameHandlerToBeReused`.
3. Record under `Deferred by design` in the client Coverage:
   - random request-id seed determinism, wraparound, and generator failure — the abstract
     type exposes no injectable seed (`CreateRandomRequestId` is private static) and
     production changes are out of scope; `RequestId_IsPublishedBeforeSendFrame` still pins
     publication;
   - `BufferOverflow` prefill — the table is internal and the only public path would need
     65,536 accepted requests. Do not use reflection.
4. Update the layer Coverage.md, Transports README layer table/directory/harness,
   RRUnreliable contract matrix, and remove the `RRUnreliableClientTransport` row from the
   `Transports/RR/Unreliable/Coverage.md` Planned table.

**Verification**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports.Concurrency
```

Expected: build succeeds; `Transports` = **Passed: 360** (324 + 36 client tests);
`Transports.Concurrency` = **Passed: 31** (28 + 3 client concurrency tests); no real
timers, sockets, or sleeps are introduced.

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
   - reuse the Stage 3 `ManualClock` and `TestMessages` from
     `Transports/RR/Unreliable/Harness`; use `RecordingLogConsumer` from
     `Tests/Pontifex.Tests/Endpoints/Harness` for the throwing-handler log assertion.
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
   - `OnRequested_WhenHandlerThrows_LogsRejectsAndTransportStaysValid` — use
     `RecordingLogConsumer` and assert the log entry;
   - `OnRequested_AssignsServerGlobalIdsFromOne`;
   - `Response_SendsClientRequestIdMessageIdAndCount`;
   - `Response_DefaultMessageCountIsOne`;
   - `SetMessageCount_AcceptsOneTo256` — loop over 1 and 256 inside one test, not
     `[TestCase]`;
   - `SetMessageCount_RejectsOutOfRange` — call it on a closed handle with 0 and 257 to
     prove the range check precedes the state check;
   - `SetMessageCount_IsOneTimeBeforeFirstResponse`;
   - `SetMessageCount_AfterFirstResponseThrows`;
   - `SetMessageCount_AfterCloseThrows`;
   - `SetMessageCount_AfterExpiryThrows`;
   - `SetMessageCount_AfterStopThrows`;
   - `Response_WhenSendFrameReturnsNonOk_DoesNotAdvanceAndCanRetry`;
   - `Response_WhenSendFrameThrows_ReturnsErrorAndCanRetry`;
   - `Response_WithNullPayload_ReturnsInvalidMessage`;
   - `Response_WithOversizedPayload_ReturnsMessageTooBigAndReleases` — build the payload
     above `MessageMaxByteSize` before the response header is added; the base checks the
     payload size, not the framed size;
   - `Response_DeclaredSequenceSendsZeroBasedIdsAndCloses`;
   - `Response_AfterLastMessage_ReturnsError`;
   - `Response_AfterResponseTtlExpires_ReturnsErrorAndCloses` — the server expiry is strict
     `Now > ExpiresAt`, so advance the clock by `ResponseTtl + 1 tick`;
   - `Response_AfterTransportStopped_ReturnsError`;
   - `OnFrameReceived_AfterStop_ReleasesWithoutPresenting`;
   - `OnFrameReceived_DuplicateClientRequest_IsPresentedAgain` — RRUnreliable permits
     duplicate presentation; this pins the absence of base-level suppression;
   - `OnFrameReceived_FromDifferentClients_HaveIndependentHandles`;
   - `Response_ConcurrentCallsProduceOneSequence` (categorized
     `Transports.Concurrency`).
3. Update the layer Coverage.md, Transports README layer table/directory/harness,
   RRUnreliable contract matrix, categories, and planned/deferred tables:
   - remove the `RRUnreliableServerTransport` row from the
     `Transports/RR/Unreliable/Generic/Coverage.md` Planned table;
   - record `OnRequested` serialization under `Deferred by design`: the abstract base calls
     the handler directly and serialization is carrier-owned; the Direct suite covers it
     with `ServerHandler_IsNeverConcurrent`.
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
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports.RR.Reliable
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports.RR.Unreliable
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj
dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj
grep -rn 'Category("Direct' Tests/Pontifex.Tests/Transports
git status --porcelain -- Pontifex/
test -f Tests/Pontifex.Tests/Transports/RR/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Reliable/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Reliable/Generic/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Reliable/Client/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Reliable/Server/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Unreliable/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Unreliable/Generic/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Unreliable/Client/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Unreliable/Server/Coverage.md
```

Expected:

- build succeeds with no errors;
- `Transports` = **Passed: 392** (235 baseline + 75 moved + 3 Stage 1 new + 11 Stage 2 +
  36 Stage 3 + 32 Stage 4);
- `Transports.Concurrency` = **Passed: 32**;
- `Transports.RR` = **Passed: 157**;
- `Transports.RR.Reliable` = **Passed: 63**;
- `Transports.RR.Unreliable` = **Passed: 79**;
- full `Pontifex.Tests` = **Passed: 987** (830 baseline + 157);
- full `Pontifex.Transport.Direct.Tests` = **Passed: 314**;
- `grep -rn 'Category("Direct'` prints no output;
- `git status --porcelain -- Pontifex/` prints no output (no production changes);
- all nine layer Coverage files exist.

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
- Every fixture moved into `Transports` carries the `Transports` category and the RR
  subcategories, and no fixture moved into `Transports` carries a `Direct.*` category.
- `RRReliableServerSessionTests` lives in the Direct CoreHelpers suite with
  `Direct.RRReliableCoreHelpers` and `Direct.RRReliable.Integration`, and the CoreHelpers
  README/Coverage list it.
- The RRUnreliable client seed-determinism/wraparound/generator-failure gap and the
  carrier-owned server callback-serialization gap are recorded under `Deferred by design`
  with exact reasons.
- The full verification set passes:
  - solution build;
  - `Transports` = 392 passed;
  - `Transports.Concurrency` = 32 passed;
  - `Transports.RR` = 157 passed;
  - `Transports.RR.Reliable` = 63 passed;
  - `Transports.RR.Unreliable` = 79 passed;
  - full `Pontifex.Tests` = 987 passed;
  - full `Pontifex.Transport.Direct.Tests` = 314 passed;
  - `grep -rn 'Category("Direct' Tests/Pontifex.Tests/Transports` prints no output;
  - `git status --porcelain -- Pontifex/` prints no output.
- All nine RR layer `Coverage.md` files exist and are referenced from the README.
- The plan file is deleted and the deletion committed with
  `Remove completed implementation plan`; after that commit `git status --porcelain` is
  empty and `test ! -f plan.md` succeeds.
