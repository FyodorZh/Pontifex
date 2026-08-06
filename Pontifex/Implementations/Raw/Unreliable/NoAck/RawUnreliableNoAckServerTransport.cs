using System;
using Actuarius.Memory;
using Scriba;

namespace Pontifex.Raw.Unreliable.NoAck
{
    public abstract class RawUnreliableNoAckServerTransport : RawUnreliableNoAckTransport
    {
        protected RawUnreliableNoAckServerTransport(string typeName, ILogger logger, IMemoryRental memory,
            RawUnreliableNoAckTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        public bool Init(Func<IEndPoint, IRawUnreliableHandler?> handlerFactory)
        {
            if (handlerFactory == null!)
                throw new ArgumentNullException(nameof(handlerFactory));
            return TryInitialize(null, handlerFactory);
        }

        protected override IEndPoint? ClientRemoteEndPoint => null;
    }
}
