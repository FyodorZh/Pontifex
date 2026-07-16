# DeliveryManager — How It Works

## Overview

DeliveryManager is a **reliable message delivery subsystem** that sits between a transport layer and business logic. It provides:

- **In-order or unordered delivery** (via `DeliverySortingManager` wrapper)
- **Message chunking/reassembly** for large payloads
- **Automatic retry with configurable backoff** (via `DeliveryDispatcher` + `IDeliveryAttemptScheduler`)
- **Deduplication** of retransmitted wire packets (via `Deduplicator`)
- **ACK piggybacking** — delivery confirmations are batched and sent back (via `DeliveryReporter`)

All types live in namespace `Pontifex.DeliveryManager` and are `internal` (not part of the public API).

---

## Architecture

```
                    ┌─────────────────────────────────────┐
                    │       DeliverySortingManager         │  (optional ordering wrapper)
                    │  (wraps DeliveryManager.Received     │
                    │   via DeliverySorter<UnionDataList>) │
                    └──────────┬──────────────────────────┘
                               │
                    ┌──────────▼──────────────────────────┐
                    │           DeliveryManager            │  (orchestrator)
                    │                                     │
                    │  ┌──────────────┐  ┌──────────────┐ │
                    │  │ Deduplicator │  │DeliveryDispatcher│
                    │  │ (sliding     │  │ (priority q   │ │
                    │  │  window on   │  │  + retry)     │ │
                    │  │  wireChunkId)│  └──────────────┘ │
                    │  └──────────────┘  ┌──────────────┐ │
                    │                    │MessagePacker  │ │
                    │  ┌──────────────┐  │(split + merge)│ │
                    │  │DeliveryReporter│ └──────────────┘ │
                    │  │ (ACK batching)│                   │
                    │  └──────────────┘                   │
                    └─────────────────────────────────────┘
```

---

## Wire Format

Every outbound wire packet is a `UnionDataList`. The first two elements are prepended by `DeliveryDispatcher`:

| Index | UnionData type | Field |
|---|---|---|
| 0 | `bool` | `true` = user message, `false` = delivery report (ACK) |
| 1 | `ushort` | wireChunkId — used by the receiver's `Deduplicator` |

The remaining elements depend on the message type (user data vs delivery report).

### User Single-Chunk Message

Created by `MessagePacker.Pack()` for payloads that fit in one chunk.

| Index | UnionData type | Field |
|---|---|---|
| 0 | `bool` | `true` (isUser) |
| 1 | `ushort` | wireChunkId |
| 2 | `byte` | partsNumber = `1` (single-chunk marker) |
| 3 | `ushort` | DeliveryId |
| 4 | `Array` | serialized user data |

### User Multi-Chunk Message

Created by `MessagePacker.Pack()` for payloads split across chunks.

| Index | UnionData type | Field |
|---|---|---|
| 0 | `bool` | `true` (isUser) |
| 1 | `ushort` | wireChunkId |
| 2 | `byte` | chunksNumber — total number of chunks |
| 3 | `byte` | chunkId — this chunk's index (0-based) |
| 4 | `ushort` | DeliveryId |
| 5 | `Array` | chunk payload (bytes) |

### Delivery Report (ACK)

Created by `DeliveryReporter.Flush()` via `DeliveryInfoSerializer.CreateDeliveryReport()`.

| Index | UnionData type | Field |
|---|---|---|
| 0 | `bool` | `false` (isUser = delivery report) |
| 1 | `ushort` | count — number of confirmations in this batch |
| 2..N | pairs: `ushort` DeliveryId, `byte` chunkId | repeated `count` times |

### Size constraints

| Constant | Value | Description |
|---|---|---|
| `UserSingleOverhead` | 6 | Byte overhead for single-chunk header (excl. payload) |
| `UserMultiOverhead` | 10 | Byte overhead for multi-chunk header (excl. payload) |
| `DeliveryInfoFixedOverhead` | 4 | Byte overhead for delivery report header (excl. entries) |
| `DeliveryInfoElementSize` | 5 | Byte overhead per confirmation entry |
| `SafetyMargin` | 4 | Padding to avoid MTU edge cases |
| `DeduplicatorCapacity` | 1024 | Sliding window size for wireChunkId dedup |
| `TransportMessageQueueCapacity` | 5000 | Max pending deliveries in the dispatcher |

Derived limits:
- `SingleChunkDeliveryMaxSize = messageMaxByteSize - UserSingleOverhead(6) - SafetyMargin(4)`
- `MultiChunkDeliveryChunkMaxSize = messageMaxByteSize - UserMultiOverhead(10) - SafetyMargin(4)`
- `DeliveryMaxByteSize = MultiChunkDeliveryChunkMaxSize × 255` (max 255 chunks)

