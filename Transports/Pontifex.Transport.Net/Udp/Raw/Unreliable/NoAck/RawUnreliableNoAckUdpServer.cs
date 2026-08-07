using System;
using System.Net;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable.Udp;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable.NoAck.Udp
{
    public sealed class RawUnreliableNoAckUdpServer : RawUnreliableUdpServerTransport<Func<IEndPoint, IRawUnreliableHandler?>>, IRawUnreliableNoAckServer
    {
        public override TransportType Type => TransportType.RawUnreliableNoAck;

        public override int MessageMaxByteSize => RawUnreliableNoAckUdpInfo.MessageMaxByteSize;

        public RawUnreliableNoAckUdpServer(IPAddress ipAddress, int port, ILogger logger, IMemoryRental memoryRental)
            : base(RawUnreliableNoAckUdpInfo.TransportName, ipAddress, port, logger, memoryRental)
        {
        }

        public bool Init(Func<IEndPoint, IRawUnreliableHandler?> handlerFactory)
        {
            if (handlerFactory == null!)
                throw new ArgumentNullException(nameof(handlerFactory));
            return TryInitializeServer(handlerFactory);
        }

        protected override IRawUnreliableHandler? InvokeFactory(Func<IEndPoint, IRawUnreliableHandler?> factory, IEndPoint source, UnionDataList triggeringMessage)
            => factory(source);
    }
}
