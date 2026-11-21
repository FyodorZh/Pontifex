using System;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable.Direct
{
    public sealed class RawUnreliableDirectClient : RawUnreliableDirectClientTransport, IRawUnreliableClient
    {
        public override TransportType Type => TransportType.RawUnreliable;

        public override int MessageMaxByteSize => RawUnreliableDirectInfo.MessageMaxByteSize;

        protected override int QueueCapacity => RawUnreliableDirectInfo.QueueCapacity;

        public RawUnreliableDirectClient(string serverName, ILogger logger, IMemoryRental memoryRental)
            : base(RawUnreliableDirectInfo.TransportName, serverName, logger, memoryRental)
        {
        }
    }
}
