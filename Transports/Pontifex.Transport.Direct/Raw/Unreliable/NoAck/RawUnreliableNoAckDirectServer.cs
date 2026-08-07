using System;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable.Direct;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable.NoAck.Direct
{
    public sealed class RawUnreliableNoAckDirectServer : RawUnreliableDirectServerTransport<Func<IEndPoint, IRawUnreliableHandler?>>, IRawUnreliableNoAckServer
    {
        public override TransportType Type => TransportType.RawUnreliableNoAck;

        public override int MessageMaxByteSize => RawUnreliableNoAckDirectInfo.MessageMaxByteSize;

        protected override int QueueCapacity => RawUnreliableNoAckDirectInfo.QueueCapacity;

        public RawUnreliableNoAckDirectServer(string serverName, ILogger logger, IMemoryRental memoryRental)
            : base(RawUnreliableNoAckDirectInfo.TransportName, serverName, logger, memoryRental)
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
