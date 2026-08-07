using System;
using System.Collections.Generic;
using System.Net;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable.Udp;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable.Ack.Udp
{
    public sealed class RawUnreliableAckUdpServer : RawUnreliableUdpServerTransport<Func<IEndPoint, UnionDataList, IRawUnreliableHandler?>>, IRawUnreliableAckServer
    {
        public override TransportType Type => TransportType.RawUnreliableAck;

        public override int MessageMaxByteSize => RawUnreliableAckUdpInfo.MessageMaxByteSize;

        public RawUnreliableAckUdpServer(IPAddress ipAddress, int port, ILogger logger, IMemoryRental memoryRental)
            : base(RawUnreliableAckUdpInfo.TransportName, ipAddress, port, logger, memoryRental)
        {
        }

        public bool Init(Func<IEndPoint, UnionDataList, IRawUnreliableHandler?> handlerFactory)
        {
            if (handlerFactory == null!)
                throw new ArgumentNullException(nameof(handlerFactory));
            return TryInitializeServer(handlerFactory);
        }

        protected override IRawUnreliableHandler? InvokeFactory(Func<IEndPoint, UnionDataList, IRawUnreliableHandler?> factory, IEndPoint source, UnionDataList triggeringMessage)
            => factory(source, triggeringMessage);
    }
}
