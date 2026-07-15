# DeliveryManager — How to Use

## What It Is

DeliveryManager turns an unreliable, unordered transport into a reliable, optionally ordered delivery system. It handles:

- Splitting large messages into chunks and reassembling them
- Retransmitting lost packets with configurable backoff
- Detecting and discarding duplicates
- Acknowledging received data (ACK piggybacking)
- Optionally delivering messages in order

---

## Quick Start

```csharp
using Pontifex.DeliveryManager;
using Actuarius.Memory;

// 1. Create with a byte pool and max packet size
var pool = MemoryRental.Shared.ByteArraysPool;
var dm = new DeliveryManager(maxPacketSize: 1400, bytesPool: pool);

// 2. Subscribe to received messages
dm.Received += (id, data, processTime) =>
{
    Console.WriteLine($"Received {id}: {data.Count} bytes");
    data.Release(); // must release when done
};

// 3. Schedule a message for delivery
var data = pool.Acquire(100);
// ... fill data ...
dm.ScheduleDelivery(DeliveryId.Zero.Next, data);
// data is released internally — do not use after this call

// 4. Periodically pump outgoing data (e.g., every 10-50ms)
var scheduler = new RetryDeliveryScheduler(
    disconnectTimeout: TimeSpan.FromSeconds(30));
var collector = new CollectingConsumer();  // your IConsumer<IMultiRefByteArray> impl
dm.ProcessOutgoing(scheduler, DateTime.UtcNow, collector);
// collector now has data to send over the wire

// 5. Feed incoming data from the wire
var incoming = ReadFromWire();
dm.ProcessIncoming(incoming);
```

---

## Setup

### Constructor

```csharp
var dm = new DeliveryManager(
    messageMaxByteSize: 1400,    // MTU of your transport
    bytesPool: pool              // IPool<IMultiRefByteArray, int>
);
```

**`messageMaxByteSize`**: The maximum single-wire-packet size your transport supports. Typically the MTU minus protocol headers (e.g., 1400 for Ethernet + IP + UDP).

**`bytesPool`**: Acquired from `MemoryRental.Shared.ByteArraysPool`. This is the same pool used everywhere in Pontifex.

### Required Infrastructure

You need to implement:

1. **A periodic tick** — call `ProcessOutgoing(scheduler, now, consumer)` on a timer (10-50ms interval)
2. **A retry scheduler** — use `RetryDeliveryScheduler` or implement `IDeliveryAttemptScheduler`
3. **An IConsumer** — to collect data for the wire (or send it directly)

---

## Sending Data

### Single-chunk messages (fast path)

```csharp
var id = DeliveryId.Zero.Next;  // or any unique ID
var data = pool.Acquire(200);
// fill data[0..199]

var result = dm.ScheduleDelivery(id, data);
// data is released — do not use it after this call
```

Messages up to `maxPacketSize - 9` bytes go as a single wire packet.

### Multi-chunk messages (large payloads)

```csharp
var id = DeliveryId.Zero.Next;
var hugeData = pool.Acquire(50000);
// fill data[0..49999]

var result = dm.ScheduleDelivery(id, hugeData);
// Automatically split into ~36 chunks (at ~1400 MTU)
```

Messages up to `(maxPacketSize - 11) × 255` bytes are supported.

### When to assign DeliveryId

The caller is responsible for assigning unique `DeliveryId` values. A simple pattern:

```csharp
private DeliveryId _nextId = DeliveryId.Zero.Next;

DeliveryId NextId()
{
    var id = _nextId;
    _nextId = _nextId.Next;
    return id;
}
```

`DeliveryId` uses uint16 with wrap-around. `Next` skips 0 (reserved). The `CompareTo` method handles wrap-around correctly using half-range comparison.

---

## Receiving Data

```csharp
dm.Received += (id, data, processTime) =>
{
    Console.WriteLine($"Got message {id.Id}, {data.Count} bytes");
    // 'data' is yours — release when done
    data.Release();
};

// Feed raw wire data:
dm.ProcessIncoming(wireData);
```

The data you get back is a **new reference** — you must call `data.Release()` when you're done with it.

### Delivery confirmations (ACKs)

```csharp
dm.Delivered += id =>
{
    Console.WriteLine($"Message {id.Id} confirmed delivered");
};

dm.FailedToDeliver += id =>
{
    Console.WriteLine($"Message {id.Id} delivery failed (retries exhausted)");
};
```

---

## Retry Scheduler

### Built-in: `RetryDeliveryScheduler`

```csharp
var scheduler = new RetryDeliveryScheduler(
    disconnectTimeout: TimeSpan.FromSeconds(30),
    baseIntervalMs: 100
);
```

Behavior:
- 1st retry after 100ms
- 2nd retry after 200ms
- Nth retry after N × 100ms
- Gives up when total elapsed time from original send exceeds 30s

### Custom scheduler

Implement `IDeliveryAttemptScheduler`:

