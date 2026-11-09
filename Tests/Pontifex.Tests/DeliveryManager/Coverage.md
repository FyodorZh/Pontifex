# DeliveryManager Layer Coverage

- **Layer:** DeliveryManager (root layer)
- **Suite type:** Test Suit
- **Subject:** the `Pontifex.Delivery` delivery subsystem: `DeliveryManager`,
  `DeliveryReporter`, `DeliveryDispatcher`, `DeliveryInfoSerializer`, `Deduplicator`,
  `DeliverySorter<T>`, `DeliverySortingManager`, `MessagePacker`, `MessageSplitter`,
  `MessageMerger`, and `RetryDeliveryScheduler`
- **Directory:** `Tests/Pontifex.Tests/DeliveryManager`
- **Inheritance parent:** — (Test Suit root layer)
- **Contract:** [how-it-works.md](../../../Pontifex/Utils/DeliveryManager/how-it-works.md),
  [IDeliveryManager.cs](../../../Pontifex/Utils/DeliveryManager/IDeliveryManager.cs),
  [DeliveryManager.cs](../../../Pontifex/Utils/DeliveryManager/DeliveryManager.cs),
  [DeliveryDispatcher.cs](../../../Pontifex/Utils/DeliveryManager/DeliveryDispatcher.cs),
  [DeliveryReporter.cs](../../../Pontifex/Utils/DeliveryManager/DeliveryReporter.cs),
  [DeliveryInfoSerializer.cs](../../../Pontifex/Utils/DeliveryManager/Serialization/DeliveryInfoSerializer.cs),
  [Deduplicator.cs](../../../Pontifex/Utils/DeliveryManager/Deduplicator.cs),
  [DeliverySorter.cs](../../../Pontifex/Utils/DeliveryManager/Sorting/DeliverySorter.cs),
  [DeliverySortingManager.cs](../../../Pontifex/Utils/DeliveryManager/Sorting/DeliverySortingManager.cs),
  [MessagePacker.cs](../../../Pontifex/Utils/DeliveryManager/MessagePacker.cs),
  [MessageSplitter.cs](../../../Pontifex/Utils/DeliveryManager/SplitAndMerge/MessageSplitter.cs),
  [MessageMerger.cs](../../../Pontifex/Utils/DeliveryManager/SplitAndMerge/MessageMerger.cs),
  [RetryDeliveryScheduler.cs](../../../Pontifex/Utils/DeliveryManager/Scheduler/RetryDeliveryScheduler.cs)

## Covered tests

### Deduplicator

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `FirstId_ReturnsNew` | The first id seen in an empty window is new. | A fresh deduplicator accepts its first packet. | Capacity 8. |
| `SequentialIds_AllNew` | In-order ids inside the window are new. | Normal sequential traffic is not deduplicated. | Capacity 16. |
| `SameIdTwice_ReturnsDuplicate` | Repeating a seen id reports a duplicate. | Retransmission detection works for the most recent id. | — |
| `IdInWindow_AlreadyReceived_ReturnsDuplicate` | A previously seen id still inside the window is a duplicate. | Out-of-order retransmissions are detected inside the window. | Ids 1–3 received, then 2 again. |
| `IdBelowFrom_ReturnsDuplicate` | Ids below the window's lower bound are treated as old. | The window does not need to retain every historical id. | Ids 1–9 received before querying 4. |
| `GapFromEmpty_FillsAndMarksNew` | A forward gap within capacity returns `New`. | Gaps smaller than the capacity are not overflows. | Ids 1 then 5; the skipped range is represented in the window. |
| `Overflow_WhenGapExceedsCapacity` | A gap larger than the window is an overflow. | The capacity bound is enforced. | Capacity 4; 1 then 10. |
| `Trim_RemovesLeadingConfirmed` | Acknowledged leading ids are trimmed from the window. | The window advances without losing duplicate detection for the remaining ids. | Ids 1–8 then 9; 9 is still a duplicate on repeat and 10 is new. |
| `LargeSequentialRange_AllNew` | A long sequential run inside a large window is accepted. | Production-scale window capacity does not reject normal traffic. | Capacity 1024, 100 ids. |
| `RapidSameId_StaysDuplicate` | Repeated queries for the same id remain duplicates. | Duplicate detection is stable, not one-shot. | Ten repeats. |
| `NewIdAfterTrim_Works` | After the window advances, older ids are duplicates and newer ids are new. | Trim preserves the distinction between dropped and unseen ids. | Capacity 4, ids 1–8 queried. |
| `OverflowAtFullCapacity_ReturnsOverflow` | A far-ahead id overflows even when the window is full. | Overflow is driven by the distance, not by the number of stored ids. | Capacity 4; ids 1–4 then 100. |

