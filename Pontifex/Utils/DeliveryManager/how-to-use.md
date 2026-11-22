# DeliveryManager — How to Use

`Pontifex.Delivery.DeliveryManager` turns an unreliable, unordered packet transport into a
reliable, optionally ordered delivery system. It splits and reassembles messages, retransmits
unacknowledged chunks, suppresses duplicates, batches acknowledgements, and reports delivery
outcomes.

The type is internal to the Pontifex assembly. Transport adapters such as the RawReliable
converters construct it directly; application code normally sees it through the transport that
owns it.

## Construction

```csharp
using Pontifex.Delivery;

var manager = new DeliveryManager(
    messageMaxByteSize: 1400,
    bytesPool: MemoryRental.Shared.ByteArraysPool,
    collectablePool: MemoryRental.Shared.CollectablePool);
```

- `messageMaxByteSize` is the maximum size of one wire packet, including the delivery envelope.
  It bounds `DeliveryMaxByteSize`, the largest user payload accepted by `ScheduleDelivery`.
- `bytesPool` supplies the byte arrays used for splitting and reassembly.
- `collectablePool` supplies the pooled objects used for wire messages and delivery metadata.

## Sending

```csharp
UnionDataList data = /* user payload */;
SendResult result = manager.ScheduleDelivery(data, out DeliveryId id);
```

`ScheduleDelivery` takes ownership of `data` and releases it before returning. The assigned `id`
is unique for the manager instance and starts at 1. A rejected call returns
`SendResult.InvalidMessage`, `SendResult.MessageTooBig`, or another error and sets `id` to its
default value.

Outgoing wire traffic is pumped explicitly:

```csharp
manager.ProcessOutgoing(
    scheduler,
    DateTime.UtcNow,
    new SendConsumer(endpoint));
```

`ProcessOutgoing` emits new deliveries, retransmissions, and batched delivery confirmations.
`RetryDeliveryScheduler(disconnectTimeout, baseIntervalMs)` is the standard scheduler. The caller
must invoke `ProcessOutgoing` periodically and from the same thread as `ProcessIncoming`.

## Receiving

```csharp
manager.Received += (DeliveryId id, UnionDataList payload) =>
{
    try
    {
        // Consume payload.
    }
    finally
    {
        payload.Release(); // the callback owns the received reference
    }
};

manager.Delivered += id => { /* all chunks acknowledged */ };
manager.FailedToDeliver += id => { /* retries exhausted */ };

bool accepted = manager.ProcessIncoming(wireData);
```

`ProcessIncoming` takes ownership of `wireData` and releases it before returning. It accepts two
wire shapes, distinguished by the first element:

- **User packet** — `bool isUser = true`, then `ushort wireChunkId`, then the packed delivery
  metadata and payload.
- **Delivery report** — `bool isUser = false`, then the serialized delivery confirmations.

The method returns `false` for malformed packets and for a deduplication-window overflow.

## Reset

`Clear()` drops pending outgoing deliveries, queued unsent messages, retransmission state, and
partial reassembly buffers. It does not reset the inbound deduplication window or the
delivery-id sequence; those belong to the connection lifetime of the manager instance. Create a
new manager when a fully fresh sequence and deduplication window are required.