```csharp
class ExponentialBackoff : IDeliveryAttemptScheduler
{
    public bool Reschedule(IDeliveryTask task, DateTime now, out TimeSpan delta)
    {
        if (task.DeliveryAttempts > 10)
        {
            delta = TimeSpan.Zero;
            return false; // give up after 10 attempts
        }

        delta = TimeSpan.FromMilliseconds(
            100 * Math.Pow(2, task.DeliveryAttempts));
        return true;
    }
}
```

Access `task.DeliveryAttempts` (1-based), `task.ScheduleTime` (original send time), `task.Id` (DeliveryInfo).

---

## Ordered Delivery

```csharp
var inner = new DeliveryManager(maxPacketSize, pool);
var ordered = new SortedDeliveryManager(inner);

ordered.Received += (id, data, processTime) =>
{
    // Messages arrive in DeliveryId order
    data.Release();
};

ordered.FailedToSort += () =>
{
    Console.WriteLine("Sequence gap detected — ordering broken");
};
```

`SortedDeliveryManager` wraps any `IDeliveryManager`. It buffers out-of-order messages and delivers them only when the next expected DeliveryId arrives.

`FailedToSort` fires when a gap in the sequence is detected (a message with an ID lower than expected was received after advancing past it). This is **fatal** — the sorter enters an error state and must be replaced.

---

## Wire Format Reference

| Type | Byte 0 | Payload |
|---|---|---|
| UserSingle | 0x00 | `[DeliveryId:2][ResponseProcessTime:2][userData:N]` |
| UserMulti | 0x01 | `[DeliveryId:2][ResponseProcessTime:2][PartId:1][PartsNum:1][chunkData:N]` |
| DeliveryInfo | 0x02 | `[Count:2][{DeliveryId:2}{ChunkId:1} × Count]` |

All multi-byte integers are **little-endian**. `DeliveryInfo` is always generated by the DeliveryManager automatically — you never create it manually.

---

## Integration Pattern

```
┌──────────────┐    ScheduleDelivery    ┌──────────────────┐
│ Business     │───────────────────────▶│                  │
│ Logic        │                        │  DeliveryManager │
│              │◀───────────────────────│                  │
│              │   Received/Delivered    │                  │
└──────────────┘                        └────────┬─────────┘
                                                 │
                                    ProcessOutgoing│  ProcessIncoming
                                                 │
                                                 ▼
                                        ┌──────────────────┐
                                        │  Transport Layer  │
                                        │  (sends/receives  │
                                        │   wire packets)   │
                                        └──────────────────┘
```

1. **Business logic** calls `ScheduleDelivery` to send
2. **Your code** periodically calls `ProcessOutgoing` → sends resulting packets to the transport
3. **Transport** delivers packets → your code calls `ProcessIncoming`
4. **Events** (`Received`, `Delivered`, `FailedToDeliver`) notify business logic

### Pseudocode for the glue layer

```csharp
class MyProtocol
{
    private readonly DeliveryManager _dm;
    private readonly RetryDeliveryScheduler _scheduler;
    private readonly IConsumer<IMultiRefByteArray> _wire;

    // Called periodically (e.g., every 10ms)
    void Tick()
    {
        // 1. Process outgoing data
        var collector = new CollectingConsumer();
        _dm.ProcessOutgoing(_scheduler, DateTime.UtcNow, collector);

        // 2. Send to wire
        while (collector.TryTake(out var packet))
        {
            _wire.Send(packet);
        }

        // 3. (Receiving is driven by wire events, not by tick)
    }

    // Called when data arrives from the wire
    void OnDataReceived(IMultiRefByteArray wireData)
    {
        _dm.ProcessIncoming(wireData);
    }
}
```

---

## Memory Management Rules

1. **Input data** passed to `ScheduleDelivery` is released internally. Don't use it after the call.
2. **Received data** from the `Received` event must be released when you're done with it (call `.Release()` or use `.AsDisposable()`).
3. **Never release wire data** before passing to `ProcessIncoming` — it's released internally.
4. **Byte pool** is `MemoryRental.Shared.ByteArraysPool` in production.
5. All serialization uses the same pool — no extra allocations.

---

## Error Handling

| Situation | Signal | Action |
|---|---|---|
| Retries exhausted | `FailedToDeliver` event | Message lost; log or notify user |
| Deduplicator overflow | `ProcessIncoming` returns false | Close connection (fatal) |
| Buffer full | `ScheduleResult.BufferOverflow` | Slow down sender |
| ID collision | `ScheduleResult.IdIsNotUnique` | Fix DeliveryId assignment bug |
| Sorting gap | `FailedToSort` event | Replace SortedDeliveryManager |
| Unknown type | `ProcessIncoming` returns false | Log and ignore |

---

## Configuration Constants

If you need different values, modify `DeliveryManager.cs`:

| Constant | Default | Notes |
|---|---|---|
| `DeduplicatorCapacity` | 1024 | Must cover max in-flight packets |
| `TransportMessageQueueCapacity` | 5000 | Max pending sends before overflow |
| `SafetyMargin` | 4 | Additional headroom below MTU |
