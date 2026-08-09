using System;
using Actuarius.Memory;
using Pontifex.Raw.Direct;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Raw.Reliable.Direct;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Direct
{
    /// <summary>
    /// RawReliableAck Direct server transport. Admits clients through the
    /// acknowledger-based ACK handshake and sends the handshake wire messages
    /// over the Direct channel.
    /// </summary>
    public sealed class RawReliableAckDirectServer : RawReliableDirectServerTransport, IRawReliableAckServer
    {
        public override TransportType Type => TransportType.RawReliableAck;

        public override int MessageMaxByteSize => RawReliableAckDirectInfo.MessageMaxByteSize;

        protected override int QueueCapacity => RawReliableAckDirectInfo.QueueCapacity;

        protected override int DispatcherCapacity => RawReliableAckDirectInfo.QueueCapacity;

        public RawReliableAckDirectServer(string serverName, ILogger logger, IMemoryRental memoryRental)
            : base(RawReliableAckDirectInfo.TransportName, serverName, logger, memoryRental)
        {
        }

        protected override void SendAckResponseToClient(IEndPoint source, UnionDataList ackResponse)
        {
            if (!_channels.TryGetValue(source, out var channel))
            {
                ackResponse.Release();
                return;
            }

            ackResponse.PutFirst(new UnionData(RawReliableAckDirectInfo.AckOKResponse));
            channel.SendToClient(ackResponse);
        }

        protected override void SendRejectionToClient(IEndPoint source)
        {
            if (!_channels.TryGetValue(source, out var channel))
            {
                return;
            }

            var rejection = Memory.CollectablePool.Acquire<UnionDataList>();
            rejection.PutFirst(new UnionData(RawReliableAckDirectInfo.AckRejectResponse));
            channel.SendToClient(rejection);
        }
    }
}
