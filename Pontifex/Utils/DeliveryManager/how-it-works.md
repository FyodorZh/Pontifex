# DeliveryManager — How It Works

## Overview

DeliveryManager is a **reliable message delivery subsystem** that sits between a transport layer and business logic. It provides:

- **In-order or unordered delivery** (via `DeliverySorter` wrapper)
- **Message chunking/reassembly** for large payloads
- **Automatic retry with configurable backoff** (via `DeliveryDispatcher` + `IDeliveryAttemptScheduler`)
- **Deduplication** of retransmitted packets (via `Deduplicator`)
- **ACK piggybacking** — delivery confirmations are batched and sent back alongside user data

All types live in namespace `Pontifex.DeliveryManager` and are `internal` (not part of the public API).

---

## Architecture

```
                     ┌─────────────────────────────────────┐
                     │         DeliverySortingManager        │  (optional ordering wrapper)
                     │  (adds DeliverySorter on top)         │
                     └──────────┬──────────────────────────┘
                                │
                     ┌──────────▼──────────────────────────┐
                     │           DeliveryManager            │  (orchestrator)
                     │                                     │
                     │  ┌────────────┐  ┌────────────────┐ │
                     │  │ Deduplicator│  │ DeliveryDispatcher│ │
                     │  │ (sliding   │  │ (priority queue │ │
                     │  │  window)   │  │  + retry logic) │ │
                     │  └────────────┘  └────────────────┘ │
                     │  ┌──────────────────────────────┐   │
                     │  │        MessagePacker          │   │
                     │  │  ┌────────────────────────┐   │   │
                     │  │  │   UserMessageHandler    │   │   │
                     │  │  │  (chunk + reassemble +  │   │   │
                     │  │  │   serialize + parse)    │   │   │
                     │  │  └────────────────────────┘   │   │
                     │  └──────────────────────────────┘   │
                     │  ┌──────────────────────────────┐   │
                     │  │         AckBuffer             │   │
                     │  │  (accumulate + batch ACKs)    │   │
                     │  └──────────────────────────────┘   │
                     └─────────────────────────────────────┘
```

---

## Wire Format

Every message sent over the wire is a single `IMultiRefByteArray` with the following structure:

### UserSingle (type = 0x00)
For messages that fit in one chunk.
```
Offset  Size  Field
0       1     type (0x00)
1       2     DeliveryId (ushort LE)
3       N     user data (raw bytes)
```

### UserMulti (type = 0x01)
For messages split across multiple chunks. Each chunk carries the same `DeliveryId` plus chunk metadata.
```
Offset  Size  Field
0       1     type (0x01)
1       2     DeliveryId (ushort LE)
3       1     partId (byte) — which chunk this is (0-based)
4       1     partsNumber (byte) — total number of chunks
5       N     chunk data (raw bytes)
```

### DeliveryInfo (type = 0x02)
Batched delivery confirmations (ACKs).
```
Offset  Size  Field
0       1     type (0x02)
1       2     count (ushort LE) — number of confirmations in this batch
3       for each confirmation:
        2     DeliveryId (ushort LE)
        1     chunkId (byte)
```

### Size constraints

| Constant | Value | Description |
|---|---|---|
| `UserSingleOverhead` | 6 | Header bytes for UserSingle |
| `UserMultiOverhead` | 10 | Header bytes for UserMulti |
| `DeliveryInfoFixedOverhead` | 6 | Header bytes for DeliveryInfo |
| `DeliveryInfoElementSize` | 5 | Each confirmation entry |
| `SafetyMargin` | 4 | Padding to avoid MTU edge cases |
| `DeduplicatorCapacity` | 1024 | Sliding window size |
| `TransportMessageQueueCapacity` | 5000 | Max pending deliveries |

Derived limits:
- `SingleChunkDeliveryMaxSize = messageMaxByteSize - UserSingleOverhead(6) - SafetyMargin(4)`
- `MultiChunkDeliveryChunkMaxSize = messageMaxByteSize - UserMultiOverhead(10) - SafetyMargin(4)`
- `DeliveryMaxByteSize = MultiChunkDeliveryChunkMaxSize × 255` (max 255 chunks)

---

## Sending Flow

```
ScheduleDelivery(id, data)
  │
  ├─ data.size ≤ SingleChunkMax
  │   └─ serialize as UserSingle → _queueToSend
  │
  ├─ data.size ≤ DeliveryMaxByteSize
  │   └─ split into chunks of MultiChunkMax
  │       └─ serialize each as UserMulti → _queueToSend
  │
  └─ else → return MessageTooBig

ProcessOutgoing(scheduler, now, dst)
  │
  ├─ for each item in _queueToSend:
  │     ├─ ParseDeliveryInfo  → extract DeliveryInfo from header
  │     ├─ ScheduleDeliver(info, data, now)
  │     │   └─ DeliveryDispatcher: add to priority queue with key=now
  │     └─ (BufferOverflow → fire FailedToDeliver)
  │
  ├─ if _confirmationList not empty:
  │     └─ batch into DeliveryInfo message(s) → dst
  │
  └─ DeliveryDispatcher.TryToDeliver(dst, scheduler, now)
      └─ for each task due (TopKey ≤ now):
          ├─ if task already confirmed → skip
          ├─ dst.Put(acquired data)   — send to wire
          ├─ increment DeliveryAttempts
          ├─ scheduler.Reschedule(task, now, out delta)
          │   ├─ true  → re-queue with key = sendTime + delta
          │   └─ false → fire FailedToDeliver, release task
          └─ loop
```

