using System;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable.Direct
{
    public sealed class RawUnreliableDirectServer : RawUnreliableDirectServerTransport<Func<IEndPoint, IRawUnreliableHandler?>>, IRawUnreliableServer
    {
        public override TransportType Type => TransportType.RawUnreliable;

        public override int MessageMaxByteSize => RawUnreliableDirectInfo.MessageMaxByteSize;

        protected override int QueueCapacity => RawUnreliableDirectInfo.QueueCapacity;

        public RawUnreliableDirectServer(string serverName, ILogger logger, IMemoryRental memoryRental)
            : base(RawUnreliableDirectInfo.TransportName, serverName, logger, memoryRental)
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
