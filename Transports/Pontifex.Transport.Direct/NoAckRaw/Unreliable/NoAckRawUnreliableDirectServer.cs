using System;
using Actuarius.Memory;
using Pontifex.NoAck.Raw.Direct;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.NoAck.Raw.Unreliable.Direct
{
    public sealed class NoAckRawUnreliableDirectServer : NoAckRawDirectServer, INoAckRawUnreliableServer
    {
        public override TransportType Type => TransportType.NoAckRawUnreliable;

        public NoAckRawUnreliableDirectServer(string serverName, ILogger logger, IMemoryRental memoryRental)
            : base(serverName, "direct-noack-raw-unreliable", logger, memoryRental)
        {
        }

        public SendResult TrySend(IEndPoint destination, UnionDataList message) => SendToClient(destination, message);
    }
}
