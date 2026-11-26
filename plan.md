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
- The code baseline is `dc926bd more tests`, which contains the previously implemented
  AnyTransport/admission/unreliable-server/endpoint-client tests. The working tree is clean
  at the current HEAD, which also contains only the plan revisions.

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
- Exactly the 25 enumerated Stage 1 moved tests carry
  `[Category("Transports.Concurrency")]`. Additionally, exactly four new Stage 3/4 tests
  carry it: `Response_Callbacks_AreSerializedPerRequest`,
  `Stop_FromInsideAResponseCallback_DoesNotDeadlock`,
  `Request_FromInsideAResponseCallback_IsAccepted`, and
  `Response_ConcurrentCallsProduceOneSequence`. Do not tag other tests.
- Remove `Direct.*` categories from moved abstract tests. The exception is the
  `RRReliableServerSession` fixture relocated to the Direct CoreHelpers suite: the fixture
  gains `Direct.RRReliableCoreHelpers`, and only its two stress methods keep
  `Direct.RRReliable.Integration`.
- Test names use `Scenario_ExpectedResult`. Names may repeat across different layer
  fixtures; coverage is checked per test file, not globally. Planned rows use `—` in the
  first cell; deferred rows use prose in the first cell without backticks, so the coverage
  checker does not count them as tests.
- Never place `Assert.That` inside a transport-invoked callback: `AnyTransport.Stop`,
  `RRUnreliableClientTransport.OnFrameReceived`, and
  `RRUnreliableServerTransport.OnFrameReceived` catch callback exceptions. Record the
  observed value in a captured field and assert after the public call returns.
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
  moving the `RRReliableServerSession` stress tests and the session-window test to the
  CoreHelpers suite and updating documentation/categories.
- Reworking RRReliable core helper tests beyond the session stress/window relocation.
- Covering Raw transports again or changing the existing Raw layers.

## Assumptions

- The existing RRReliable abstract tests are correct and can be moved without weakening
  assertions, except the shared stop-ordering test, which is rewritten from timer-factory
  scheduling to a direct clearing-order probe. The reliable scheduling-order behaviour
  remains covered by the client stop test
  `Stop_SchedulesOneFailedPerPendingRequestBeforeOnStoppedAndDeliversAfter`; the server stop
  path closes handles synchronously. Evidence: the suite passes in
  `Tests/Pontifex.Transport.Direct.Tests`.
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
   - `public override TransportType Type => TransportType.RRReliable;` (the abstract
     `Type` must be implemented for the double to compile);
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
   - `Client/*` → `Transports/RR/Reliable/Client/*`, moving
     `PlainReliableClientHandler` out of `RRReliableClientTransportTests.cs` into
     `Client/Harness/PlainReliableClientHandler.cs` so the fixture file contains only the
     fixture;
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
   - Add `FrameMaxByteSize_IsTheMessageLimitPlusTheOverheadAndOne` to RRTransport using a
     custom `FrameOverheadBytes`; this is the shared formula test and does not replace the
     reliable-specific assertion.
   - Add `ValidatePayload_WhenSerializationThrows_ReturnsInvalidMessageAndReleases` using a
     list containing `PutLast(default(UnionData))`; `UnionData.GetDataSize()` throws for
     `UnionDataType.Unknown`, and `TryValidatePayload` must release the list and return
     `SendResult.InvalidMessage`.
   - Keep in `RRReliableTransportTests.cs`: `Type_IsRRReliable` and the reliable
     `FrameMaxByteSize` assertion using `RRReliableFrame.MaxOverheadBytes`.
   - Add `TimerFactory_IsInjected` to `RRReliableTransportTests.cs`; expose the protected
     `TimerFactory` on `TestRRReliableTransport` and assert constructor injection. This is
     the third new test and becomes part of the reliable layer's delta.
   - Move `Stop_SchedulesTerminalWorkBeforeOnStoppedBegins` to `RRTransportTests.cs` as
     `Stop_ClearsPendingBeforeTheUserOnStoppedCallback`: the `ClearPendingRequests` override
     sets `clearRan = true`; the user stop callback records `sawClear = clearRan`; after
     `Stop` returns, assert `sawClear` is true. Do not assert inside either callback. The
     reliable client layer retains its existing scheduling-order test; the server stop path
     closes handles synchronously.
   - Strengthen the moved
     `Stop_StopsTheCarrierWithTheSuppliedReasonAfterClearingPending` test: record
     `StopCarrierCalls` inside the `ClearPendingRequests` callback into a captured field,
     then assert after `Stop` returns that the recorded value was zero. Do not assert inside
     the callback.
5. Copy `Tests/Pontifex.Transport.Direct.Tests/RR/TestSupport/ManualTimerFactory.cs` to
   `Tests/Pontifex.Tests/Transports/RR/Reliable/Harness/ManualTimerFactory.cs`; change the
   copied file's namespace to `Pontifex.Tests.Transports`. Keep the Direct copy.
6. Move `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport/Server/RRReliableServerSessionStressTests.cs`
   to `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableCoreHelpers/Tests/Integration/RRReliableServerSessionTests.cs`.
   Rename the fixture to `RRReliableServerSessionTests`, use the
   `Pontifex.Tests.RR.RRReliableCoreHelpers` namespace, keep both stress tests, and move
   `Direct.RRReliable.Integration` from the fixture to the two stress test methods only;
   the fixture carries `Direct.RRReliableCoreHelpers`, and remove
   `Direct.RRReliableTransport`. The `Tests/Integration/` group satisfies TEST-10 for the
   parallel stress facet. Also move `CompletedWindow_IsPerSessionAndFullAfterEveryId` from
   `RRReliableServerTransportTests.cs` into this CoreHelpers fixture, because it constructs
   the internal `RRReliableServerSession` directly and is not transport-contract coverage.
   Update `RRReliableCoreHelpers/README.md` directory layout, group table, the sentence
   saying the suite has no groups, and both the suite-type and root-subject lists to include
   `RRReliableServerSession`; update `Coverage.md` to add `RRReliableServerSession` to the
   suite subject and coverage. This is a mechanical relocation grouped with the suite
   move because the plan-executor granularity rule groups mechanical moves; it is not a
   separate behavioral checkpoint.
7. Update namespaces in files moved into `Transports` to `Pontifex.Tests.Transports` (or a
   documented sub-namespace only if a name collision occurs). Do not apply this step to
   the CoreHelpers stress test from action 6.
8. Update categories on every fixture moved into `Transports`:
   - `[Category("Transports")]`;
   - `[Category("Transports.RR")]`;
   - `[Category("Transports.RR.Reliable")]`;
   - add `.Client`/`.Server` where useful;
   - add `[Category("Transports.Concurrency")]` to exactly the following 25 moved tests.
     This list is authoritative; do not tag additional tests even if they drive a callback
     pump thread, because the expected filter counts are derived from this list:
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
     `CancelOfAQueuedRequest_PreventsPresentation`,
     `ResponseTtlWhileQueued_SendsFailedAndNeverPresents`,
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
    - add RR production and specification links to the README Contract references
      section (README-05): `RRTransport.cs`, `RRReliableTransport.cs`,
      `RRReliableClientTransport.cs`, `RRReliableServerTransport.cs`, and the shared/reliable
      RR specifications;
    - add categories, filter commands, and out-of-scope entries; rewrite the existing
      concurrency-category prose and test-count sentences so they describe the expanded
      category instead of the old five-test Raw-only count.