### DeliveryDispatcher

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `ScheduleDeliver_ReturnsOk` | A fresh task is admitted. | Scheduling succeeds below capacity. | Capacity 10. |
| `ScheduleDeliver_DuplicateId_ReturnsIdIsNotUnique` | The same `DeliveryInfo` cannot be scheduled twice. | Per-chunk admission is unique. | Duplicate is detected by `DeliveryInfo`, not just `DeliveryId`. |
| `ScheduleDeliver_DoesNotModifyOriginalData` | Admission clones the caller's data. | The dispatcher owns its copy; the caller's element list is unchanged. | The test keeps an extra reference on the input list while scheduling. |
| `ScheduleDeliver_AtCapacity_ReturnsBufferOverflow` | Capacity is a hard admission bound. | A full dispatcher rejects the next task. | Capacity 2. |
| `TryToDeliver_NoDueTasks_SendsNothing` | Tasks are not sent before their schedule time. | Time gating prevents early transmission. | The pump time is one hour before the schedule time. |
| `TryToDeliver_DueTask_SendsOnce` | A due task is transmitted once with its wire envelope. | The first dispatch emits the discriminator and wire chunk id ahead of the packed data. | The first element is the `true` user discriminator read through the union alias as `ushort` 1. |
| `TryToDeliver_ConfirmedDelivery_Skipped` | A confirmed task is not transmitted. | Acknowledgement removes pending work before it is due. | Confirmation happens before the due pump. |
| `TryToDeliver_Retry_SendsMultipleTimes` | A task is retransmitted while the scheduler permits it. | Retry keeps sending the same task at the scheduled intervals. | Base interval 50 ms; pumps at 0/100/200 ms; timeout 10 s. |
| `TryToDeliver_Failure_FiresFailedToDeliver` | Exhausting the retry budget reports failure. | `FailedToDeliver` fires once when the scheduler gives up. | Timeout 50 ms; second pump 200 ms after scheduling. |
| `ConfirmDelivered_SingleChunk_FiresDelivered` | Confirming the only chunk reports delivery. | Single-chunk messages fire `Delivered` immediately. | — |
| `ConfirmDelivered_MultiChunk_AllConfirmed_FiresOnce` | Delivery fires only after every chunk is confirmed. | The per-message chunk counter suppresses early delivery. | Two chunks; first confirmation emits nothing. |
| `ConfirmDelivered_NonExistent_NoOp` | Confirming an unknown id is harmless. | Stray acknowledgements do not fire events. | — |
| `ConfirmDelivered_DoubleAck_IsNoOp` | A second acknowledgement is ignored. | Delivery is one-shot per message. | — |
| `Clear_EmptiesQueue` | `Clear` drops pending tasks. | A cleared dispatcher transmits nothing. | Pump time is one hour after scheduling. |
| `MultipleTasks_AllDue_AllSent` | All due tasks are transmitted. | The pump drains every due entry. | Three tasks share a schedule time; order is deliberately not asserted. |
| `NewFormat_DispatchedData_HasDiscriminatorAndWireChunkId` | The dispatched wire message has `[true, wireChunkId]` before the packed data. | The wire envelope is the dispatcher's responsibility, not the packer's. | Wire chunk ids start at 1. |
| `NewFormat_Retry_PreservesDiscriminator` | Retransmission reuses the same wire envelope. | Retry does not rebuild or corrupt the envelope. | The first consumer releases its reference without retaining the message. |
| `NewFormat_SequentialDispatches_HaveUniqueWireChunkIds` | Each scheduled chunk gets a unique wire chunk id. | The wire chunk id is a per-chunk sequence, not the delivery id. | Three sequential admissions produce ids 1, 2, 3. |
| `ScheduleDeliver_AfterWireChunkIdWrap_RestartsAtOne` | The 16-bit wire chunk id wraps from 65,535 to 1. | The sequence never emits 0. | The loop clears each task to keep memory flat while advancing 65,535 admissions. |
| `ScheduleDeliver_WithNonPositiveCapacity_ReturnsBufferOverflow` | Capacity must be positive for admission to succeed. | Zero and negative capacity behave as a full queue. | Runs for capacity 0 and -1. |