### Key invariants (sending)

1. **DeliveryId uniqueness across chunks**: A multi-chunk message shares the same `DeliveryId` across all chunks. The `DeliveryDispatcher` tracks `DeliveryInfo(deliveryId, chunkId)` per chunk. ACKs confirm individual chunks. The logical message (all chunks of same `DeliveryId`) is considered delivered only when ALL its chunks are acknowledged.

2. **`_unfinishedLogicDeliveries` dictionary** counts outstanding chunks per `DeliveryId`. When a chunk is confirmed, the count decrements. When it reaches zero, `Delivered` event fires. This ensures the caller doesn't get a "delivered" notification until all chunks of a large message are confirmed.

3. **Retry is per-chunk**: Each chunk of a multi-chunk message retries independently. The scheduler decides per-chunk.

4. **ScheduleDelivery releases the input `IMultiRefByteArray`** (via `finally { data.Release() }`). The serialized copy retains the data. The caller must not use the data after `ScheduleDelivery` returns.

---

## Receiving Flow

```
ProcessIncoming(incomingData)
  │
  ├─ type == DeliveryInfo (0x02)
  │   └─ for each confirmation: dispatcher.ConfirmDelivered(info)
  │       └─ decrements _unfinishedLogicDeliveries count
  │           └─ if count reaches 0 → fire Delivered event
  │
  ├─ type == UserSingle (0x00) or UserMulti (0x01)
  │   ├─ TryParseDeliveryId → extract DeliveryId from header
  │   ├─ Deduplicator.Received(id) → New / Duplicate / Overflow
  │   │   ├─ Overflow → return false (fatal)
  │   │   └─ Duplicate → still add to confirmation list (ACK it!), but skip processing
  │   ├─ add DeliveryInfo to _confirmationList (for later ACK)
  │   │
  │   └─ if New:
  │       ├─ UserSingle → Recipient.ReceivedSingle(data)
  │       │   └─ AddRef → return reference
  │       ├─ UserMulti  → Recipient.ReceivedMulti(id, partId, partsNumber, chunk)
  │       │   └─ store chunk; when all parts done → Combine() into one buffer
  │       │
  │       └─ if userData != null → fire Received event
  │
  └─ unknown type → return false
```

### Key invariants (receiving)

1. **ACK always sent**: Even for duplicate messages, the `DeliveryInfo` is added to `_confirmationList`. This ensures the sender gets the ACK and stops retransmitting. Without this, the sender would keep retrying forever.

2. **Deduplicator uses `DeliveryId` as packet ID**: This works because each logically distinct send gets a unique `DeliveryId` from the sender. Retransmissions carry the same `DeliveryId`.

3. **Deduplicator overflow** returns `false` (fatal). The caller (protocol layer) should close the connection. The window size of 1024 must be sufficient for the expected packet window.

4. **Multi-chunk messages accept chunks in any order**. `UnorderedDeliveryRecipient` stores chunks by `partId` and combines only when all are ready. Partial chunks are released on `Clear()`.

5. **DeliveryInfo messages are NOT deduplicated**. They bypass the `Deduplicator` entirely (no `DeliveryId` is parsed from them). If a duplicate `DeliveryInfo` arrives, `ConfirmDelivered` is a no-op for already-confirmed deliveries (the entry was already removed from `_unfinishedDeliveries`).

---

## Component Details

### Deduplicator

Sliding-window deduplicator backed by `CycleQueue<bool>`.

**State:**
- `_queue[bool]` — tracks which IDs in the window have been seen
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

**Capacity**: 1024. If the sender transmits more than 1024 unacknowledged messages, overflow occurs (fatal).

### DeliveryDispatcher

Manages the retry queue and delivery tracking.

**Structures:**
- `_deliveryQueue: PriorityQueue<DateTime, DeliveryTask>` — sorted by next send time
- `_unfinishedDeliveries: HashSet<DeliveryInfo>` — all chunks not yet acknowledged
- `_unfinishedLogicDeliveries: Dictionary<DeliveryId, int>` — per-message chunk counter

**DeliveryTask lifecycle:**
1. Created by `ScheduleDeliver`, enqueued with `key = now`
2. `TryToDeliver` dequeues when `key ≤ now`, sends via `dst.Put()`, then either:
   - Reschedules with new key = `sendTime + delta` (if scheduler says retry)
   - Removes and fires `FailedToDeliver` (if scheduler says give up)
3. `ConfirmDelivered` removes from `_unfinishedDeliveries` and decrements counter; if counter reaches 0, fires `Delivered` event