11. Update `Tests/Pontifex.Tests/Transports/RR/Coverage.md` and every moved Coverage file:
    - move the old root coverage to `Transports/RR/Reliable/Coverage.md` and trim the
      shared rows that move to `Transports/RR/Coverage.md`;
    - re-base every moved Coverage file: update the layer/subject/directory, inheritance
      parent, dependency field, and every relative contract link for its new depth; use the
      exact identity rows `| Layer | \`<layer>\` |`, `| Subject | \`<type>\` |`,
      `| Directory | \`<path without trailing slash>\` |`,
      `| Inheritance parent | \`<parent>\` |`, `| Dependency | — |`, and a
      `| Contract | [<name>](<relative path>)... |` row with at least one resolving relative
      link;
      remove the stale
      `RRTransport outside the selected tree` deferred row and reword the matching
      prose paragraph to say the base is now the `RRTransport` ancestor layer and the shared
      tests live in `Transports/RR/Coverage.md`;
    - normalize every moved/new RR `Coverage.md` heading to the exact suite form
      `## Planned` and `## Deferred by design` (the Direct files use
      `## Deferred By Design`);
    - add rows for the shared tests now owned by RRTransport;
    - add rows for any existing RRReliable tests missing from the old coverage tables:
      `Request_WhenTheTimerFactoryThrows_ReturnsErrorAndUnbindsTheHandler`,
      `Finish_WhenTheSendFails_LeavesTheHandleRetryable`,
      `Reject_WhenTheSendFails_LeavesTheHandleRetryable`,
      `Reject_AfterTheResponseTtlClosedTheHandle_ReturnsError`;
    - remove `CompletedWindow_IsPerSessionAndFullAfterEveryId` from the RRReliable server
      coverage and record it in CoreHelpers coverage instead;
    - update `Tests/Pontifex.Tests/Transports/Coverage.md`: its “whole selected inheritance
      tree is covered” note must not claim the RR branch is complete while the RRUnreliable
      layers are planned; distinguish the AnyTransport root layer from the planned RR
      subtree;
    - add all four missing RRUnreliable layers (`RRUnreliableTransport`,
      `RRUnreliableTransport<THandler>`, `RRUnreliableClientTransport`, and
      `RRUnreliableServerTransport`) to the `Planned` section of
      `Transports/RR/Coverage.md`; the Unreliable layer files do not exist until Stage 2.
      Write planned rows with `—` in the first cell and the layer name in prose, so the
      coverage checker does not mistake a planned row for a covered test. Do not put planned
      coverage status in the README (README-16).
12. Update Direct documentation and category references:
    - search for `RRReliableTransport`, `Direct.RRReliableTransport`, and plain
      `RRReliableTransport` prose outside the moved files;
    - remove every literal `RRReliableTransport` reference to the deleted abstract suite
      from `RRReliableCoreHelpers/README.md`, `RRReliableCoreHelpers/Coverage.md`, and
      `RRReliableDirect/README.md`, replacing it with the new
      `Tests/Pontifex.Tests/Transports/RR/Reliable` path where a pointer is still needed;
      remove or reword the stale CoreHelpers sentence that
      says `ManualTimerFactory` is shared with the abstract transport suite; remove or
      reword the stale CoreHelpers `Coverage.md` deferred row that says
      `UnionDataList.GetDataSize` cannot throw through the public contract, because the new
      `PutLast(default(UnionData))` test proves the guard is reachable;
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
git grep -n "RRReliableTransport" -- Tests/Pontifex.Transport.Direct.Tests
! grep -q "shared with the abstract transport suite" Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableCoreHelpers/README.md
grep -rn "class RRReliableServerSessionTests" Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableCoreHelpers/Tests
! grep -q "cannot be made to throw through its public contract" Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableCoreHelpers/Coverage.md
! grep -q "No missing layers: the whole selected inheritance tree is covered" Tests/Pontifex.Tests/Transports/Coverage.md
! grep -q "stays outside the selected tree" Tests/Pontifex.Tests/Transports/RR/Reliable/Coverage.md
! grep -q "An \`RRTransport\` layer" Tests/Pontifex.Tests/Transports/RR/Reliable/Coverage.md
! grep -q "shared base is outside the selected tree" Tests/Pontifex.Tests/Transports/RR/Reliable/Coverage.md
python3 - <<'PY'
import sys
text = open('Tests/Pontifex.Tests/Transports/RR/Reliable/Coverage.md',
            encoding='utf-8-sig').read()
deferred = text.split('## Deferred by design', 1)[1].split('\n## ', 1)[0]
if ('| An `RRTransport` layer |' in deferred or
        'shared base is outside the selected tree' in deferred):
    print('stale RRTransport deferred row')
    sys.exit(1)
PY
test -f Tests/Pontifex.Tests/Transports/RR/RRTransportTests.cs
test "$(grep -c '\[Test\]' Tests/Pontifex.Tests/Transports/RR/RRTransportTests.cs)" -eq 15
test -f Tests/Pontifex.Tests/Transports/RR/Reliable/RRReliableTransportTests.cs
test "$(grep -c '\[Test\]' Tests/Pontifex.Tests/Transports/RR/Reliable/RRReliableTransportTests.cs)" -eq 3
test -f Tests/Pontifex.Tests/Transports/RR/Reliable/Generic/RRReliableTransportGenericTests.cs
test "$(grep -c '\[Test\]' Tests/Pontifex.Tests/Transports/RR/Reliable/Generic/RRReliableTransportGenericTests.cs)" -eq 8
test -f Tests/Pontifex.Tests/Transports/RR/Reliable/Client/RRReliableClientTransportTests.cs
test "$(grep -c '\[Test\]' Tests/Pontifex.Tests/Transports/RR/Reliable/Client/RRReliableClientTransportTests.cs)" -eq 30
test -f Tests/Pontifex.Tests/Transports/RR/Reliable/Server/RRReliableServerTransportTests.cs
test "$(grep -c '\[Test\]' Tests/Pontifex.Tests/Transports/RR/Reliable/Server/RRReliableServerTransportTests.cs)" -eq 21
! grep -rn "Tests/Pontifex.Transport.Direct.Tests" Tests/Pontifex.Tests/Transports/RR --include=Coverage.md
python3 - <<'PY'
import os, re, sys
base = 'Tests/Pontifex.Tests/Transports/RR'
expected = {
 'Coverage.md': ('RRTransport', 'Pontifex.RR.RRTransport', base, 'Pontifex.AnyTransport'),
 'Reliable/Coverage.md': ('RRReliableTransport',
   'Pontifex.RR.Reliable.RRReliableTransport', f'{base}/Reliable',
   'Pontifex.RR.RRTransport'),
 'Reliable/Generic/Coverage.md': ('RRReliableTransportGeneric',
   'Pontifex.RR.Reliable.RRReliableTransport<THandler>', f'{base}/Reliable/Generic',
   'Pontifex.RR.Reliable.RRReliableTransport'),
 'Reliable/Client/Coverage.md': ('RRReliableClientTransport',
   'Pontifex.RR.Reliable.RRReliableClientTransport', f'{base}/Reliable/Client',
   'Pontifex.RR.Reliable.RRReliableTransport'),
 'Reliable/Server/Coverage.md': ('RRReliableServerTransport',
   'Pontifex.RR.Reliable.RRReliableServerTransport', f'{base}/Reliable/Server',
   'Pontifex.RR.Reliable.RRReliableTransport<THandler>'),
}
for rel, (layer, subject, directory, parent) in expected.items():
    path = os.path.join(base, rel)
    text = open(path, encoding='utf-8-sig').read()
    for field, value in (('Layer', layer), ('Subject', subject),
                         ('Directory', directory), ('Inheritance parent', parent)):
        if f'| {field} | `{value}` |' not in text:
            print(f'bad {field}: {path} expected {value}')
            sys.exit(1)
    if '| Dependency | — |' not in text:
        print(f'missing/non-canonical Dependency row: {path}')
        sys.exit(1)
    if '| Contract |' not in text:
        print(f'missing Contract row: {path}')
        sys.exit(1)
    if not re.search(r'\]\((?:\.\./)+', text):
        print(f'Contract row has no relative link: {path}')
        sys.exit(1)
    for link in re.findall(r'\]\(([^)]+)\)', text):
        if link.startswith(('..', '.')):
            target = os.path.normpath(os.path.join(os.path.dirname(path), link))
            if not os.path.exists(target):
                print(f'broken link: {path} {link}')
                sys.exit(1)