### DeliveryInfoSerializer

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `CreateDeliveryReport_SingleConfirmation_WritesCountIdAndChunkId` | A report is `ushort(count)` followed by `(ushort id, byte chunkId)` pairs. | The single-pair wire layout is exact. | — |
| `CreateDeliveryReport_MultipleConfirmations_WritesAllPairs` | Multiple confirmations are written in order. | The report preserves confirmation order and count. | — |
| `CreateDeliveryReport_PartialRange_HonorsStartAndCount` | `start` and `count` select a slice of the source list. | The reporter can batch a list without copying it. | Start 1, count 2. |
| `CreateDeliveryReport_ZeroCount_WritesCountOnly` | An empty batch is a single zero count. | Zero-length reports are well-formed. | — |
| `LoadDeliveryReport_Valid_ReturnsTrueAndPopulatesList` | A well-formed report loads into `CurrentDeliveryReport`. | Parsing exposes id and chunk id. | The report message is released after the load. |
| `LoadDeliveryReport_MultipleConfirmations_PopulatesAll` | All pairs are parsed in order. | Multi-confirmation reports round-trip. | — |
| `LoadDeliveryReport_WrongFirstElement_ReturnsFalse` | The first element must be a `ushort` count. | Type validation rejects malformed reports. | The list contains a byte followed by valid-looking pairs. |
| `LoadDeliveryReport_TruncatedAfterCount_ReturnsFalse` | A count without its pairs is malformed. | Truncation is rejected instead of partially accepted. | Count 5 with no pairs. |
| `LoadDeliveryReport_TruncatedPair_ReturnsFalse` | A pair must contain both an id and a chunk id. | A partially written pair is rejected. | Count 1 followed by an id only. |
| `LoadDeliveryReport_CountZero_ReturnsTrueEmptyList` | A zero count is valid and yields an empty report. | Empty reports are accepted. | — |
| `RoundTrip_DeliveryInfo_PreservesIdsAndChunks` | Create followed by load preserves every id/chunk pair. | The serializer is symmetric for valid input. | Two confirmations. |

