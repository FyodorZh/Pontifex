using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable.Direct;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable.NoAck.Direct
{
    public sealed class RawUnreliableNoAckDirectClient : RawUnreliableDirectClientTransport, IRawUnreliableNoAckClient
    {
        public override TransportType Type => TransportType.RawUnreliableNoAck;

        public override int MessageMaxByteSize => RawUnreliableNoAckDirectInfo.MessageMaxByteSize;

        protected override int QueueCapacity => RawUnreliableNoAckDirectInfo.QueueCapacity;

        public RawUnreliableNoAckDirectClient(string serverName, ILogger logger, IMemoryRental memoryRental)
            : base(RawUnreliableNoAckDirectInfo.TransportName, serverName, logger, memoryRental)
        {
        }
    }
}
