using System;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable.Direct;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable.Ack.Direct
{
    public sealed class RawUnreliableAckDirectServer : RawUnreliableDirectServerTransport<Func<IEndPoint, UnionDataList, IRawUnreliableHandler?>>, IRawUnreliableAckServer
    {
        public override TransportType Type => TransportType.RawUnreliableAck;

        public override int MessageMaxByteSize => RawUnreliableAckDirectInfo.MessageMaxByteSize;

        protected override int QueueCapacity => RawUnreliableAckDirectInfo.QueueCapacity;

        public RawUnreliableAckDirectServer(string serverName, ILogger logger, IMemoryRental memoryRental)
            : base(RawUnreliableAckDirectInfo.TransportName, serverName, logger, memoryRental)
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