### DeliveryManager

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `ScheduleDelivery_SingleChunk_Ok` | Small user data is accepted. | The single-chunk fast path works through the facade. | `MaxMsgSize` 100. |
| `ScheduleDelivery_InvalidData_ReturnsInvalidMessage` | `null` data is rejected. | Invalid input is reported, not thrown. | — |
| `ScheduleDelivery_MessageTooBig_ReturnsMessageTooBig` | Data over `DeliveryMaxByteSize` is rejected. | The size guard is enforced before packing. | 30,000 bytes. |
| `DeliveryMaxByteSize_ReturnsCorrectValue` | The maximum is `(messageMaxByteSize - multiOverhead - safetyMargin) * 255`. | The public limit matches the documented wire size arithmetic. | 100-byte message size yields 21,930 bytes. |
| `ProcessIncoming_MalformedDeliveryReport_ReturnsFalse` | A non-user packet with an invalid report is rejected. | Malformed acknowledgements do not confirm deliveries. | The report declares five confirmations but carries none. |
| `ProcessOutgoing_ProducesBuffer` | A scheduled message is emitted on the next pump. | Scheduling feeds the outgoing queue. | The captured message is released by the fixture. |
| `RoundTrip_SingleChunk_ReceivedFires` | A single-chunk message is delivered with its id and payload. | End-to-end send/receive through two managers. | Payload is copied out; the handler owns and releases the received buffer inside the callback. |
| `DuplicateInbound_ReceivedFiresOnce` | A retransmitted message is delivered once. | Inbound deduplication is active at the facade level. | The same wire message is processed twice. |
| `AckRoundTrip_DeliveredFires` | An acknowledgement round trip fires `Delivered` on the sender. | Delivery confirmation crosses the two managers. | Acknowledged id equals the scheduled id. |
| `DeliveryFailure_FiresFailedToDeliver` | An unacknowledged message reports failure after retries. | The retry timeout is observable through the facade. | Timeout 50 ms, base interval 100 ms, second pump 200 ms later. |
| `Clear_ThenProcessOutgoing_ProducesNothing` | `Clear` discards scheduled outgoing work. | A reset prevents stale transmissions. | — |
| `ProcessIncoming_AfterClear_StillProcesses` | `Clear` does not break the inbound path. | Outgoing reset and inbound processing are independent. | A message scheduled before `Clear` is still delivered inbound. |
| `MultipleSends_AllReceived` | Multiple scheduled messages are all delivered. | The pipeline handles a small burst. | Five messages, delivered through one pump. |
| `ProcessIncoming_GarbageBytes_ReturnsFalse` | Malformed/truncated wire data is rejected. | Garbage does not throw or fire events. | The test supplies a `ushort` followed by a byte that cannot complete a message. |
| `ProcessIncoming_WireChunkIdBeyondWindow_ReturnsFalse` | A wire chunk id jump beyond the deduplication window is an overflow. | The facade rejects the packet without delivering or throwing. | Chunk id 2,000 against the 1,024-entry window. |
| `ScheduleDelivery_EmptyData_RoundTripsEmptyPayload` | Zero-byte user data is valid. | Empty payloads survive packing, dispatch, and delivery. | The received payload is observed as an empty element list. |
| `ProcessOutgoing_WithoutSchedule_ProducesNothing` | An idle manager emits nothing. | The pump is side-effect-free when there is no work. | — |
| `MultipleProcessOutgoing_ConfirmationsAccumulate` | A received message produces one outgoing report. | The report is flushed once, not once per pump. | A second pump produces no report. |
| `MultiChunkRoundTrip_WithLargeData` | Data larger than the single-chunk limit is split and reassembled. | The multi-chunk path preserves payload bytes. | 150 bytes, more than one chunk. |
| `MultiChunkRoundTrip_OutOfOrder` | Chunks may arrive out of order. | Reassembly is order-independent within one message. | Chunks are fed in reverse order. |
| `MultiChunk_DuplicateChunk_ReceivedFiresOnce` | A duplicate chunk after completion does not re-deliver. | Inbound deduplication covers multi-chunk messages. | The first chunk is replayed after the complete message. |
| `MultiChunkAckRoundTrip_DeliveredFires` | All chunks must be acknowledged before delivery fires. | Multi-chunk acknowledgement counting works at the facade. | Sender reports `Delivered` with the scheduled id. |
| `MultiChunkRetryTimeout_FiresFailedToDeliver` | An unacknowledged multi-chunk message reports failure. | Retry exhaustion applies to the whole message. | Timeout 50 ms, 150-byte payload. |