PY
python3 - <<'PY'
import glob, re, sys
expected = {
 'TerminalKeepsTheHandlerBoundUntilTheFinishCallbackReturns',
 'CancelOutsideACallback_DeliversOneCanceledAndSuppressesLateResponses',
 'Stop_SchedulesOneFailedPerPendingRequestBeforeOnStoppedAndDeliversAfter',
 'ConcurrentResponsesForDifferentRequests_AreDeliveredSafely',
 'ResponseAndFinish_ProduceOrderedFramesWithServerIdsFromOne',
 'ResponseOrdinal_IsCommittedOnlyAfterOk',
 'ResponseHandle_MatrixOfFinishRejectAndClosedCalls',
 'Finish_WhenTheSendFails_LeavesTheHandleRetryable',
 'Reject_WhenTheSendFails_LeavesTheHandleRetryable',
 'Reject_AfterTheResponseTtlClosedTheHandle_ReturnsError',
 'Reject_ClosesTheHandleAndRepeatedRejectReturnsOk',
 'Cancel_ClosesTheLiveHandleAndSuppressesLaterResponses',
 'CancelOfAQueuedRequest_PreventsPresentation',
 'ResponseTtlWhileQueued_SendsFailedAndNeverPresents',
 'FullDispatchQueue_ReleasesTheOverflowingRequest',
 'DuplicateRequest_IsSuppressedLiveAndAfterEveryTerminalCause',
 'TwoClientsWithTheSameClientLocalId_DoNotSuppressEachOther',
 'OnRequested_IsSerializedAcrossRequests',
 'ThrowingHandler_RejectsTheUndecidedHandle',
 'RequestBeforeOnStarted_IsHeldUntilTheReadyGateOpens',
 'FullPendingListBeforeStart_ReleasesTheOverflowingFrame',
 'StopDispatch_ReleasesARequestStillQueued',
 'OnSessionClosed_ClosesOnlyThatSession',
 'BlockedResponse_WithConcurrentStop_DoesNotHang',
 'TwoThreadsOnOneHandle_ProduceOneTerminalResult',
}
found = set()
for path in glob.glob('Tests/Pontifex.Tests/Transports/RR/**/*Tests.cs', recursive=True):
    source = open(path, encoding='utf-8-sig').read()
    for match in re.finditer(r'\[Test\]([\s\S]*?)public void (\w+)', source):
        if 'Transports.Concurrency' in match.group(1):
            found.add(match.group(2))
if found != expected:
    print('concurrency category mismatch:', sorted(found ^ expected))
    sys.exit(1)
PY
python3 - <<'PY'
import glob, os, re, sys
for path in glob.glob('Tests/Pontifex.Tests/Transports/RR/**/*Tests.cs', recursive=True):
    source = open(path, encoding='utf-8-sig').read()
    tests = set(re.findall(r'\[Test\][\s\S]*?public void (\w+)', source))
    coverage = open(os.path.join(os.path.dirname(path), 'Coverage.md'),
                    encoding='utf-8-sig').read()
    counts = {}
    for name in re.findall(r'^\|\s*`([A-Za-z0-9_]+)`\s*\|', coverage, re.M):
        counts[name] = counts.get(name, 0) + 1
    covered = set(counts)
    missing = tests - covered
    stale = covered - tests
    duplicates = {name: count for name, count in counts.items() if count != 1}
    for name in sorted(missing):
        print(f'missing coverage: {path} {name}')
    for name in sorted(stale):
        print(f'stale coverage: {path} {name}')
    for name, count in duplicates.items():
        print(f'duplicate coverage: {path} {name} x{count}')
    if missing or stale or duplicates:
        sys.exit(1)
PY
test ! -d Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableTransport
test -f Tests/Pontifex.Tests/Transports/RR/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Reliable/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Reliable/Generic/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Reliable/Client/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Reliable/Server/Coverage.md
```

Expected:

- build succeeds with no errors;
- `Transports` = **Passed: 312** (235 baseline + 74 moved + 3 new);
- `Transports.Concurrency` = **Passed: 30** (5 baseline + exactly the 25 named moved
  threaded tests; the category-member script passes);
- `Transports.RR` = **Passed: 77**;
- `Transports.RR.Reliable` = **Passed: 62**;
- full `Pontifex.Tests` = **Passed: 907** (830 baseline + 77);
- Direct unfiltered run = **Passed: 315** (389 baseline − 74 moved);
- `Direct.RRReliableCoreHelpers` = **Passed: 61** (58 + 2 stress + 1 session-window test);
- `Direct.RRReliable.Integration` = **Passed: 3** (the two moved stress methods plus the
  existing Direct RRReliable integration test);
- `grep -rn 'Category("Direct'` prints no output;
- `git grep "Direct.RRReliableTransport"` prints no match outside `plan.md`;
- `git grep "RRReliableTransport" -- Tests/Pontifex.Transport.Direct.Tests` prints no
  output (old-suite prose redirected);
- the stale `ManualTimerFactory` sentence is gone from the CoreHelpers README;
- the stale CoreHelpers `GetDataSize` sentence is gone;
- the coverage Python check finds every `[Test]` method in its sibling `Coverage.md`;
- `grep "class RRReliableServerSessionTests"` finds exactly the moved file;
- the old `RRReliableTransport` directory no longer exists;
- all five RRTransport/RRReliable coverage files exist;
- the root `Transports/Coverage.md` no longer claims the whole selected tree is covered
  while RRUnreliable is planned;
- the moved reliable coverage no longer says the RRTransport base stays outside the
  selected tree, and the stale `An \`RRTransport\` layer` deferred row is gone;
- the five test files exist with exactly 15 / 3 / 8 / 30 / 21 `[Test]` methods in
  RRTransport, Reliable root, Generic, Client, and Server respectively;
