using System;
using Actuarius.Collections;
using Pontifex.Utils;

namespace Pontifex.Delivery
{
    /// <summary>
    /// Side of the DeliveryManager visible to business logic: scheduling outgoing messages
    /// and receiving incoming messages with delivery notifications.
    /// </summary>
    internal interface IDeliveryManagerUserSide
    {
        /// <summary>
        /// Fires when a complete message is received from the remote side.
        /// The caller must release the provided <see cref="UnionDataList"/> when done with it.
        /// </summary>
        event Action<DeliveryId, UnionDataList>? Received;

        /// <summary>
        /// Fires when a previously scheduled message has been confirmed delivered
        /// by the remote side (all chunks acknowledged).
        /// </summary>
        event Action<DeliveryId>? Delivered;

        /// <summary>
        /// Fires when a scheduled message cannot be delivered because retries have been exhausted.
        /// </summary>
        event Action<DeliveryId>? FailedToDeliver;

        /// <summary>
        /// Maximum total size in bytes of user data that can be passed to <see cref="ScheduleDelivery"/>.
        /// Messages exceeding this size are rejected with <see cref="SendResult.MessageTooBig"/>.
        /// </summary>
        int DeliveryMaxByteSize { get; }

        /// <summary>
        /// Schedule a message for reliable delivery. The data is serialized and automatically split
        /// into wire chunks if necessary. The <paramref name="data"/> is released internally
        /// — do not use it after this call.
        /// </summary>
        /// <param name="data">User data to send. Released on return.</param>
        /// <param name="deliveryId">The auto-assigned unique ID for this delivery.</param>
        /// <returns><see cref="SendResult.Ok"/> on success, or an error code.</returns>
        SendResult ScheduleDelivery(UnionDataList data, out DeliveryId deliveryId);
    }

    /// <summary>
    /// Side of the DeliveryManager visible to the transport/protocol adapter:
    /// feeding incoming wire data and pumping outgoing wire data.
    /// </summary>
    internal interface IDeliveryManagerTransportSide
    {
        /// <summary>
        /// Feed an incoming wire packet into the delivery system. The first element
        /// must be the <c>ushort</c> packetId. The data is released internally —
        /// do not release it yourself after this call.
        /// </summary>
        /// <param name="data">The incoming wire packet with packetId as first element.</param>
        /// <returns>true if the packet was recognized (user data, ACK) and processed;
        /// false if it was malformed or caused a deduplicator overflow.</returns>
        bool ProcessIncoming(UnionDataList data);

        /// <summary>
        /// Pump outgoing wire packets (new deliveries, retransmissions, and batched delivery
        /// confirmations) into the provided consumer. Each outgoing packet has the
        /// <c>ushort</c> packetId as its first element. Call periodically (e.g., every 10-50ms).
        /// Must be called from the same thread as <see cref="ProcessIncoming"/>.
        /// </summary>
        /// <param name="scheduler">Controls retry timing per delivery attempt.</param>
        /// <param name="now">Current UTC time used for scheduling decisions.</param>
        /// <param name="dst">Consumer that receives wire packets ready for transmission.</param>
        void ProcessOutgoing(IDeliveryAttemptScheduler scheduler, DateTime now, IConsumer<UnionDataList> dst);
    }

    /// <summary>
    /// Combined interface providing full access to the delivery manager.
    /// </summary>
    internal interface IDeliveryManager : IDeliveryManagerUserSide, IDeliveryManagerTransportSide
    {
        /// <summary>
        /// Reset all state: clear send/receive queues, pending deliveries, and partial
        /// reassembly buffers. Call on connection close or reset.
        /// </summary>
        void Clear();
    }
}
