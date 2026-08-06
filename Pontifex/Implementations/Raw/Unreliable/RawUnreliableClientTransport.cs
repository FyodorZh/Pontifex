using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable
{
    public abstract class RawUnreliableClientTransport : RawUnreliableTransport, IRawUnreliableClient
    {
        protected RawUnreliableClientTransport(string typeName, ILogger logger, IMemoryRental memory,
            RawUnreliableTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        public bool Init(IRawUnreliableHandler handler)
        {
            if (handler == null!)
                throw new ArgumentNullException(nameof(handler));
            return TryInitialize(handler, null);
        }

        protected override IRawUnreliableHandler? InvokeHandlerFactory(IEndPoint source, UnionDataList triggeringMessage)
        {
            throw new NotSupportedException("A client transport has no server handler factory.");
        }

        protected override void OnStarted()
        {
            var handler = ClientHandler;
            if (handler == null) return;

            var ep = CreateEndpoint(handler, ClientRemoteEndPoint);
            _clientEndpoint = ep;

            var dispatcher = _dispatcher;
            if (dispatcher == null) return;

            if (!dispatcher.Post(RawUnreliableWorkItem.StartClientEndpoint(ep)))
                StartClientEndpoint(ep);
        }
    }
}
