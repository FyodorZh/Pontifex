using System;
using System.Net;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable.Udp
{
    public sealed class RawUnreliableUdpServer : RawUnreliableUdpServerTransport<Func<IEndPoint, IRawUnreliableHandler?>>, IRawUnreliableServer
    {
        public override TransportType Type => TransportType.RawUnreliable;

        public override int MessageMaxByteSize => RawUnreliableUdpInfo.MessageMaxByteSize;

        protected override int QueueCapacity => RawUnreliableUdpInfo.QueueCapacity;

        protected override long SendBudgetBytes => RawUnreliableUdpInfo.SendBudgetBytes;

        protected override int SendBudgetMessages => RawUnreliableUdpInfo.SendBudgetMessages;

        public RawUnreliableUdpServer(IPAddress ipAddress, int port, ILogger logger, IMemoryRental memoryRental)
            : base(RawUnreliableUdpInfo.TransportName, ipAddress, port, logger, memoryRental)
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