---

## Sending Flow

```
ScheduleDelivery(id, data)
  │
  ├─ data is null → return InvalidMessage
  │
  ├─ data.size ≤ singleChunkMax
  │   └─ Pack as single-chunk → _queueToSend (QueuedMessage)
  │
  ├─ data.size ≤ DeliveryMaxByteSize
  │   └─ Serialize data → split into chunks of multiChunkMax
  │       └─ Pack each as multi-chunk → _queueToSend
  │
  └─ else → return MessageTooBig

ProcessOutgoing(scheduler, now, dst)
  │
  ├─ for each QueuedMessage in _queueToSend:
  │     ├─ ScheduleDeliver(info, userMessage, now)
  │     │   └─ DeliveryDispatcher: add to priority queue with key=now
  │     │       ├─ Ok → enqueue
  │     │       ├─ BufferOverflow → fire FailedToDeliver
  │     │       └─ IdIsNotUnique → release data (should not happen for unique DeliveryInfo)
  │     └─ release userMessage ref
  │
  ├─ DeliveryReporter.Flush():
  │     └─ batch pending confirmations into DeliveryReport message(s) → dst
  │
  └─ DeliveryDispatcher.TryToDeliver(dst, scheduler, now)
      └─ for each task due (TopKey ≤ now):
          ├─ if task already confirmed → skip (release task)
          ├─ dst.Put(AcquireMessage) — clone with isUser+wireChunkId → dst
          ├─ increment DeliveryAttempts
          ├─ scheduler.Reschedule(task, now, out delta)
          │   ├─ true  → re-enqueue with key = sendTime + delta
          │   └─ false → fire FailedToDeliver, release task
          └─ loop
```

### Key invariants (sending)

1. **DeliveryId uniqueness across chunks**: A multi-chunk message shares the same `DeliveryId` across all chunks. The `DeliveryDispatcher` tracks `DeliveryInfo(deliveryId, chunkId)` per chunk. ACKs confirm individual chunks. The logical message is considered delivered only when ALL its chunks are acknowledged.

2. **`_unfinishedLogicDeliveries` dictionary** counts outstanding chunks per `DeliveryId`. When a chunk is confirmed, the count decrements. When it reaches zero, `Delivered` fires. This ensures the caller doesn't get a "delivered" notification until all chunks are confirmed.

3. **Retry is per-chunk**: Each chunk retries independently under the dispatcher. The scheduler decides per-chunk.

4. **ScheduleDelivery uses `using var disposer = data.AsDisposable()`** — the input `UnionDataList` is released on return. The caller must not use it afterwards.

5. **Retransmitted packets have the same wireChunkId** — the dispatcher's `AcquireMessage()` clones the stored data, which already has the original wireChunkId prepended. The receiver's deduplicator therefore sees a duplicate.

---

## Receiving Flow

```
ProcessIncoming(data)
  │
  ├─ TryPopFirst(isUser)
  │   └─ false (not a UnionData.Bool) → return false
  │
  ├─ isUser == false → Delivery Report (ACK)
  │   ├─ DeliveryInfoSerializer.LoadDeliveryReport(data)
  │   │   └─ fails if count is not ushort → return false
  │   ├─ for each (DeliveryId, chunkId):
  │   │     └─ dispatcher.ConfirmDelivered(info)
  │   │         ├─ removes from _unfinishedDeliveries
  │   │         └─ decrements _unfinishedLogicDeliveries count
  │   │             └─ if count reaches 0 → fire Delivered event
  │   └─ return true
  │
  ├─ isUser == true → User Message
  │   ├─ TryPopFirst(wireChunkId)
  │   │   └─ not ushort → return false
  │   │
  │   ├─ Deduplicator.Received(wireChunkId)
  │   │   ├─ Overflow → return false (fatal, close connection)
  │   │   ├─ New → process full message
  │   │   │   ├─ MessagePacker.TryUnpackUserMessage(data, out unpacked)
  │   │   │   │   └─ reads partsNumber byte:
  │   │   │   │       1 → ReadUserSingle (single-chunk)
  │   │   │   │       N → ReadUserMulti (multi-chunk, merge via MessageMerger)
  │   │   │   └─ fails → return false
  │   │   ├─ Duplicate → skip full processing
  │   │   │   └─ MessagePacker.TryPeekDeliveryInfo(data, out info)
  │   │   │       └─ fails → return false
  │   │   │
  │   │   ├─ DeliveryReporter.Add(info) — always (so ACK is sent)
  │   │   │
  │   │   └─ if unpacked.UserData != null (New path):
  │   │         └─ fire Received(id, userData), release userData
  │   │
  │   └─ return true
  │
  └─ default → return false
```

