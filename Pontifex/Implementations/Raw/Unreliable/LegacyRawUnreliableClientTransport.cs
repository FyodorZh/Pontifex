using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable
{
    /// <remarks>
    /// This is a legacy base kept as a temporary shim for the carrier transports and is scheduled for
    /// deletion in Stage 6. The canonical name <c>RawUnreliableClientTransport</c> is reserved for the
    /// new architecture.
    /// </remarks>
    public abstract class LegacyRawUnreliableClientTransport : LegacyRawUnreliableTransport, IRawUnreliableClient
    {
        protected LegacyRawUnreliableClientTransport(string typeName, ILogger logger, IMemoryRental memory,
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