- each moved Coverage file has the expected Directory and Inheritance parent, contains no
  old Direct path, and every relative contract link resolves;
- manual stage review: every test name enumerated in action 4 exists in the named file, and
  the 13 shared names are absent from the reliable root file. Inspect `git diff -M` and
  confirm the only semantic edits are: retargeting the 13 shared tests to `TestRRTransport`;
  the stop-ordering rewrite; the strengthened stop-reason assertion; the new frame-size,
  serialization-failure, and `TimerFactory_IsInjected` tests; and namespace/category/
  coverage changes. No other assertion may be weakened;
- manual README review: the selected tree includes `RRTransport` and the RRReliable
  branch; the layer table has rows for RRTransport and the four RRReliable layers with the
  correct subject, directory, inheritance parent, and coverage link; the directory and
  harness layouts include `RR/`; the RR contract matrices have headings; the categories
  section names `Transports.RR` and `Transports.RR.Reliable` and states the concurrency
  count as 30; the filter commands are present; the out-of-scope list mentions concrete RR
  carriers and Direct RR suites; no planned coverage status appears in the README.

**Definition of done**

The RRTransport and RRReliable layers exist under `Transports/RR`, all moved tests pass
under the new categories, the Direct abstract suite is gone, the CoreHelpers suite owns the
session stress tests, and the README sections listed in action 10/12 are updated.

**Risks / notes**

- Name collisions between Raw harness and moved RR harness in
  `Pontifex.Tests.Transports` must be resolved by renaming the harness type, not by
  reverting to layer-specific namespaces unless necessary.
- `Stop_ClearsPendingBeforeTheUserOnStoppedCallback` must not depend on
  `ManualTimerFactory` after moving to the shared RRTransport layer; use the user-callback
  probe described in action 4.
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
   - `Stop_WhileRunning_HidesHandlerBeforeTheCarrierStops` — record `DispatchHandler`
     inside the test double's `StopCarrier` into a captured field, then assert after `Stop`
     returns that the recorded value was `null`; do not assert inside `StopCarrier` because
     `AnyTransport.Stop` swallows callback exceptions;
   - `Init_AfterStop_ReturnsFalse` — start successfully first, then stop, then call `Init`
     again.
5. Update `Transports/RR/Unreliable/Coverage.md`, the Generic coverage file, and the
   Transports README:
   - add both layer rows (`RRUnreliableTransport` and
     `RRUnreliableTransportGeneric`);
   - create both Coverage files with the exact identity rows (Layer, Subject,
     Directory without trailing slash, Inheritance parent, `Dependency | —`, Contract
     relative link) and the headings `## Planned` and `## Deferred by design`;
   - remove all four RRUnreliable rows from the `Planned` table in
     `Transports/RR/Coverage.md`;
   - add `RRUnreliableClientTransport` to the `Transports/RR/Unreliable/Coverage.md`
     Planned table and `RRUnreliableServerTransport` to the
     `Transports/RR/Unreliable/Generic/Coverage.md` Planned table (nearest existing
     ancestor), using `—` in the first cell and the layer name in prose so the coverage
     checker does not treat a planned row as a covered test;
   - add the RRUnreliable contract summary for the base and generic layers;
   - add directory and harness entries. Do not put planned coverage status in the README.

**Verification**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports.RR.Unreliable
test -f Tests/Pontifex.Tests/Transports/RR/Unreliable/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Unreliable/Generic/Coverage.md
grep -q '| Layer | `RRUnreliableTransport` |' Tests/Pontifex.Tests/Transports/RR/Unreliable/Coverage.md
grep -q '| Inheritance parent | `Pontifex.RR.RRTransport` |' Tests/Pontifex.Tests/Transports/RR/Unreliable/Coverage.md
grep -q '| Dependency | — |' Tests/Pontifex.Tests/Transports/RR/Unreliable/Coverage.md
grep -Eq '\]\(\.\./' Tests/Pontifex.Tests/Transports/RR/Unreliable/Coverage.md
grep -q '| Layer | `RRUnreliableTransportGeneric` |' Tests/Pontifex.Tests/Transports/RR/Unreliable/Generic/Coverage.md
grep -q '| Inheritance parent | `Pontifex.RR.Unreliable.RRUnreliableTransport` |' Tests/Pontifex.Tests/Transports/RR/Unreliable/Generic/Coverage.md
grep -q '| Dependency | — |' Tests/Pontifex.Tests/Transports/RR/Unreliable/Generic/Coverage.md
grep -Eq '\]\(\.\./' Tests/Pontifex.Tests/Transports/RR/Unreliable/Generic/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Unreliable/RRUnreliableTransportTests.cs
test "$(grep -c '\[Test\]' Tests/Pontifex.Tests/Transports/RR/Unreliable/RRUnreliableTransportTests.cs)" -eq 2
test -f Tests/Pontifex.Tests/Transports/RR/Unreliable/Generic/RRUnreliableTransportGenericTests.cs
test "$(grep -c '\[Test\]' Tests/Pontifex.Tests/Transports/RR/Unreliable/Generic/RRUnreliableTransportGenericTests.cs)" -eq 9
sed -n '/^## Planned/,/^## /p' Tests/Pontifex.Tests/Transports/RR/Unreliable/Coverage.md | grep -q "RRUnreliableClientTransport"
sed -n '/^## Planned/,/^## /p' Tests/Pontifex.Tests/Transports/RR/Unreliable/Generic/Coverage.md | grep -q "RRUnreliableServerTransport"
! sed -n '/^## Planned/,/^## /p' Tests/Pontifex.Tests/Transports/RR/Coverage.md | grep -q "RRUnreliable"
python3 - <<'PY'
import glob, os, re, sys
for path in glob.glob('Tests/Pontifex.Tests/Transports/RR/Unreliable/**/*Tests.cs', recursive=True):
    source = open(path, encoding='utf-8-sig').read()
    tests = set(re.findall(r'\[Test\][\s\S]*?public void (\w+)', source))
    coverage = open(os.path.join(os.path.dirname(path), 'Coverage.md'),
                    encoding='utf-8-sig').read()
    counts = {}
    for name in re.findall(r'^\|\s*`([A-Za-z0-9_]+)`\s*\|', coverage, re.M):
        counts[name] = counts.get(name, 0) + 1
    covered = set(counts)
    missing = tests - covered
    stale = covered - tests
    duplicates = {name: count for name, count in counts.items() if count != 1}
    for name in sorted(missing):
        print(f'missing coverage: {path} {name}')
    for name in sorted(stale):
        print(f'stale coverage: {path} {name}')
    for name, count in duplicates.items():
        print(f'duplicate coverage: {path} {name} x{count}')
    if missing or stale or duplicates:
        sys.exit(1)
