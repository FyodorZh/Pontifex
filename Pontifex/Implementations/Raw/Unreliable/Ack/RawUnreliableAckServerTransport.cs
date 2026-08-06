using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable.Ack
{
    public abstract class RawUnreliableAckServerTransport : RawUnreliableServerTransport<Func<IEndPoint, UnionDataList, IRawUnreliableHandler?>>
    {
        protected RawUnreliableAckServerTransport(string typeName, ILogger logger, IMemoryRental memory,
            RawUnreliableTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl)
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
