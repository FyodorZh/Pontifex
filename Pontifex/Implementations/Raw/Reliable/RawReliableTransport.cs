using System;
using Actuarius.Memory;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Utils;
using Pontifex.Utils.CheckPointGate;
using Scriba;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Base class for all RawReliable transports. Owns the connection-oriented
    /// lifecycle and the shared conformance controls for the RawReliable
    /// contract variants, on top of the generic <see cref="RawTransport"/>
    /// dispatcher and teardown scaffolding.
    /// </summary>
    public abstract class RawReliableTransport : RawTransport
    {
        protected new RawReliableTransportConformanceControl Conformance => (RawReliableTransportConformanceControl)base.Conformance;

        protected RawReliableTransport(string typeName, ILogger logger, IMemoryRental memory, RawReliableTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl ?? new RawReliableTransportConformanceControl())
        {
        }

        /// <summary>
        /// Test-only conformance control for a RawReliable transport. All
        /// checkpoint gates are inactive until armed by a conformance adapter.
        /// </summary>
        protected class RawReliableTransportConformanceControl : RawConformanceControl, IRawReliableAckTransportConformanceControl
        {
            private readonly CheckPoint _beforeAcknowledgerGate = new();
            private readonly CheckPoint _beforeAckResponseCommitGate = new();
            private readonly CheckPoint _beforeHandlerConnectedGate = new();

            public ICheckPointCtl BeforeAcknowledgerGate => _beforeAcknowledgerGate;

            public ICheckPointCtl BeforeAckResponseCommitGate => _beforeAckResponseCommitGate;

            public ICheckPointCtl BeforeHandlerConnectedGate => _beforeHandlerConnectedGate;
        }

        /// <summary>
        /// Commits an accepted message to the carrier for the given endpoint.
        /// Ownership of the message transfers to the carrier; it must release it
        /// on any non-<see cref="SendResult.Ok"/> result.
        /// </summary>
        protected abstract SendResult SendToCarrier(RawReliableEndpoint endpoint, UnionDataList message);

        /// <summary>
        /// The configured remote destination for the client endpoint, or null
        /// for a server transport.
        /// </summary>
        protected abstract IEndPoint? ClientRemoteEndPoint { get; }

        protected RawReliableEndpoint CreateEndpoint(IRawReliableHandler handler, IEndPoint? remote)
        {
            var ep = new RawReliableEndpoint(this, handler, remote)
            {
                SendDelegate = SendToCarrier,
                DisconnectDelegate = StopEndpoint
            };
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
                ep.RawHandler.OnReceived(message);
            }
            catch (Exception e)
            {
                Log.wtf(e);
                StopEndpoint(ep, new StopReasons.ExceptionFail(Name, e, "handler.OnReceived threw"));
            }
        }

        /// <summary>
        /// Disconnects the server session for a source route, typically when the
        /// peer connection closes. The disconnect is scheduled on the dispatcher
        /// so session callbacks stay serialized. No-op while the server is
        /// stopping (its own teardown already covers active sessions).
        /// </summary>
        protected void DisconnectSessionEndpoint(IEndPoint source, StopReason reason)
        {
            if (_stopping || !IsStarted) return;

            if (_routes.TryGetValue(source, out var ep))
            {
                StopEndpoint(ep, reason);
            }
        }

        /// <summary>
        /// Injects an inbound message into a specific endpoint's receive path as
        /// if it had arrived from the carrier. Used by the endpoint conformance
        /// control's <c>InjectInboundData</c>. Malformed or oversized data is
        /// discarded and the logical connection is disconnected without stopping
        /// the transport.
        /// </summary>
        internal void InjectInboundToEndpoint(RawReliableEndpoint ep, UnionDataList data)
        {
            if (data == null!)
            {
                Log.e("RawReliable transport: injected inbound data is null");
                StopEndpoint(ep, new StopReasons.TextFail(Name, "Injected inbound data is null"));
                return;
            }

            if (data.GetDataSize() > MessageMaxByteSize)
            {
                data.Release();
                Log.e("RawReliable transport: injected oversized inbound data");
                StopEndpoint(ep, new StopReasons.TextFail(Name, "Injected inbound data exceeds MessageMaxByteSize"));
                return;
            }

            OnCarrierInbound(ReferenceEquals(ep, _clientEndpoint) ? null : ep.RemoteEndPoint, data);
        }

        protected override void TeardownEndpoint(RawEndpoint endpoint, StopReason reason)
        {
            var ep = (RawReliableEndpoint)endpoint;
            if (ep.TeardownDone) return;
            ep.MarkTeardownDone();

            if (ep.OnStartedCompleted)
            {
                ep.HitBeforeHandlerDisconnectedGate();
                ep.MarkDisconnected();
                try { ep.Handler.OnDisconnected(reason); }
                catch (Exception e) { Log.wtf(e); }

                if (ReferenceEquals(ep, _clientEndpoint) && ep.Handler is IRawReliableClientHandler clientHandler)
                {
                    ep.HitBeforeHandlerStoppedGate();
                    try { clientHandler.OnStopped(reason); }
                    catch (Exception e) { Log.wtf(e); }
                }
            }
            else if (ReferenceEquals(ep, _clientEndpoint) && ep.Handler is IRawReliableClientHandler clientHandler)
            {
                try { clientHandler.OnStopped(reason); }
                catch (Exception e) { Log.wtf(e); }
            }

            if (ep.RemoteEndPoint != null &&
                _routes.TryGetValue(ep.RemoteEndPoint, out var current) &&
                ReferenceEquals(current, ep))
            {
                _routes.Remove(ep.RemoteEndPoint);
            }

            if (!ReferenceEquals(ep, _clientEndpoint) && ep.RemoteEndPoint != null)
            {
                OnServerSessionEnded(ep.RemoteEndPoint);
            }
        }

        /// <summary>
        /// Invoked after a server session endpoint is torn down, with the
        /// session's source route. Carriers use this to release the peer
        /// connection so the client observes the disconnect.
        /// </summary>
        protected virtual void OnServerSessionEnded(IEndPoint source)
        {
        }
    }
}
