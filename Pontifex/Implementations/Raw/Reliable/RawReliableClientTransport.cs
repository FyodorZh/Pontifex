using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Base class for all RawReliable client transports. Owns the client-side
    /// connection lifecycle: the client endpoint is created when the transport
    /// starts, the logical connection is established by a variant handshake,
    /// and inbound traffic is routed between the connecting and connected
    /// states.
    /// </summary>
    public abstract class RawReliableClientTransport : RawReliableTransport
    {
        private volatile bool _connected;
        private volatile bool _handshakeFailed;

        protected RawReliableClientTransport(string typeName, ILogger logger, IMemoryRental memory, RawReliableTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        protected bool IsClientConnected => _connected;

        protected void MarkConnected() => _connected = true;

        protected void MarkHandshakeFailed() => _handshakeFailed = true;

        protected override void OnStarted()
        {
            var dispatcher = _dispatcher;
            if (dispatcher == null) return;

            if (!dispatcher.Post(RawWorkItem.StartClient()))
                StartClient();
        }

        protected override void ProcessServerInbound(IEndPoint source, UnionDataList message)
        {
            throw new NotSupportedException("A client transport has no server inbound path.");
        }

        protected override void StartClient()
        {
            var handler = ClientHandler;
            if (handler == null) return;

            var ep = CreateEndpoint((IRawReliableHandler)handler, ClientRemoteEndPoint);
            _clientEndpoint = ep;
            ep.MarkValid();

            BeginHandshake();
        }

        /// <summary>
        /// Variant hook invoked on the dispatcher thread once the client
        /// endpoint is created. The Ack variant builds and sends its ACK data;
        /// a no-handshake variant would complete the connection immediately.
        /// </summary>
        protected abstract void BeginHandshake();

        /// <summary>
        /// Variant hook invoked on the dispatcher thread for an inbound message
        /// received while the logical connection is still being established.
        /// </summary>
        protected abstract void HandleConnectingInbound(UnionDataList message);

        protected override void DeliverClientInbound(UnionDataList message)
        {
            var ep = (RawReliableEndpoint?)_clientEndpoint;
            if (_connected && ep != null && !_stopping && IsStarted)
            {
                DeliverToEndpoint(ep, message);
                return;
            }

            if (!_connected && !_handshakeFailed)
            {
                HandleConnectingInbound(message);
                return;
            }

            message.Release();
        }
    }
}