### DeliveryReporter

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Flush_WhenEmpty_ProducesNothing` | Flushing with no confirmations emits nothing. | The reporter has no phantom output. | — |
| `Flush_SingleReport_StartsWithDiscriminatorAndClears` | A flush prepends the non-user discriminator and clears pending confirmations. | The report is ready to send and is emitted once. | Message size 100 fits both confirmations in one packet. |
| `Flush_WhenConfirmationsExceedPacket_SplitsIntoBatches` | Confirmations are split by the packet capacity. | Five confirmations with a two-per-packet limit produce 2 + 2 + 1. | Packet limit is derived from `(19 - 4 - 4) / 5 = 2`; removing the safety-margin subtraction would give 3. |
| `Clear_DropsPendingConfirmations` | `Clear` discards accumulated confirmations. | Reset suppresses a pending report. | — |

### DeliverySorter

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `FirstPush_SetsExpectedId` | The first push establishes the expected id. | A fresh sorter accepts the first parcel and exposes it in order. | Constructed with `DeliveryId.Zero`; the first id overrides the initial expectation. |
| `SequentialPushPop_Works` | In-order pushes pop in order. | The happy path advances one id at a time. | — |
| `AheadIdsBuffered_PoppedInOrder` | Out-of-order arrivals are buffered and released in order. | The priority buffer restores order. | Pushes 1, 3, 2. |
| `TryPop_WhenNextMissing_ReturnsFalse` | A missing next id blocks the later buffered ids. | `TryPop` never skips a gap. | After popping 1, only 3 is buffered. |
| `PastId_Rejected` | Ids below the expected id are rejected. | Stale parcels do not re-enter the stream. | Push 5, pop, then push 3. |
| `AfterClear_PushReturnsFalse` | `Clear` permanently disables the sorter for pushes. | The sorter is not reusable after reset. | A replacement sorter is required. |
| `AfterClear_TryPopReturnsFalse` | `Clear` permanently disables the sorter for pops. | No buffered parcel survives a reset. | — |
| `Clear_ReleasesAllAndSetsError` | `Clear` invokes the destructor for every buffered parcel. | Reset releases caller-owned parcels exactly once. | Two buffered parcels. |
| `DuplicateIdAfterPop_SecondPopFiresOnErrorWithIds` | A buffered key below the advanced expectation raises `OnError` with both ids. | The sorter reports the expected id and the stale id. | Duplicate id 5 is accepted twice; the second pop observes 5 below expectation 6. |
| `FirstMessageLowerThanStartId_Accepted` | The first message wins over the configured start id. | Start id is only an initial expectation for an empty sorter. | Start id 100, first push 50. |
| `TryPop_WhenEmpty_ReturnsFalse` | Popping an empty sorter is harmless. | The sorter reports absence rather than throwing. | — |
| `GapFill_ThenPopsInOrder` | Filling a gap unblocks the buffered sequence. | The sorter resumes after the missing id arrives. | Push 1, pop; buffer 3 and 4; push 2; pop 2, 3, 4. |
| `SameIdAfterPop_AcceptedForNextSequence` | An id just consumed is stale; the next id is accepted. | Advance-by-one semantics are exact. | After popping 5, push 5 is rejected and push 6 is accepted. |

### DeliverySortingManager

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `AheadIdsBuffered_ReceivedInOrder` | The wrapper delivers messages in delivery-id order. | The wrapper buffers an ahead id and forwards it in order once the gap fills. | Pushes equivalent to ids 1, 3, 2; the subscriber releases each parcel. |
| `SingleMessage_PassesThroughInOrder` | A single message passes through unchanged. | Ordering adds no corruption for the trivial case. | — |
| `Clear_StopsFurtherProcessing` | `Clear` permanently stops the wrapper. | A cleared sorting manager drops subsequent inbound messages. | The underlying sorter is put into its error state. |
| `DuplicateMessage_NotSentToSorter` | Duplicates are filtered before ordering. | The inner delivery manager deduplicates and the wrapper fires once. | The same wire message is processed twice. |
| `BusyOrder_MultipleBatches_ReceivedInOrder` | A burst is delivered in strict id order. | The wrapper restores order across multiple buffered messages. | Ten messages, pushed 1–10. |
| `GapInSequence_FiresFailedToSort` | Receiving a later id before an earlier one raises `FailedToSort`. | The wrapper reports an unrecoverable ordering gap. | Packet 2 is delivered before packet 1. |
| `AfterGap_SorterStillDeliversLaterMessages` | A rejected push raises `FailedToSort` but does not kill the wrapper. | Later ids are still accepted and delivered. | A higher id is sent after the gap. |
| `AfterClear_FailedToSortFiresOnNextMessage` | After `Clear`, the next message raises `FailedToSort`. | Reset is observable through the event. | Two messages are delivered before `Clear` so the inbound dedup window is advanced. |

### MessageMerger

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Combine_FirstChunk_ReturnsNull` | An incomplete message yields no buffer. | Partial state is not exposed. | — |
| `Combine_AllChunksInOrder_ReturnsCombined` | The final chunk returns the concatenated payload. | Reassembly preserves chunk order and content. | Two chunks, 2 + 3 bytes. |
| `Combine_OutOfOrder_ReturnsCombined` | Chunks may arrive in any order. | The merger indexes by chunk id. | Chunk 1 then chunk 0. |
| `Combine_DuplicateChunk_Ignored` | A repeated chunk does not replace the first copy. | Duplicate chunks are harmless. | The duplicate carries different bytes, proving the first copy wins. |
| `Combine_InvalidPartId_Ignored` | A chunk id outside `partsNumber` is ignored. | Out-of-range chunks cannot corrupt reassembly. | Part id 5 with two parts. |
| `Clear_WithPending_PreventsLaterCombine` | `Clear` discards partial reassembly. | State does not leak across a reset. | After `Clear`, a non-initial chunk cannot complete the message. |
| `Combine_PartsNumber1_TreatedAsMulti` | The merger handles a one-part message on the multi-chunk path. | The merger contract is independent of the packer's fast path. | `partsNumber` 1, chunk 0 returns a one-byte buffer. |
| `Combine_DifferentIds_TrackedSeparately` | Interleaved messages do not mix. | Reassembly state is keyed by delivery id. | Two ids interleaved chunk 0, chunk 1. |

