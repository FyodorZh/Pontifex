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
    /// states. Carries the RawReliable shared machinery on the client side:
    /// endpoint creation, delivery, inbound injection, and client teardown.
    /// </summary>
    public abstract class RawReliableClientTransport : RawClientTransport
    {
        protected new RawReliableTransportConformanceControl Conformance => (RawReliableTransportConformanceControl)base.Conformance;

        private volatile bool _connected;
        private volatile bool _handshakeFailed;

        protected RawReliableClientTransport(string typeName, ILogger logger, IMemoryRental memory, RawReliableTransportConformanceControl conformanceControl)
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        protected bool IsClientConnected => _connected;

        protected void MarkConnected() => _connected = true;

        protected void MarkHandshakeFailed() => _handshakeFailed = true;

        /// <summary>
        /// The client endpoint created on start, or null until the client
        /// lifecycle has begun.
        /// </summary>
        protected RawReliableEndpoint? ClientEndpoint => (RawReliableEndpoint?)_clientEndpoint;

        /// <summary>
        /// Commits an accepted message to the carrier for the given endpoint.
        /// Ownership of the message transfers to the carrier; it must release it
        /// on any non-<see cref="SendResult.Ok"/> result.
        /// </summary>
        protected abstract SendResult SendToCarrier(RawReliableEndpoint endpoint, UnionDataList message);

        /// <summary>
        /// The configured remote destination for the client endpoint.
        /// </summary>
        protected abstract IEndPoint? ClientRemoteEndPoint { get; }

        /// <summary>
        /// Creates the endpoint for the logical connection and wires its send,
        /// disconnect, and inbound-injection operations. Carriers may override
        /// to return a <see cref="RawReliableEndpoint"/> subclass that exposes
        /// transport-specific controls.
        /// </summary>
        protected virtual RawReliableEndpoint CreateEndpoint(IRawReliableHandler handler, IEndPoint? remote)
        {
            var ep = new RawReliableEndpoint(this, handler, remote)
            {
                SendDelegate = SendToCarrier,
                DisconnectDelegate = StopEndpoint
            };
            ep.WireInjector(msg => InjectInboundToEndpoint(ep, msg));
            return ep;
        }

        protected override void DeliverToEndpoint(RawEndpoint endpoint, UnionDataList message)
        {
            var ep = (RawReliableEndpoint)endpoint;
            if (!ep.IsValidInternal)
            {
                message.Release();
                return;
            }

            ep.HitAfterReceivedGate();

            if (_stopping || !IsStarted || !ep.IsValidInternal)
            {
                message.Release();
                return;
            }

            try
            {
                lock (ep.CallbackLock)
                {
                    if (!ep.IsValidInternal)
                    {
                        message.Release();
                        return;
                    }

                    ep.RawHandler.OnReceived(message);
                }
            }
            catch (Exception e)
            {
                Log.wtf(e);
                StopEndpoint(ep, new StopReasons.ExceptionFail(Name, e, "handler.OnReceived threw"));
            }
        }

        /// <summary>
        /// Injects an inbound message into the client endpoint's receive path as
        /// if it had arrived from the carrier. Used by the endpoint conformance
        /// control's <c>InjectInboundData</c>. Malformed or oversized data is
        /// discarded and the logical connection is disconnected without stopping
        /// the transport.
        /// </summary>
        internal void InjectInboundToEndpoint(RawReliableEndpoint ep, UnionDataList data)
        {
            if (data == null!)
            {
                Log.e("RawReliable client transport: injected inbound data is null");
                StopEndpoint(ep, new StopReasons.TextFail(Name, "Injected inbound data is null"));
                return;
            }

            if (data.GetDataSize() > MessageMaxByteSize)
            {
                data.Release();
                Log.e("RawReliable client transport: injected oversized inbound data");
                StopEndpoint(ep, new StopReasons.TextFail(Name, "Injected inbound data exceeds MessageMaxByteSize"));
                return;
            }

            OnCarrierInbound(data);
        }

        /// <summary>
        /// Runs the client endpoint teardown: the handler's OnDisconnected fires
        /// when the logical connection completed, and the client OnStopped fires
        /// in every teardown so the client always observes the connection end.
        /// </summary>
        protected override void TeardownEndpoint(RawEndpoint endpoint, StopReason reason)
        {
            var ep = (RawReliableEndpoint)endpoint;
            if (ep.TeardownDone) return;
            ep.MarkTeardownDone();

            if (ep.OnStartedCompleted)
            {
                ep.HitBeforeHandlerDisconnectedGate();
                ep.MarkDisconnected();
                lock (ep.CallbackLock)
                {
                    try { ep.Handler.OnDisconnected(reason); }
                    catch (Exception e) { Log.wtf(e); }
                }
            }

            if (ep.Handler is IRawReliableClientHandler clientHandler)
            {
                if (ep.OnStartedCompleted)
                {
                    ep.HitBeforeHandlerStoppedGate();
                }
                lock (ep.CallbackLock)
                {
                    try { clientHandler.OnStopped(reason); }
                    catch (Exception e) { Log.wtf(e); }
                }
            }
        }

        protected override void OnStarted()
        {
            var dispatcher = _dispatcher;
            if (dispatcher == null) return;

            if (!dispatcher.Post(RawWorkItem.StartClient()))
                StartClient();
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