**Capacity**: 5000 simultaneous pending deliveries. `ScheduleResult.BufferOverflow` if exceeded.

### DeliverySorter<TParcel>

Ensures messages are delivered to the upper layer in `DeliveryId` order.

**State:**
- `_id: DeliveryId` — the next expected ID
- `_parcels: PriorityQueue<DeliveryId, TParcel>` — out-of-order buffer

**Algorithm:**
- `Push(id, parcel)`: Accepts if `id ≥ _id` (enqueues to priority queue). Rejects (returns false) if `id < _id` (too old).
- `TryPop()`: If `_parcels.TopKey == _id`, dequeue and advance `_id = _id.Next`. If `TopKey < _id`, signal error — a message was skipped (gap detected).

**Error state**: Once `_hasError` is set, all subsequent `Push()` and `TryPop()` fail. The sorter must be replaced.

### UserMessageHandler

Handles both send-direction and receive-direction user message processing.

**Send (chunking + serialization):**
- `GetChunkCount(dataSize)` — number of chunks needed
- `GetNextChunk(data, chunkId, out chunk)` — extract one chunk
- `CreateUserSingle(id, data)` — build single-chunk wire message
- `CreateUserMulti(id, data, partId, partsNumber)` — build multi-chunk wire message

**Receive (reassembly + deserialization):**
- `Combine(id, partId, partsNumber, chunkData)` — stores chunk; returns combined buffer when all chunks ready (same semantics as legacy `UnorderedDeliveryRecipient`)
- `TryParseUserMessage(data, out parsed)` — parse wire message into fields
- `Deserialize(data)` — deserialize bytes back to `UnionDataList`

All reassembly state is cleared via `Clear()`.

### AckBuffer

Accumulates delivery confirmations (ACKs) received from the remote side and flushes them as batched `DeliveryInfo` wire messages.

- `Add(info)` — record a confirmation (called from `ProcessIncoming`)
- `Flush(messageMaxByteSize, safetyMargin, dst)` — batch and send pending ACKs (called from `ProcessOutgoing`)
- `TryParseDeliveryInfo(data, confirmations)` — parse incoming `DeliveryInfo` wire message into confirmations
- `Clear()` — discard all pending ACKs

---

## Wire Size Calculations

```
MaxUserDataInSingleChunk = messageMaxByteSize - UserSingleOverhead(6) - SafetyMargin(4)
MaxChunkDataInMultiChunk = messageMaxByteSize - UserMultiOverhead(10) - SafetyMargin(4)
MaxUserDataTotal          = MaxChunkDataInMultiChunk × 255
MaxConfirmationsPerPacket = (messageMaxByteSize - DeliveryInfoFixedOverhead(6) - SafetyMargin(4)) / DeliveryInfoElementSize(5)
```

The `-4` safety margin exists in the legacy code "just to be sure" (avoids edge cases with MTU).

---

## Thread Safety

All `DeliveryManager` methods are **NOT thread-safe**:
- `ScheduleDelivery` can be called from any thread (the queue is `SystemQueue<T>` — thread-safe only in SPSC scenarios)
- `ProcessIncoming` and `ProcessOutgoing` must be called from the same thread
- The caller must provide external synchronization

The legacy code achieved thread safety by running everything on a single logic thread (`IPeriodicLogic.LogicTick()`), with cross-thread handoff via `ConcurrentQueueValve` at the protocol layer above.

---

## Protocol Handshake (Legacy Context)

The delivery system does NOT handle the initial ACK handshake. In the legacy code, the protocol layer above handled:

1. **Client sends**: `AckPrefix + userAckData`
2. **Server validates**: checks `AckPrefix`, calls handler producer
3. **Server responds**: `AckOKResponse + userAckResponse`
4. **Client validates**: checks `AckOKResponse`, calls `OnConnected`

After the handshake, the delivery system takes over for all subsequent data transfer. Keepalive is also handled at the protocol layer (sending empty messages with `isKeepAlive = true` every 1 second).

---

## Event Lifecycle

```
ScheduleDelivery(id, data)
  │
  ├─[later] ProcessOutgoing → sent to wire
  │
  ├─[remote receives, sends back DeliveryInfo]
  │
  ├─ ProcessIncoming(DeliveryInfo)
  │   └─ Dispatcher.ConfirmDelivered(info)
  │       └─ if last chunk of DeliveryId → Delivered(id)
  │
  └─[retries exhausted]
      └─ Dispatcher: FailedToDeliver(id)

On the receiving side:

ProcessIncoming(UserSingle/UserMulti)
  ├─[if first time] → Received(id, data, processTime)
  └─[always] → _confirmationList.Add(…)  →  [later] ProcessOutgoing sends DeliveryInfo
```

### Invariant: Delivered fires exactly once per unique DeliveryId

The `_unfinishedLogicDeliveries` counter ensures this: if a message has N chunks, `Delivered` fires only when the Nth `ConfirmDelivered` call decrements the counter to zero. The `_unfinishedDeliveries` hash set prevents double-ACK of the same chunk.