### MessagePacker

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `DeliveryMaxByteSize_ComputedCorrectly` | The maximum is `multiChunkMax * 255`. | The public limit matches the documented arithmetic. | Multi-chunk maximum 86 bytes yields 21,930. |
| `Pack_SmallData_ProducesOneQueuedMessage` | Small data takes the single-chunk fast path. | A valid send produces one queued message. | — |
| `Pack_SingleChunk_HasCorrectDeliveryId` | The queued message carries the requested id and chunk 0. | Delivery metadata is assigned correctly for the fast path. | — |
| `Pack_SingleChunk_EmptyData_Works` | Empty data takes the fast path and succeeds. | Zero-length payloads are valid. | — |
| `Pack_LargeData_ProducesMultipleQueuedMessages` | Data above the single-chunk limit is split. | The packer emits multiple chunks. | 200 bytes. |
| `Pack_MultiChunk_AllHaveSameDeliveryId` | Every chunk belongs to the same logical delivery. | Chunk metadata preserves the delivery id. | — |
| `Pack_MultiChunk_ChunkIdsAreSequential` | Chunk ids start at 0 and increase by one. | Reassembly can index chunks directly. | — |
| `Pack_MultiChunk_PayloadSizesSumToOriginal` | Summed chunk payloads equal the serialized user size. | Splitting loses and duplicates no payload bytes. | Serialized size includes the list count, types, varint length, and raw bytes. |
| `Pack_DataAtSingleChunkLimit_ProducesOneChunk` | Exactly the single-chunk maximum stays on the fast path. | The boundary is inclusive. | 86 bytes serialize to 90 bytes. |
| `Pack_DataJustOverSingleChunkLimit_ProducesMultipleChunks` | One byte over the boundary switches to multi-chunk. | The boundary is not off by one. | 87 bytes serialize to 91 bytes. |
| `Pack_DataTooBig_ReturnsMessageTooBig` | Data over the maximum is rejected without output. | The size guard runs before splitting. | 30,000 bytes; the consumer list stays empty. |
| `TryPeekDeliveryInfo_SingleChunk_ReturnsIdAndChunkZero` | A single-chunk wire message exposes its id with chunk 0. | Duplicate packets can be acknowledged without unpacking. | — |
| `TryPeekDeliveryInfo_MultiChunk_ReturnsIdAndChunkId` | A multi-chunk wire message exposes both its id and chunk id. | The peek path reads the same prefix as full unpacking. | — |
| `TryPeekDeliveryInfo_Empty_ReturnsFalse` | An empty list has no delivery info. | Malformed input is rejected. | — |
| `TryPeekDeliveryInfo_SingleMissingId_ReturnsFalse` | A single-chunk prefix requires an id. | Truncated input is rejected. | — |
| `TryPeekDeliveryInfo_MultiTruncated_ReturnsFalse` | A multi-chunk prefix requires a part id and an id. | Truncated multi-chunk input is rejected. | — |
| `Clear_DoesNotThrow` | `Clear` is safe on a packer with no partial inbound state. | Reset does not destabilise the packer. | The packer has just produced outbound chunks. |
| `Pack_ReleasesDataOnSuccess` | A successful pack consumes the caller's data list. | The caller must not release the input after a successful pack. | Observed through the tracked pool: the input list's reference count reaches zero. |

### MessageSplitter

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `GetChunkCount_ExactFit_ReturnsOne` | A payload that exactly fits is one chunk. | The boundary is inclusive. | Size 5, maximum 5. |
| `GetChunkCount_LargerThanMax_ReturnsMultiple` | The count is the ceiling of size over maximum. | The chunk count is sufficient for the payload. | Size 7, maximum 3, count 3. |
| `GetChunkCount_Empty_ReturnsZero` | Empty data needs no chunks. | Empty payloads do not produce phantom chunks. | — |
| `GetNextChunk_IteratesAllChunks_InOrder` | Chunk ids 0..n-1 cover the input exactly. | Iteration terminates and reassembles the original bytes. | 7 bytes split 3/3/1. |
| `GetNextChunk_PastEnd_ReturnsFalse` | An id at or past the count yields no chunk. | The iteration bound is exact. | Ids 1 and 5 for a one-chunk payload. |
| `GetNextChunk_EmptyData_ReturnsFalse` | An empty payload yields no chunk. | The splitter handles zero-length input. | — |

