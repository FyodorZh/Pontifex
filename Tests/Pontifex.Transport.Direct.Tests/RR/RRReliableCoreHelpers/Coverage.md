# RRReliable Core Helpers Coverage

| Field | Value |
| --- | --- |
| Layer | Root |
| Subject | RR reliable core helper group including `RRReliableServerSession` |
| Directory | `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableCoreHelpers/` |
| Inheritance parent | none |
| Dependency | The helpers are internal; the suite relies on the Pontifex assembly's `InternalsVisibleTo` entry. |
| Contract | [RRReliable specification](../../../../Pontifex/Abstractions/Transports/RR/Reliable/Specification.md), [IRRReliableTimerFactory](../../../../Pontifex/Abstractions/Transports/RR/Reliable/IRRReliableTimerFactory.cs) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `RequestFrame_RoundTripsHeaderAndPayload` | Wire header | A request header decodes to the type and client-local id, leaving the payload untouched | The payload stays in wire order after the header is popped. |
| `ResponseFrame_RoundTripsOrdinalAndPayload` | Response ordinal | A response header decodes to its id and ordinal | The ordinal is encoded as the bit-identical `int` and decoded through `unchecked`. |
| `ResponseFrame_RoundTripsUIntMaxOrdinal` | Response ordinal domain | `uint.MaxValue` survives the `int` round trip | Proves the encoding keeps the full unsigned domain. |
| `FinishFrame_RoundTripsEveryCompletion` | Terminal notification | Every completion type decodes | The completion travels as one byte element. |
| `CancelFrame_RoundTripsHeader` | Best-effort cancellation | A cancel header decodes and carries no payload | The payload is absent by design. |
| `Decode_KeepsTheFrameAlive` | Buffer ownership | Decoding does not release the frame | The caller releases the remainder. |
| `UnknownFrameType_IsRejected` | Malformed input | An unknown type byte is rejected | The caller releases the remainder. |
| `MissingRequestId_IsRejected` | Malformed input | A header without a request id is rejected | A short frame is not a valid request or cancel. |
| `ResponseWithoutOrdinal_IsRejected` | Malformed input | A response without an ordinal is rejected | The ordinal is mandatory for stream ordering. |
| `ZeroOrdinal_IsRejected` | Ordinal domain | Ordinal zero is not a response | Ordering starts at one. |
| `UnknownCompletion_IsRejected` | Malformed input | An unknown completion byte is rejected | Only the four declared completion types are valid on the wire. |
| `HeaderConstants_MatchTheSerializedOverhead` | Header size | The declared per-kind sizes equal the measured serialized overhead | The overhead excludes the payload and its list-count prefix. |
| `MaximumHeader_AccommodatesTheCountPrefixGrowth` | Frame size boundary | The largest response header plus one spare byte covers the count-prefix growth | A sixty-three-element payload grows its prefix when three header elements are prepended; this is the `+1` in `FrameMaxByteSize`. |
| `EncodeRequest_WithNullFrame_Throws` | Encoder guard | A null frame throws | The encoder never owns a null frame. |
| `EncodeResponse_WithNullFrame_Throws` | Encoder guard | A null response frame throws | Same ownership rule. |
| `EncodeResponse_WithZeroOrdinal_Throws` | Ordinal domain | Encoding ordinal zero throws | Mirrors decode rejection. |
| `Decode_WithNullFrame_Throws` | Decoder guard | A null frame throws | The decoder never owns a null frame. |
| `NewState_IsProvisionalWithTheFirstExpectedOrdinal` | Client-local state | A fresh request is provisional, expects ordinal one, and has no terminal | The transport adds the state under a provisional entry. |
| `TryAccept_MovesOnlyOnceToAccepted` | Request transition | Provisional to accepted happens once | A second accept reports no transition. |
| `TryMarkTerminalSilently_LeavesNoPendingCompletion` | Silent removal | A provisional request becomes terminal without a completion | Only a provisional request may be removed silently. |
| `TryBeginTerminal_IsIdempotentAndKeepsOnePendingCompletion` | Terminal transition | The first terminal wins and stores one pending completion | Later causes are silent. |
| `TryBeginTerminal_WhileACallbackRuns_IsDeferred` | Deferred terminal | A terminal requested inside a callback is marked, not delivered reentrantly | The caller delivers it after the callback returns. |
| `CallbackGate_RejectsReentrantEntry` | Callback serialization | The per-request callback gate rejects reentrant entry | Callback execution belongs to the transport, which uses this flag. |
| `TryRequestCancel_SetsTheFlagOnlyOnceAndNotAfterTerminal` | Cancellation | Cancellation is recorded once and refused after terminal | The flag suppresses later responses. |
| `ProvisionalBuffer_PreservesWireOrderAndTransfersOwnership` | Provisional buffering | Responses and terminal frames buffer in wire order and ownership passes out on take | The transport drains the buffer after `Request` returns `Ok`. |
| `ProvisionalBuffer_RefusesFramesAfterAcceptance` | Provisional buffering | A non-provisional request refuses a new provisional frame | The caller keeps ownership on refusal. |
| `ReleaseProvisionalBuffer_ReleasesEveryFrame` | Provisional ownership | Every buffered frame is released on discard | Used when `Request` returns non-`Ok`. |
| `AcceptedResponse_IsReadyInOrderAndDuplicatesAreDropped` | Ordered responses | Ordinal one is ready, three buffers, a duplicate three is dropped, two fills the gap, then three drains | Lower ordinals never deliver twice. |
| `LowerOrdinal_IsDroppedAndReleased` | Duplicate suppression | An ordinal below the expected value and ordinal zero are dropped and released | The state owns and releases a dropped frame. |
| `ProvisionalResponse_IsDroppedAndReleased` | Phase guard | A response for a provisional request is dropped and released | Provisional responses go through the provisional buffer instead. |
| `BufferedResponses_DrainAfterTheTerminalTransition` | Ordered completion | A response buffered before the terminal frame still drains after the terminal transition | Prevents the state from hiding accepted responses behind the completion. |
| `ReleaseBufferedResponses_ReleasesEveryOutOfOrderResponse` | Buffer ownership | Out-of-order responses are released on discard | Used on a terminal close. |
| `HeldTerminal_WaitsForTheMissingExpectedOrdinal` | Held terminal | A terminal frame is held until the missing expected ordinal arrives and then drains the buffered responses | The terminal position is the highest known ordinal at arrival. |
| `HeldTerminal_CommitsImmediatelyWithoutAResponse` | Zero-response completion | A terminal with no buffered response commits immediately | Zero-response finishes are not delayed. |
| `AResponseAboveTheHeldTerminalPosition_IsDropped` | Held terminal suppression | A response above the held terminal position is dropped and released | It belongs to a pre-terminal stream position. |
| `AFirstTerminalCause_OverridesTheHeldTerminal` | Terminal precedence | A later first cause overrides a held terminal that has not committed | Stop and cancel win over a delayed server terminal. |
| `TrySetTimer_IsRefusedAfterTheTerminalTransition` | Timer ownership | A timer is stored while live, disposed on close, and refused after terminal | The caller disposes a refused handle. |
| `AddAndGet_ReturnsTheEntryWithoutRemovingIt` | Non-removing lookup | A response lookup keeps the entry live | A reliable request spans many responses and one terminal frame. |
| `AddDuplicate_ReturnsFalseAndKeepsTheFirstEntry` | Id uniqueness | A duplicate id reports failure and keeps the first entry | The client allocates ids through the retry loop. |
| `TakeExact_RemovesOnlyTheMatchingState` | Owner removal | Removal succeeds only for the exact state reference | Guards against removing a newer request with a reused id. |
| `Remove_RemovesTheEntryAndReportsWhetherItExisted` | Removal | Removal reports whether the id was present | Used for silent and terminal removals. |
| `Clear_InvokesEveryEntryAndEmptiesTheTable` | Stop clearing | Clear visits every entry and empties the table | The callback runs while the table is locked, matching the unreliable table. |
| `TakeAll_RemovesEveryEntryOutsideTheTableLock` | Stop clearing | TakeAll removes every entry and returns them for processing outside the table lock | The client clears provisional entries without nesting a state transition under the table lock. |
| `ConcurrentAccess_AddsEveryEntryAndReadsThemSafely` | Lock protection | Parallel adds and reads see a consistent table | No entry is lost or observed with a foreign state. |
| `CompletedWindow_IsPerSessionAndFullAfterEveryId` | Session window | The 65,536-bit window fills after every id and a fresh session starts empty | The transport rejects new ids only when its own session window is full. |
| `ConcurrentMarkCompletedAndQueries_AreLockSafeAndExactlyOnce` | Session concurrency | Parallel mark/query calls are lock-safe and every marked id becomes completed | Runs in the `Tests/Integration/` group and carries `Direct.RRReliable.Integration`. |
| `ConcurrentCloseAndMarkCompleted_AreExactlyOnce` | Session concurrency | A parallel close racing mark-completed calls is exception-free and leaves the session closed | Runs in the `Tests/Integration/` group and carries `Direct.RRReliable.Integration`. |
| `Schedule_WithNullCallback_Throws` | Factory guard | A null callback throws | The factory never schedules a null action. |
| `Schedule_WithNegativeDelay_Throws` | Factory guard | A negative delay throws | TTL delays are positive. |
| `Schedule_WithALongDelay_DoesNotFireSynchronouslyAndDisposeCancelsIt` | Factory disposal | Scheduling is non-blocking and disposing the handle cancels it | Proves no real time is waited for; the fire path is deferred. |
| `Dispose_IsIdempotent` | Factory disposal | Disposing a handle twice is safe | The core may dispose a timer more than once on racing closes. |
| `Schedule_RecordsTheDelayAndCallback` | Manual timer support | Scheduling records one pending callback with its delay | Shared loose support; it lets a test assert that work was scheduled before `onStopped`. |
| `Fire_RunsTheCallbackOnlyOnce` | Manual timer support | Firing runs the callback once and closes the handle | A second fire is a no-op. |
| `Suppress_ClosesTheHandleWithoutRunningTheCallback` | Manual timer support | Suppression closes a handle without a callback | Models a cancelled TTL. |
| `Dispose_ClosesTheHandleWithoutRunningTheCallback` | Manual timer support | Disposal closes a handle without a callback and is idempotent | Models a closed request. |
| `FireAll_RunsEveryPendingCallback` | Manual timer support | Every pending handle fires and the pending count reaches zero | Used to drive several TTLs without ordering assumptions. |
| `Schedule_WithNullCallback_Throws` (manual) | Manual timer support | The manual factory rejects a null callback | Mirrors the production contract. |
| `Schedule_WithNegativeDelay_Throws` (manual) | Manual timer support | The manual factory rejects a negative delay | Mirrors the production contract. |

## Planned

| Missing test | Owner layer |
| --- | --- |
| Client and server transport use of the state, table, and timer | The abstract RR transport suite under `Tests/Pontifex.Tests/Transports/RR/Reliable`, client and server layers. |
| Real timer fire and callback delivery | Root — deferred by design; the manual factory drives every deterministic TTL test. |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Firing the production timer with real time | The suite is deterministic and does not wait; firing is exercised through the manual factory in the transport layers. |
