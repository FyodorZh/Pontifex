using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable.Ack;
using Pontifex.Raw.Unreliable.NoAck;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Pontifex.Utils.CheckPointGate;
using Scriba;

namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Base class for all RawUnreliable transports. Owns the endpoint and
    /// handler lifecycle, routing, and conformance machinery shared by the Ack
    /// and NoAck contract variants, on top of the generic
    /// <see cref="RawTransport"/> dispatcher and teardown scaffolding. Concrete
    /// transports implement only the abstract carrier hooks and call
    /// <see cref="RawTransport.OnCarrierInbound"/>.
    /// </summary>
    /// <remarks>
    /// The Ack and NoAck contracts differ only in the server handler factory
    /// signature; that difference is isolated to
    /// <see cref="InvokeHandlerFactory"/>, supplied by the variant server base.
    /// </remarks>
    public abstract class RawUnreliableTransport : RawTransport
    {
        protected new RawUnreliableTransportConformanceControl Conformance => (RawUnreliableTransportConformanceControl)base.Conformance;

        protected RawUnreliableTransport(string typeName, ILogger logger, IMemoryRental memory, RawUnreliableTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl ?? new RawUnreliableTransportConformanceControl())
        {
        }

        /// <summary>
        /// Test-only conformance control for a RawUnreliable transport.
        /// Implements both the Ack and NoAck contract variants. All checkpoint
        /// gates are inactive until armed by a conformance adapter.
        /// </summary>
        protected class RawUnreliableTransportConformanceControl : RawUnreliableConformanceControl, IRawUnreliableNoAckTransportConformanceControl, IRawUnreliableAckTransportConformanceControl
        {
            private readonly CheckPoint _beforeHandlerFactoryGate = new();
            private readonly CheckPoint _beforeHandlerStartedGate = new();

            public ICheckPointCtl BeforeHandlerFactoryGate => _beforeHandlerFactoryGate;

            public ICheckPointCtl BeforeHandlerStartedGate => _beforeHandlerStartedGate;

            public bool TryMakeReliable() => ((RawUnreliableTransport)_owner).TryMakeReliableForDebug();
        }

        protected class RawUnreliableConformanceControl : RawConformanceControl, IRawUnreliableConformanceControl
        {
        }

        /// <summary>
        /// Commits an accepted message to the carrier for the given endpoint.
        /// Ownership of the message transfers to the carrier; it must release it
        /// on any non-<see cref="SendResult.Ok"/> result.
        /// </summary>
        protected abstract SendResult SendToCarrier(RawUnreliableEndpoint endpoint, UnionDataList message);

        /// <summary>
        /// The configured remote destination for the client endpoint, or null
        /// for a server transport.
        /// </summary>
        protected abstract IEndPoint? ClientRemoteEndPoint { get; }

        /// <summary>
        /// Enables transport-wide reliable debug mode before Start. Returns false
        /// when the implementation cannot provide the test mode.
        /// </summary>
        protected abstract bool TryMakeReliableForDebug();

        /// <summary>
        /// Invokes the variant server handler factory with the inbound source
        /// route and its triggering message. Only a running server transport
        /// invokes this; client transports never reach it.
        /// </summary>
        protected abstract IRawUnreliableHandler? InvokeHandlerFactory(IEndPoint source, UnionDataList triggeringMessage);

        /// <summary>
        /// Starts the single client endpoint after the client transport starts.
        /// Called on the dispatcher thread.
        /// </summary>
        protected void StartClientEndpoint(RawUnreliableEndpoint ep)
        {
            Conformance.BeforeHandlerStartedGate.Hit();
            ep.MarkValid();
            try
            {
                ep.Handler.OnStarted(ep);
                ep.MarkOnStartedCompleted();
            }
            catch (Exception e)
            {
                Log.wtf(e);
                ep.MarkInvalid();
                var dispatcher = _dispatcher;
                if (dispatcher == null || !dispatcher.Post(RawWorkItem.Stop(new StopReasons.ExceptionFail(Name, e, "client handler.OnStarted threw"))))
                    Stop(new StopReasons.ExceptionFail(Name, e, "client handler.OnStarted threw"));
            }
        }

        protected override void ProcessServerInbound(IEndPoint source, UnionDataList message)
        {
            if (_stopping || !IsStarted)
            {
                message.Release();
                return;
            }

            if (_routes.TryGetValue(source, out var existing))
            {
                DeliverToEndpoint(existing, message);
                return;
            }

            Conformance.BeforeHandlerFactoryGate.Hit();

            IRawUnreliableHandler? handler;
            try { handler = InvokeHandlerFactory(source, message); }
            catch (Exception e) { Log.wtf(e); message.Release(); return; }

            if (handler == null)
            {
                message.Release();
                return;
            }

            var ep = CreateEndpoint(handler, source);
            _routes[source] = ep;

            Conformance.BeforeHandlerStartedGate.Hit();
            ep.MarkValid();
            try
            {
                ep.Handler.OnStarted(ep);
                ep.MarkOnStartedCompleted();
            }
            catch (Exception e)
            {
                Log.wtf(e);
                _routes.TryRemove(source, out _);
                ep.MarkInvalid();
                message.Release();
                return;
            }

            DeliverToEndpoint(ep, message);
        }

        protected override void TeardownEndpoint(RawEndpoint endpoint, StopReason reason)
        {
            var ep = (RawUnreliableEndpoint)endpoint;
            if (ep.TeardownDone) return;
            ep.MarkTeardownDone();

            ep.HitBeforeHandlerStoppedGate();

            if (ep.OnStartedCompleted)
            {
                try { ep.Handler.OnStopped(reason); }
                catch (Exception e) { Log.wtf(e); }
            }

            if (ep.RemoteEndPoint != null &&
                _routes.TryGetValue(ep.RemoteEndPoint, out var current) &&
                ReferenceEquals(current, ep))
            {
                _routes.TryRemove(ep.RemoteEndPoint, out _);
            }
        }

        protected RawUnreliableEndpoint CreateEndpoint(IRawUnreliableHandler handler, IEndPoint? remote)
        {
            var ep = new RawUnreliableEndpoint(this, handler, remote)
            {
                SendDelegate = SendToCarrier,
                StopDelegate = StopEndpoint
            };
            return ep;
        }
    }
}
