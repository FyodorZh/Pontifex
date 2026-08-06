using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable.Ack
{
    public abstract class RawUnreliableAckServerTransport : RawUnreliableAckTransport
    {
        protected RawUnreliableAckServerTransport(string typeName, ILogger logger, IMemoryRental memory,
            RawUnreliableAckTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        public bool Init(Func<IEndPoint, UnionDataList, IRawUnreliableHandler?> handlerFactory)
        {
            if (handlerFactory == null!)
                throw new ArgumentNullException(nameof(handlerFactory));
            return TryInitialize(null, handlerFactory);
        }

        protected override IEndPoint? ClientRemoteEndPoint => null;
    }
}