### Key invariants (receiving)

1. **ACK always sent**: Even for duplicate wire packets, the `DeliveryInfo` is added to the confirmation list via `DeliveryReporter.Add()`. This ensures the sender gets the ACK and stops retransmitting.

2. **Deduplicator operates on `wireChunkId`**, NOT on `DeliveryId`. The `wireChunkId` is a `ushort` assigned sequentially by the sender's `DeliveryDispatcher`. Retransmissions of the same chunk carry the same `wireChunkId`. The deduplicator is a sliding window over this ID space.

3. **Deduplicator overflow** returns `false` (fatal). The caller (protocol layer) should close the connection.

4. **Multi-chunk messages accept chunks in any order**. `MessageMerger` stores chunks by `partId` and combines only when all are ready. Partial chunks are released on `Clear()`.

5. **Delivery Report (ACK) messages are NOT deduplicated**. They bypass the `Deduplicator` entirely. If a duplicate ACK arrives, `ConfirmDelivered` is a no-op (the entry was already removed from `_unfinishedDeliveries`).

---

## Component Details

### Deduplicator

Sliding-window deduplicator backed by `CycleQueue<bool>` (fixed-capacity circular buffer).

**State:**
- `_queue[bool]` — tracks which wireChunkIds in the window have been seen
- `_from (uint)` — first ID in the window
- `_till (uint)` — last ID in the window

**Algorithm (`Received(id)`):**

```
if queue is empty OR id > _till:
    fill gap from _till+1 to id:
        put false for gaps, true for id itself
        if queue overflows → return Overflow
    _till = id
    Trim
    return New

if id < _from:
    return Duplicate (outside window, must be old)

pos = id - _from
if _queue[pos] is already true:
    return Duplicate

_queue[pos] = true
Trim
return New
```

**Trim()**: Pops leading `true` entries from the queue, shifting `_from` forward. This keeps the window tight — only IDs that might still be retransmitted are tracked.

**Capacity**: 1024. If the sender transmits more than 1024 unacknowledged wireChunkIds, overflow occurs (fatal).

**Note**: ID=0 is not handled gracefully (integer underflow in `Trim`). The system never generates wireChunkId=0 (`_nextSeq` starts at 1 and wraps from 65535 to 1), so this is unreachable in production.

### DeliveryDispatcher

Manages the retry queue and delivery tracking.

**Structures:**
- `_deliveryQueue: PriorityQueue<DateTime, DeliveryTask>` — min-heap sorted by next send time
- `_unfinishedDeliveries: HashSet<DeliveryInfo>` — all chunks not yet acknowledged
- `_unfinishedLogicDeliveries: Dictionary<DeliveryId, int>` — per-message chunk counter

**DeliveryTask lifecycle:**
1. Created by `ScheduleDeliver`, enqueued with `key = now`
2. `Init()` clones the user data, prepends `bool(true)` (isUser) and `ushort` (wireChunkId)
3. `TryToDeliver` dequeues when `key ≤ now`, sends via `dst.Put(AcquireMessage())`, then either:
   - Reschedules with new key = `sendTime + delta` (if scheduler says retry)
   - Removes and fires `FailedToDeliver` (if scheduler says give up)
4. `ConfirmDelivered` removes the chunk from `_unfinishedDeliveries` and decrements the per-message counter; if counter reaches 0, fires `Delivered`

**Capacity**: 5000 simultaneous pending deliveries. `ScheduleResult.BufferOverflow` if exceeded.

**PriorityQueue note**: The `PriorityQueue` is a binary min-heap and is NOT stable for equal keys. Tasks scheduled at the same `DateTime` may be dispatched in any order. Tests that depend on dispatch order should use staggered timestamps.

### MessagePacker

Handles serialization of user data into wire format and deserialization on the receiving side.

**Components:**
- `MessageSplitter` — splits a serialized byte buffer into chunks of at most `multiChunkMax`
- `MessageMerger` — reassembles chunks by `DeliveryId` + `partId`, returns combined buffer when complete

**Pack flow (send):**
1. Small data (≤ singleChunkMax) → single-chunk wire message, added to `_queueToSend`
2. Large data → serialize via `UnionDataList.Serialize()`, split into chunk-sized pieces, each wrapped as a multi-chunk wire message → `_queueToSend`
3. If `Serialize()` fails → `InvalidMessage`. If total size > `DeliveryMaxByteSize` → `MessageTooBig`.

**Unpack flow (receive):**
1. `TryUnpackUserMessage`: reads `partsNumber` byte
   - 1 → `ReadUserSingle`: returns the user data directly (with DeliveryId)
   - N → `ReadUserMulti`: merges chunks via `MessageMerger`, deserializes the combined buffer back into a `UnionDataList`