### RetryDeliveryScheduler

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `FirstAttempt_ReturnsDeltaBaseInterval` | The first retry delay is the base interval. | The default 100 ms interval is exposed. | Attempt 1. |
| `MultipleAttempts_DeltaGrowsLinearly` | The delay scales linearly with the attempt count. | Attempt 3 produces 300 ms. | Default base 100 ms. |
| `TimeoutExceeded_ReturnsFalse` | Retry stops after the timeout. | The scheduler enforces the overall deadline. | Timeout 3 s, elapsed 5 s. |
| `ExactTimeoutBoundary_StillRetries` | Reaching the timeout exactly still retries. | The timeout comparison is `>` rather than `>=`. | Elapsed exactly 3 s. |
| `JustBeforeTimeout_ReturnsTrue` | A task just inside the timeout retries. | The boundary is not premature. | Elapsed 2,999 ms. |
| `CustomBaseInterval_UsedForDelta` | A custom base interval is honoured. | Attempt 2 with base 500 ms yields 1,000 ms. | — |
| `ZeroTimeout_FailsImmediately` | A zero timeout gives up on the first reschedule. | The scheduler can be configured to disable retries. | Elapsed 1 ms. |

## Planned

No known missing tests in this layer. New gaps must be added here with an owner and a reason.

## Deferred by design

| Gap | Reason |
|---|---|
| Thread safety and concurrent use of `DeliveryManager`. | The contract says callers must provide external synchronization; concurrency belongs to a separate categorised layer or suite. |
| Real time, sleeping, timers, and wall clock. | Every scheduling entry point accepts an explicit `DateTime`; the default suite stays deterministic. |
| Real transports, sockets, and networks. | Covered by the transport and endpoint suites; this suite tests the delivery subsystem in isolation. |
| Wire compatibility with an independent peer implementation. | Interoperability is an integration concern, not a unit contract of these types. |
| Production capacity values (5,000 pending deliveries, 1,024 dedup window). | Tests use small capacities to stay fast and deterministic; the arithmetic and boundary behaviour are covered at small scale. |
| Shared primitives `DeliveryId`, `UnionDataList`, and zig-zag varint encoding. | They have loose tests at the project root and are not part of this suite's selected subject group. |
| Dispatcher buffer overflow at the production capacity. | Reaching 5,000 pending deliveries adds no contract meaning beyond the small-capacity overflow test. |
| `DeliveryInfoSerializer.CreateDeliveryReport` with a count larger than the supplied list. | The method is a trusted internal API; an out-of-range count is a programming error, not a recoverable input. |
| `MessageSplitter` negative chunk ids or non-positive maximum chunk size. | Internal callers pass validated positive sizes and non-negative ids; no graceful behaviour is defined for those inputs. |

## Notes

- Every pool-using test creates its own `MemoryRental`, so no byte-array or collectable-pool
  state is shared between tests. `TrackedFixture` additionally proves that each test returns
  every resource it rents.
- Scheduling is always driven with explicit `DateTime` values. No test sleeps or waits on a
  timer, and no test depends on the wall clock for a pass/fail decision.
- `DeliveryDispatcher` uses a binary priority queue that is not stable for equal keys.
  Order-sensitive scenarios either stagger schedule times or assert only counts and content.
- `DeliveryInfoSerializer.CurrentDeliveryReport` is reused between loads; tests read it before
  loading another report.
- `ProcessIncoming` takes ownership of the incoming list and releases it. Tests add an extra
  reference before handoff when they still need the message after the call.
- `DeliveryManager.Received` transfers the delivered user buffer to the subscriber. The
  subscriber must release it; the manager releases it only when no handler is attached.
  `DeliverySortingManager` follows the same rule for its own `Received` event.
- `TrackedFixture` releases every captured outgoing message, runs registered cleanup actions
  (`Clear` on managers, dispatchers, packers, mergers, and sorters), and then asserts that the
  tracked pools have no live resources and no release errors. This enforces the buffer-ownership
  and release guarantees for the default suite run.
- The suite tests `internal` production types through `InternalsVisibleTo("Pontifex.Tests")`.
  This is an assembly-level seam, not reflection or private-state inspection.
- `RetryDeliverySchedulerTests` uses a hand-written `StubDeliveryTask`; no mocking library is
  used anywhere in the suite.
- Test message sizes are small (`messageMaxByteSize` 100, single-chunk maximum 90, multi-chunk
  chunk maximum 86) so the documented size arithmetic is exercised quickly and exactly.