PY
```

Expected: build succeeds; `Transports` = **Passed: 323** (312 + 2 base + 9 generic);
`Transports.RR.Unreliable` = **Passed: 11**; both coverage files exist and each test has
exactly one coverage row; the base file has 2 `[Test]` methods and the generic file has 9;
the client/server planned rows are present in their nearest ancestor coverage files and the
four parent planned rows are gone; both Coverage files have exact identity rows, `Dependency | —`,
and relative contract links; manual stage review: all 2 base and 9 generic test names
enumerated in action 4 exist in the named files with no substitutions; manual README review:
layer-table rows for `RRUnreliableTransport` and `RRUnreliableTransportGeneric`,
directory/harness layout, contract summary, categories, and filter commands are present.

**Definition of done**

Both RRUnreliable base and generic layers exist, have coverage files and tests, pass in
the Transports run, and have the README layer/contract entries from action 5.

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

1. Create `Transports/RR/Unreliable/Client/Coverage.md` (exact identity rows with
   `Dependency | —` and a relative contract link, plus the `## Planned` and
   `## Deferred by design` headings), `RRUnreliableClientTransportTests.cs`, and `Harness/`
   files:
   - `TestRRUnreliableClientTransport.cs` — concrete client with configurable `SendFrame`
     result/throw/capture, exposed `OnFrameReceived`, `PendingCountForTests`, and the three
     internal test hooks; construct it with a public `StringEndPoint` server route (or a
     small `IEndPoint` stub) and use that same instance for response sources. Do not add a
     random-seed configuration; the production seed is private static and a production
     change is out of scope;
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
   - `Request_WhenStopRacesBeforeSend_ReturnsErrorAndUnbinds` — the test double's
     `SendFrame` returns `Ok`; the stop sets `_stopping`, so the post-send branch returns
     `Error`;
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
   - `Response_Callbacks_AreSerializedPerRequest` — mark `[Timeout(5000)]`; give the
     recording handler an active-callback counter. Start worker A with the first `Receive`;
     wait for its entry signal (first callback active) before starting worker B with the
     second `Receive`. The main thread then waits with bounded `SpinWait` until worker B
     reports `ThreadState.WaitSleepJoin` (blocked on the production state lock). Treat
     `ThreadState` as a secondary diagnostic; the active-callback counter is the primary
     evidence. Do not accept any fallback condition; fail if the wait is not observed.
     Assert active callbacks
     == 1 and second callback not entered, then release A and join both. Assert both ids were
     delivered in order and max active callbacks stayed 1.
     Only a duplicate id is dropped, covered by
     `Response_WithDuplicateMessageId_IsDropped`. Category `Transports.Concurrency`;
   - `Stop_FromInsideAResponseCallback_DoesNotDeadlock` — mark `[Timeout(5000)]` so a
     deadlock fails instead of hanging the run; category `Transports.Concurrency`;
   - `Request_FromInsideAResponseCallback_IsAccepted` — use a second
     `RecordingUnreliableClientHandler` for the nested request, record the `SendResult`
     inside the callback, assert after `Receive` returns that it was `Ok`, and mark
     `[Timeout(5000)]`; category `Transports.Concurrency`;
   - `Cancel_RemovesPendingRequestWithoutCallback`;
   - `Cancel_BeforeSend_RemovesPendingEntryAndDropsLaterResponse` — after the
     `BeforeSendForTests` hook cancels, `Request` still returns `Ok` because production does
     not re-check cancellation after that hook; assert the pending count is zero and a later
     response produces no callback;
   - `Cancel_InsideANonLastMessageCallback_DefersUnbind` — use a response with at least
     two messages, call `Cancel()` from the first callback while the request is still in the
     pending table, record `IsBound` inside the callback, and assert after `Receive` returns
     that the recorded value was `true` and that the handler is now unbound; do not assert
     inside the callback;
   - `Stop_ClearsPendingWindowWithoutCallbacks`;
   - `Response_WhenTransportStopped_IsDropped`;
   - `ExpiredRequest_AllowsTheSameHandlerToBeReused`.
3. Record under `Deferred by design` in the client Coverage:
   - random request-id seed determinism, wraparound, and generator failure — the abstract
     type exposes no injectable seed (`CreateRandomRequestId` is private static) and
     production changes are out of scope; `RequestId_IsPublishedBeforeSendFrame` still pins
     publication;
   - `BufferOverflow` prefill — the client's pending table is private and not injectable;
     the only public path would need 65,536 accepted requests. Do not use reflection.
4. Update the layer Coverage.md, Transports README layer table/directory/harness,
   RRUnreliable contract matrix, the `Transports.Concurrency` prose/count, and remove the
   `RRUnreliableClientTransport` row from the
   `Transports/RR/Unreliable/Coverage.md` Planned table.

**Verification**

```bash
dotnet build Pontifex.sln
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports
dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --filter TestCategory=Transports.Concurrency
test -f Tests/Pontifex.Tests/Transports/RR/Unreliable/Client/Coverage.md
grep -q '| Layer | `RRUnreliableClientTransport` |' Tests/Pontifex.Tests/Transports/RR/Unreliable/Client/Coverage.md
grep -q '| Inheritance parent | `Pontifex.RR.Unreliable.RRUnreliableTransport` |' Tests/Pontifex.Tests/Transports/RR/Unreliable/Client/Coverage.md
grep -q '| Dependency | — |' Tests/Pontifex.Tests/Transports/RR/Unreliable/Client/Coverage.md
grep -Eq '\]\(\.\./' Tests/Pontifex.Tests/Transports/RR/Unreliable/Client/Coverage.md
test -f Tests/Pontifex.Tests/Transports/RR/Unreliable/Client/RRUnreliableClientTransportTests.cs
test "$(grep -c '\[Test\]' Tests/Pontifex.Tests/Transports/RR/Unreliable/Client/RRUnreliableClientTransportTests.cs)" -eq 35
! grep -rnE 'Thread\.Sleep|Task\.Delay|DateTime\.UtcNow|new Timer\(' Tests/Pontifex.Tests/Transports/RR/Unreliable/Client
python3 - <<'PY'
import glob, os, re, sys
for path in glob.glob('Tests/Pontifex.Tests/Transports/RR/Unreliable/Client/*Tests.cs'):
    source = open(path, encoding='utf-8-sig').read()
    tests = set(re.findall(r'\[Test\][\s\S]*?public void (\w+)', source))
    coverage = open(os.path.join(os.path.dirname(path), 'Coverage.md'),
                    encoding='utf-8-sig').read()
    counts = {}
    for name in re.findall(r'^\|\s*`([A-Za-z0-9_]+)`\s*\|', coverage, re.M):
        counts[name] = counts.get(name, 0) + 1
    covered = set(counts)
    missing = tests - covered
    stale = covered - tests
    duplicates = {name: count for name, count in counts.items() if count != 1}
    for name in sorted(missing):
        print(f'missing coverage: {path} {name}')
    for name in sorted(stale):
        print(f'stale coverage: {path} {name}')
    for name, count in duplicates.items():
        print(f'duplicate coverage: {path} {name} x{count}')
    if missing or stale or duplicates:
        sys.exit(1)
PY
```

Expected: build succeeds; `Transports` = **Passed: 358** (323 + 35 client tests);
`Transports.Concurrency` = **Passed: 33** (30 + 3 client concurrency tests); the client
Coverage file exists with exact identity rows, `Dependency | —`, and a relative contract
link; the client test file has exactly 35 `[Test]` methods, and each test has exactly one
coverage row; manual stage review: all 35 client test names enumerated in
action 2 exist in the named file with no substitutions, and the two deadlock tests carry
`[Timeout(5000)]`; manual README review: the client layer row, directory/harness entries,
contract matrix, category prose/count, and planned-row removal are present; no real timers,
sockets, or sleeps are introduced.