2. `TryPeekDeliveryInfo`: extracts just the `DeliveryInfo` (DeliveryId + chunkId) without full deserialization — used for duplicate packets

### DeliveryReporter

Accumulates received `DeliveryInfo` entries (one per received wire chunk, including duplicates) and flushes them as batched delivery report messages.

**Flush logic:**
1. Calculates how many confirmations fit in one packet: `(messageMaxByteSize - DeliveryInfoFixedOverhead - SafetyMargin) / DeliveryInfoElementSize`
2. Splits `_confirmations` into batches of that size
3. For each batch, calls `DeliveryInfoSerializer.CreateDeliveryReport()` and prepends `bool(false)` (isUser = delivery report)
4. Clears the internal list

### DeliveryInfoSerializer

Serializes/deserializes delivery confirmations to/from `UnionDataList`.

- `CreateDeliveryReport(confirmations, start, count)`: Creates a list with `ushort(count)` followed by pairs of `(ushort DeliveryId, byte chunkId)`.
- `LoadDeliveryReport(data)`: Parses the list and populates `CurrentDeliveryReport`. Returns `false` on malformed input (wrong type or truncated).

### DeliverySorter<TParcel>

Ensures messages are delivered to the upper layer in `DeliveryId` order.

**State:**
- `_id: DeliveryId` — the next expected ID
- `_parcels: PriorityQueue<DeliveryId, TParcel>` — out-of-order buffer
- `_hasError: bool` — set on Clear() or if TryPop detects an ID gap

**Algorithm:**
- `Push(id, parcel)`: First call sets `_id = id`. Subsequent calls accept if `id ≥ _id` (enqueues). Rejects if `id < _id`.
- `TryPop()`: If `_parcels.TopKey == _id`, dequeue and advance `_id = _id.Next`. If `TopKey < _id`, set `_hasError = true` and fire `OnError`.
- `Clear(destructor)`: Empties the queue (calling destructor per item), sets `_hasError = true`.

**Error state**: Once `_hasError` is set, all `Push()` and `TryPop()` calls fail permanently. The sorter must be replaced.

### DeliverySortingManager

Optional wrapper around `IDeliveryManagerUserSide` that reorders incoming messages by `DeliveryId`.

- Wraps the inner DM's `Received` event through a `DeliverySorter<UnionDataList>`
- Exposes its own `Received` (in-order) and `FailedToSort` events
- `Clear()` calls `_sorter.Clear()` (releases pending parcels, permanently stops the sorter)

---

## Wire Size Calculations

```
MaxUserDataInSingleChunk = messageMaxByteSize - UserSingleOverhead(6) - SafetyMargin(4)
MaxChunkDataInMultiChunk = messageMaxByteSize - UserMultiOverhead(10) - SafetyMargin(4)
MaxUserDataTotal          = MaxChunkDataInMultiChunk × 255
MaxConfirmationsPerPacket = (messageMaxByteSize - DeliveryInfoFixedOverhead(4) - SafetyMargin(4)) / DeliveryInfoElementSize(5)
```

The `-4` safety margin avoids edge cases with MTU.

---

## Thread Safety

All `DeliveryManager` methods are **NOT thread-safe**:
- `ScheduleDelivery` can be called from any thread (the queue is `SystemQueue<T>` — a simple FIFO, not thread-safe)
- `ProcessIncoming` and `ProcessOutgoing` must be called from the same thread
- The caller must provide external synchronization

The legacy code achieved thread safety by running everything on a single logic thread (`IPeriodicLogic.LogicTick()`), with cross-thread handoff via `ConcurrentQueueValve` at the protocol layer above.

---

## Event Lifecycle

```
ScheduleDelivery(id, data)
  │
  ├─[later] ProcessOutgoing → sent to wire
  │
  ├─[remote receives, sends back DeliveryReport]
  │
  ├─ ProcessIncoming(DeliveryReport)
  │   └─ Dispatcher.ConfirmDelivered(info)
  │       └─ if last chunk of DeliveryId → Delivered(id)
  │
  └─[retries exhausted]
      └─ Dispatcher: FailedToDeliver(id)

On the receiving side:

ProcessIncoming(UserMessage)
  ├─[if Deduplicator.Result.New]
  │   └─ Received(id, data)
  └─[always] → DeliveryReporter.Add(info)
      └─[later] ProcessOutgoing → send DeliveryReport
```

### Invariant: Delivered fires exactly once per unique DeliveryId

The `_unfinishedLogicDeliveries` counter ensures this: if a message has N chunks, `Delivered` fires only when the Nth `ConfirmDelivered` call decrements the counter to zero. The `_unfinishedDeliveries` hash set prevents double-ACK of the same chunk.
