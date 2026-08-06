using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable.NoAck
{
    public abstract class RawUnreliableNoAckServerTransport : RawUnreliableServerTransport<Func<IEndPoint, IRawUnreliableHandler?>>
    {
        protected RawUnreliableNoAckServerTransport(string typeName, ILogger logger, IMemoryRental memory,
            RawUnreliableTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl)
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
