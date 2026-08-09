using System;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Raw.Reliable.Direct;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Direct
{
    /// <summary>
    /// RawReliableAck Direct client transport. Establishes its logical
    /// connection through the Direct channel ACK handshake.
    /// </summary>
    public sealed class RawReliableAckDirectClient : RawReliableDirectClientTransport, IRawReliableAckClient
    {
        public override TransportType Type => TransportType.RawReliableAck;

        public override int MessageMaxByteSize => RawReliableAckDirectInfo.MessageMaxByteSize;

        protected override int QueueCapacity => RawReliableAckDirectInfo.QueueCapacity;

        protected override int DispatcherCapacity => RawReliableAckDirectInfo.QueueCapacity;

        public RawReliableAckDirectClient(string serverName, ILogger logger, IMemoryRental memoryRental)
            : base(RawReliableAckDirectInfo.TransportName, serverName, logger, memoryRental)
        {
        }

        protected override void HandleConnectingInbound(UnionDataList message)
        {
            if (!message.TryPopFirst(out IMultiRefReadOnlyByteArray? marker))
            {
                message.Release();
                FailEstablishment(new TextFail(Name, "Malformed ACK response"));
                return;
            }

            if (marker.EqualByContent(RawReliableAckDirectInfo.AckOKResponse))
            {
                marker.Release();
                CompleteConnect(message);
            }
            else if (marker.EqualByContent(RawReliableAckDirectInfo.AckRejectResponse))
            {
                marker.Release();
                message.Release();
                FailEstablishment(new AckRejected(Name));
            }
            else
            {
                marker.Release();
                message.Release();
                FailEstablishment(new TextFail(Name, "Unknown ACK response marker"));
            }
        }
    }
}