**Definition of done**

The RRUnreliable client layer exists with deterministic coverage of request acceptance,
pending-window TTL/cancel/stop, response correlation, message-count fixing, duplicate
suppression, callback isolation, and send-result paths, and has the README/contract entries
from action 4.

**Risks / notes**

- `Request` may call the response callback before returning; tests must use the callback
  signal and the captured return value, not assume ordering.
- `Response` callbacks run under the state lock; a test that cancels from inside a
  callback must not deadlock.
- Release every captured response payload; the handler owns received frames.

### Stage 4 — Add the RRUnreliableServerTransport layer and finalize the RR branch

**Objective**

The Transports suite covers the RRUnreliable server dispatch, response handle, message
sequence, TTL, and stop contract; the RR branch documentation is complete and both affected
test projects are green.

**Inputs**

- Stage 3 complete.
- `RRUnreliableServerTransport`, `RRUnreliableResponse`, `RRUnreliableFrame`, and the
  RRUnreliable specification.

**Actions**

1. Create `Transports/RR/Unreliable/Server/Coverage.md` (exact identity rows with
   `Dependency | —` and a relative contract link, plus the `## Planned` and
   `## Deferred by design` headings), `RRUnreliableServerTransportTests.cs`, and `Harness/`
   files:
   - `TestRRUnreliableServerTransport.cs` — concrete server with configurable
     `SendFrame` result/throw/capture, exposed `OnFrameReceived`, a manual clock, a
     `StringEndPoint`-based source route helper, and a logger constructor parameter so the
     throwing-handler log test can attach `RecordingLogConsumer`;
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
   - `OnFrameReceived_WithOversizedFrame_ReleasesAndDoesNotPresent` — build the payload
     above `MessageMaxByteSize`, then encode the request header; the header is popped before
     the size check;
   - `OnFrameReceived_BeforeStart_ReleasesWithoutPresenting`;
   - `OnFrameReceived_WhenHandlerNotInitialized_ReleasesWithoutPresenting`;
   - `OnRequested_WhenHandlerAccepts_KeepsHandleOpen`;
   - `OnRequested_WhenHandlerRejects_LaterResponseReturnsError`;
   - `OnRequested_WhenHandlerThrows_LogsRejectsAndTransportStaysValid` — use
     `RecordingLogConsumer` and assert the log entry;
   - `OnRequested_AssignsServerGlobalIdsFromOne`;
   - `Response_SendsClientRequestIdMessageIdAndCount`;
   - `Response_DefaultMessageCountIsOne`;
   - `SetMessageCount_AcceptsOneTo256` — use two separate requests/handles, one set to 1
     and one set to 256; `SetMessageCount` is one-time per handle, so do not loop on one
     handle;
   - `SetMessageCount_RejectsOutOfRange` — call it on a closed handle with 0 and 257 to
     prove the range check precedes the state check;
   - `SetMessageCount_IsOneTimeBeforeFirstResponse`;
   - `SetMessageCount_AfterFirstResponseThrows` — call `SetMessageCount(2)` first, send one
     response, then assert the second call throws while the handle is still open (not because
     the last message closed it);
   - `SetMessageCount_AfterCloseThrows`;
   - `SetMessageCount_AfterExpiryThrows`;
   - `SetMessageCount_AfterStopThrows`;
   - `Response_WhenSendFrameReturnsNonOk_DoesNotAdvanceAndCanRetry`;
   - `Response_WhenSendFrameThrows_ReturnsErrorAndCanRetry`;
   - `Response_WithNullPayload_ReturnsInvalidMessage`;
   - `Response_WithOversizedPayload_ReturnsMessageTooBigAndReleases` — build the payload
     above `MessageMaxByteSize` before the response header is added; the base checks the
     payload size, not the framed size;
   - `Response_DeclaredSequenceSendsZeroBasedIds`; the separate last-message test
     asserts the later `Response` returns `Error`;
   - `Response_AfterLastMessage_ReturnsError`;
   - `Response_AfterResponseTtlExpires_ReturnsError` — the server expiry is strict
     `Now > ExpiresAt`, so advance the clock by `ResponseTtl + 1 tick`; the public
     `IRRUnreliableResponse` surface does not expose closure, so the test asserts the later
     send returns `Error` and does not claim an observable close;
   - `Response_AfterTransportStopped_ReturnsError`;
   - `OnFrameReceived_AfterStop_ReleasesWithoutPresenting`;
   - `OnFrameReceived_DuplicateClientRequest_IsPresentedAgain` — RRUnreliable permits
     duplicate presentation; this pins the absence of base-level suppression;
   - `OnFrameReceived_FromDifferentClients_HaveIndependentHandles`;
   - `Response_ConcurrentCallsProduceOneSequence` — mark `[Timeout(5000)]`; the test double
     records active `SendFrame` calls and blocks the first send on a release gate. Start
     worker A with the first `Response`; wait for its `SendFrame` entry signal before
     starting worker B with the second `Response`. The main thread then waits with bounded
     `SpinWait` until worker B reports `ThreadState.WaitSleepJoin` while A is blocked; treat
     `ThreadState` as a secondary diagnostic and the active-send counter as primary.
     Assert active sends == 1, release A, join both, and assert the two frames carry message
     ids 0 and 1 with no interleaving. Categorized `Transports.Concurrency`.
3. Update the layer Coverage.md, Transports README layer table/directory/harness,
   RRUnreliable contract matrix, categories, the `Transports.Concurrency` prose/count, and
   planned/deferred tables:
   - remove the `RRUnreliableServerTransport` row from the
     `Transports/RR/Unreliable/Generic/Coverage.md` Planned table;
   - record `OnRequested` serialization under `Deferred by design` using the phrase
     `carrier-owned`: the abstract base calls the handler directly and serialization is
     carrier-owned; the Direct suite covers it with `ServerHandler_IsNeverConcurrent`.
4. Finalize the Transports README:
   - selected tree shows the complete RR branch;
   - every RR layer has a row and a coverage link;
   - add the RRUnreliable contract references: `RRUnreliableTransport.cs`,
     `RRUnreliableClientTransport.cs`, `RRUnreliableServerTransport.cs`, the
     `IRRUnreliable*` interfaces, and `Pontifex/Abstractions/Transports/RR/Unreliable/Specification.md`;
   - RR contract matrices for shared, reliable, unreliable client, and unreliable server;
   - categories and commands;
   - out-of-scope: concrete RR carriers, Direct RR suites, conformance, real timing;
   - manually verify the selected tree, directory/harness layout, naming convention, and
     category/filter commands match the final file tree.
