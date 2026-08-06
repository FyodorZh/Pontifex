using System;
using Actuarius.Memory;
using Scriba;

namespace Pontifex.Raw.Unreliable.Ack
{
    public abstract class RawUnreliableAckClientTransport : RawUnreliableAckTransport
    {
        protected RawUnreliableAckClientTransport(string typeName, ILogger logger, IMemoryRental memory,
            RawUnreliableAckTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        public bool Init(IRawUnreliableHandler handler)
        {
            if (handler == null!)
                throw new ArgumentNullException(nameof(handler));
            return TryInitialize(handler, null);
        }

        protected override void OnStarted()
        {
            var handler = ClientHandler;
            if (handler == null) return;

            var ep = CreateEndpoint(handler, ClientRemoteEndPoint);
            _clientEndpoint = ep;

            var dispatcher = _dispatcher;
            if (dispatcher == null) return;

            if (!dispatcher.Post(RawUnreliableAckWorkItem.StartClientEndpoint(ep)))
                StartClientEndpoint(ep);
        }
    }
}
