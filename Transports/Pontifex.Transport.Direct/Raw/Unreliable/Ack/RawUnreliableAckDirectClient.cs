using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable.Direct;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable.Ack.Direct
{
    public sealed class RawUnreliableAckDirectClient : RawUnreliableDirectClientTransport, IRawUnreliableAckClient
    {
        public override TransportType Type => TransportType.RawUnreliableAck;

        public override int MessageMaxByteSize => RawUnreliableAckDirectInfo.MessageMaxByteSize;

        protected override int QueueCapacity => RawUnreliableAckDirectInfo.QueueCapacity;

        public RawUnreliableAckDirectClient(string serverName, ILogger logger, IMemoryRental memoryRental)
            : base(RawUnreliableAckDirectInfo.TransportName, serverName, logger, memoryRental)
        {
        }
    }
}