5. Ensure no `Planned` entry claims a layer that now exists. Restore the root
   `Tests/Pontifex.Tests/Transports/Coverage.md` statement to
   `No missing layers: the whole selected inheritance tree is covered` once the
   RRUnreliable layers exist. Record any genuinely missing test under the owning layer's
   `Planned` or `Deferred by design`.
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
test -f Tests/Pontifex.Tests/Transports/RR/Unreliable/Server/RRUnreliableServerTransportTests.cs
python3 - <<'PY'
import os, re, sys
base = 'Tests/Pontifex.Tests/Transports/RR'
expected = {
 'Coverage.md': ('RRTransport', 'Pontifex.RR.RRTransport', base, 'Pontifex.AnyTransport'),
 'Reliable/Coverage.md': ('RRReliableTransport',
   'Pontifex.RR.Reliable.RRReliableTransport', f'{base}/Reliable',
   'Pontifex.RR.RRTransport'),
 'Reliable/Generic/Coverage.md': ('RRReliableTransportGeneric',
   'Pontifex.RR.Reliable.RRReliableTransport<THandler>', f'{base}/Reliable/Generic',
   'Pontifex.RR.Reliable.RRReliableTransport'),
 'Reliable/Client/Coverage.md': ('RRReliableClientTransport',
   'Pontifex.RR.Reliable.RRReliableClientTransport', f'{base}/Reliable/Client',
   'Pontifex.RR.Reliable.RRReliableTransport'),
 'Reliable/Server/Coverage.md': ('RRReliableServerTransport',
   'Pontifex.RR.Reliable.RRReliableServerTransport', f'{base}/Reliable/Server',
   'Pontifex.RR.Reliable.RRReliableTransport<THandler>'),
 'Unreliable/Coverage.md': ('RRUnreliableTransport',
   'Pontifex.RR.Unreliable.RRUnreliableTransport', f'{base}/Unreliable',
   'Pontifex.RR.RRTransport'),
 'Unreliable/Generic/Coverage.md': ('RRUnreliableTransportGeneric',
   'Pontifex.RR.Unreliable.RRUnreliableTransport<THandler>', f'{base}/Unreliable/Generic',
   'Pontifex.RR.Unreliable.RRUnreliableTransport'),
 'Unreliable/Client/Coverage.md': ('RRUnreliableClientTransport',
   'Pontifex.RR.Unreliable.RRUnreliableClientTransport', f'{base}/Unreliable/Client',
   'Pontifex.RR.Unreliable.RRUnreliableTransport'),
 'Unreliable/Server/Coverage.md': ('RRUnreliableServerTransport',
   'Pontifex.RR.Unreliable.RRUnreliableServerTransport', f'{base}/Unreliable/Server',
   'Pontifex.RR.Unreliable.RRUnreliableTransport<THandler>'),
}
for rel, (layer, subject, directory, parent) in expected.items():
    path = os.path.join(base, rel)
    text = open(path, encoding='utf-8-sig').read()
    for field, value in (('Layer', layer), ('Subject', subject),
                         ('Directory', directory), ('Inheritance parent', parent)):
        if f'| {field} | `{value}` |' not in text:
            print(f'bad {field}: {path} expected {value}')
            sys.exit(1)
    if 'Direct test assembly' in text or 'Tests/Pontifex.Transport.Direct.Tests' in text:
        print(f'stale dependency text: {path}')
        sys.exit(1)
    for link in re.findall(r'\]\(([^)]+)\)', text):
        if link.startswith(('..', '.')):
            target = os.path.normpath(os.path.join(os.path.dirname(path), link))
            if not os.path.exists(target):
                print(f'broken link: {path} {link}')
                sys.exit(1)
PY
test "$(grep -c '\[Test\]' Tests/Pontifex.Tests/Transports/RR/Unreliable/Server/RRUnreliableServerTransportTests.cs)" -eq 32
python3 - <<'PY'
import glob, re, sys
expected = {
 'TerminalKeepsTheHandlerBoundUntilTheFinishCallbackReturns',
 'CancelOutsideACallback_DeliversOneCanceledAndSuppressesLateResponses',
 'Stop_SchedulesOneFailedPerPendingRequestBeforeOnStoppedAndDeliversAfter',
 'ConcurrentResponsesForDifferentRequests_AreDeliveredSafely',
 'ResponseAndFinish_ProduceOrderedFramesWithServerIdsFromOne',
 'ResponseOrdinal_IsCommittedOnlyAfterOk',
 'ResponseHandle_MatrixOfFinishRejectAndClosedCalls',
 'Finish_WhenTheSendFails_LeavesTheHandleRetryable',
 'Reject_WhenTheSendFails_LeavesTheHandleRetryable',
 'Reject_AfterTheResponseTtlClosedTheHandle_ReturnsError',
 'Reject_ClosesTheHandleAndRepeatedRejectReturnsOk',
 'Cancel_ClosesTheLiveHandleAndSuppressesLaterResponses',
 'CancelOfAQueuedRequest_PreventsPresentation',
 'ResponseTtlWhileQueued_SendsFailedAndNeverPresents',
 'FullDispatchQueue_ReleasesTheOverflowingRequest',
 'DuplicateRequest_IsSuppressedLiveAndAfterEveryTerminalCause',
 'TwoClientsWithTheSameClientLocalId_DoNotSuppressEachOther',
 'OnRequested_IsSerializedAcrossRequests',
 'ThrowingHandler_RejectsTheUndecidedHandle',
 'RequestBeforeOnStarted_IsHeldUntilTheReadyGateOpens',
 'FullPendingListBeforeStart_ReleasesTheOverflowingFrame',
 'StopDispatch_ReleasesARequestStillQueued',
 'OnSessionClosed_ClosesOnlyThatSession',
 'BlockedResponse_WithConcurrentStop_DoesNotHang',
 'TwoThreadsOnOneHandle_ProduceOneTerminalResult',
 'Response_Callbacks_AreSerializedPerRequest',
 'Stop_FromInsideAResponseCallback_DoesNotDeadlock',
 'Request_FromInsideAResponseCallback_IsAccepted',
 'Response_ConcurrentCallsProduceOneSequence',
}
found = set()
for path in glob.glob('Tests/Pontifex.Tests/Transports/RR/**/*Tests.cs', recursive=True):
    source = open(path, encoding='utf-8-sig').read()
    for match in re.finditer(r'\[Test\]([\s\S]*?)public void (\w+)', source):
        if 'Transports.Concurrency' in match.group(1):
            found.add(match.group(2))
if found != expected:
    print('concurrency category mismatch:', sorted(found ^ expected))
    sys.exit(1)
PY
for f in $(find Tests/Pontifex.Tests/Transports/RR -name Coverage.md); do grep -q '^## Planned' "$f" || { echo "missing Planned: $f"; exit 1; }; grep -q '^## Deferred by design' "$f" || { echo "missing Deferred: $f"; exit 1; }; done
! grep -rnE 'Thread\.Sleep|Task\.Delay|DateTime\.UtcNow|new Timer\(' Tests/Pontifex.Tests/Transports/RR/Unreliable/Server
for layer in RRTransport RRReliableTransport RRReliableTransportGeneric RRReliableClientTransport RRReliableServerTransport RRUnreliableTransport RRUnreliableTransportGeneric RRUnreliableClientTransport RRUnreliableServerTransport; do grep -q "| \`$layer\` |" Tests/Pontifex.Tests/Transports/README.md || { echo "missing README row: $layer"; exit 1; }; done
for path in RR/Coverage.md RR/Reliable/Coverage.md RR/Reliable/Generic/Coverage.md RR/Reliable/Client/Coverage.md RR/Reliable/Server/Coverage.md RR/Unreliable/Coverage.md RR/Unreliable/Generic/Coverage.md RR/Unreliable/Client/Coverage.md RR/Unreliable/Server/Coverage.md; do grep -q "($path)" Tests/Pontifex.Tests/Transports/README.md || { echo "missing README coverage link: $path"; exit 1; }; done
for ref in RRTransport.cs RRReliableTransport.cs RRReliableClientTransport.cs RRReliableServerTransport.cs RRUnreliableTransport.cs RRUnreliableClientTransport.cs RRUnreliableServerTransport.cs; do grep -q "\[$ref\](" Tests/Pontifex.Tests/Transports/README.md || { echo "missing README contract reference: $ref"; exit 1; }; done
for spec in RR/Specification.md RR/Reliable/Specification.md RR/Unreliable/Specification.md; do grep -q "$spec" Tests/Pontifex.Tests/Transports/README.md || { echo "missing README spec reference: $spec"; exit 1; }; done
! grep -q "cannot be made to throw through its public contract" Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableCoreHelpers/Coverage.md
python3 - <<'PY'
import glob, os, re, sys
for path in glob.glob('Tests/Pontifex.Tests/Transports/RR/**/*Tests.cs', recursive=True):
    source = open(path, encoding='utf-8-sig').read()
    tests = set(re.findall(r'\[Test\][\s\S]*?public void (\w+)', source))
    coverage = open(os.path.join(os.path.dirname(path), 'Coverage.md'),
                    encoding='utf-8-sig').read()
    counts = {}
    for name in re.findall(r'^\|\s*`([A-Za-z0-9_]+)`\s*\|', coverage, re.M):
        counts[name] = counts.get(name, 0) + 1
    covered = set(counts)
    missing = tests - covered
    stale = covered - tests
    duplicates = {name: count for name, count in counts.items() if count != 1}
    for name in sorted(missing):
        print(f'missing coverage: {path} {name}')
    for name in sorted(stale):
        print(f'stale coverage: {path} {name}')
    for name, count in duplicates.items():
        print(f'duplicate coverage: {path} {name} x{count}')
    if missing or stale or duplicates:
        sys.exit(1)
PY
grep -q "ConcurrentMarkCompletedAndQueries_AreLockSafeAndExactlyOnce" Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableCoreHelpers/Coverage.md
grep -q "ConcurrentCloseAndMarkCompleted_AreExactlyOnce" Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableCoreHelpers/Coverage.md
grep -q "CompletedWindow_IsPerSessionAndFullAfterEveryId" Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableCoreHelpers/Coverage.md
! grep -q "CompletedWindow_IsPerSessionAndFullAfterEveryId" Tests/Pontifex.Tests/Transports/RR/Reliable/Server/Coverage.md
grep -q "RRReliableServerSession" Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableCoreHelpers/README.md
grep -q "No missing layers: the whole selected inheritance tree is covered" Tests/Pontifex.Tests/Transports/Coverage.md
! sed -n '/^## Planned/,/^## /p' Tests/Pontifex.Tests/Transports/RR/Coverage.md | grep -q "RRUnreliable"
! sed -n '/^## Planned/,/^## /p' Tests/Pontifex.Tests/Transports/RR/Unreliable/Coverage.md | grep -q "RRUnreliableClientTransport"
! sed -n '/^## Planned/,/^## /p' Tests/Pontifex.Tests/Transports/RR/Unreliable/Generic/Coverage.md | grep -q "RRUnreliableServerTransport"
grep -qi "random request-id seed" Tests/Pontifex.Tests/Transports/RR/Unreliable/Client/Coverage.md
grep -qi "wraparound" Tests/Pontifex.Tests/Transports/RR/Unreliable/Client/Coverage.md
grep -qi "generator" Tests/Pontifex.Tests/Transports/RR/Unreliable/Client/Coverage.md
grep -qi "BufferOverflow" Tests/Pontifex.Tests/Transports/RR/Unreliable/Client/Coverage.md
sed -n '/^## Deferred/,/^## /p' Tests/Pontifex.Tests/Transports/RR/Unreliable/Server/Coverage.md | grep -qi "carrier-owned"
# The two affected test projects are run above; do not gate on unrelated projects.
```

Expected:

- build succeeds with no errors;
- `Transports` = **Passed: 390** (235 baseline + 74 moved + 3 Stage 1 new + 11 Stage 2 +
  35 Stage 3 + 32 Stage 4);
- `Transports.Concurrency` = **Passed: 34** (5 baseline Raw tests + 25 moved tests + 4 new
  Stage 3/4 tests; the category-member script passes);
- `Transports.RR` = **Passed: 155**;
- `Transports.RR.Reliable` = **Passed: 62**;
- `Transports.RR.Unreliable` = **Passed: 78**;
- full `Pontifex.Tests` = **Passed: 985** (830 baseline + 155);
- full `Pontifex.Transport.Direct.Tests` = **Passed: 315**;
- `grep -rn 'Category("Direct'` prints no output;
- `git status --porcelain -- Pontifex/` prints no output (no production changes);
- all nine layer Coverage files exist, each with exact Layer/Subject/Directory/
  Inheritance-parent rows, no old Direct dependency text, resolving relative links, and
  `## Planned` plus `## Deferred by design`;
- every layer has a README row and an exact coverage link;
- the README contract-reference list has exact production link text and the three RR
  specification links;
- each RR test file's coverage table exactly matches its own `[Test]` methods, with no
  missing or stale rows (names may repeat across different layer files);
- the server test file has exactly 32 `[Test]` methods;
- manual stage review: all 32 server test names enumerated in action 2 exist in the named
  file with no substitutions;
- manual README review: the selected tree shows the complete RR branch; all nine layer
  rows have correct subject/directory/parent/coverage; the directory and harness layouts,
  contract matrices, category prose/count (34), filter commands, and out-of-scope list are
  complete; no planned coverage status remains in the README;
- the three CoreHelpers tests are present in CoreHelpers coverage, `CompletedWindow` is gone
  from the reliable server coverage, and the CoreHelpers README names
  `RRReliableServerSession`;
- the root `Transports/Coverage.md` again says
  `No missing layers: the whole selected inheritance tree is covered`;
- the stale CoreHelpers `GetDataSize` sentence is gone.

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
- `RRReliableServerSessionTests` lives in the Direct CoreHelpers suite with a fixture-level
  `Direct.RRReliableCoreHelpers`; its two stress methods carry
  `Direct.RRReliable.Integration`. The CoreHelpers README/Coverage list it.
- The RRUnreliable client seed-determinism/wraparound/generator-failure gap and the
  carrier-owned server callback-serialization gap are recorded under `Deferred by design`
  with exact reasons.
- The full verification set passes:
  - solution build;
  - `Transports` = 390 passed;
  - `Transports.Concurrency` = 34 passed;
  - `Transports.RR` = 155 passed;
  - `Transports.RR.Reliable` = 62 passed;
  - `Transports.RR.Unreliable` = 78 passed;
  - full `Pontifex.Tests` = 985 passed;
  - full `Pontifex.Transport.Direct.Tests` = 315 passed;
  - `grep -rn 'Category("Direct' Tests/Pontifex.Tests/Transports` prints no output;
  - `git status --porcelain -- Pontifex/` prints no output.
- All nine RR layer `Coverage.md` files exist and are referenced from the README.
- The plan file is deleted and the deletion committed with
  `Remove completed implementation plan`; after that commit `git status --porcelain` is
  empty and `test ! -f plan.md` succeeds.
