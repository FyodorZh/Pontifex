using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Pontifex.Utils.CheckPointGate;
using Scriba;

namespace Pontifex.Raw.Unreliable.NoAck
{
    /// <summary>
    /// Base class for all RawUnreliableNoAck transports. Owns all endpoint,
    /// handler, lifecycle, routing, and conformance machinery shared by the
    /// client and server variants. Concrete transports implement only the
    /// abstract carrier hooks and call <see cref="OnCarrierInbound"/>.
    /// </summary>
    public abstract class RawUnreliableNoAckTransport : RawUnreliableTransport
    {
        protected new IRawUnreliableNoAckTransportConformanceControl Conformance => (IRawUnreliableNoAckTransportConformanceControl)base.Conformance;

        protected RawUnreliableNoAckTransport(string typeName, ILogger logger, IMemoryRental memory, RawUnreliableNoAckTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl ?? new RawUnreliableNoAckTransportConformanceControl())
        {
        }

        /// <summary>
        /// Test-only conformance control for a RawUnreliableNoAck transport.
        /// All checkpoint gates are inactive until armed by a conformance adapter.
        /// </summary>
        protected class RawUnreliableNoAckTransportConformanceControl : RawUnreliableConformanceControl, IRawUnreliableNoAckTransportConformanceControl
        {
            private readonly CheckPoint _beforeHandlerFactoryGate = new();
            private readonly CheckPoint _beforeHandlerStartedGate = new();

            public ICheckPointCtl BeforeHandlerFactoryGate => _beforeHandlerFactoryGate;

            public ICheckPointCtl BeforeHandlerStartedGate => _beforeHandlerStartedGate;

            public bool TryMakeReliable() => ((RawUnreliableNoAckTransport)_owner).TryMakeReliableForDebug();
        }

        /// <summary>
        /// Starts the underlying carrier. Called once during <see cref="TryStart"/>.
        /// </summary>
        protected abstract bool StartCarrier();

        /// <summary>
        /// Stops the underlying carrier. Called once during <see cref="OnStopped"/>.
        /// </summary>
        protected abstract void StopCarrier(StopReason reason);

        /// <summary>
        /// Commits an accepted message to the carrier for the given endpoint.
        /// Ownership of the message transfers to the carrier; it must release it
        /// on any non-<see cref="SendResult.Ok"/> result.
        /// </summary>
        protected abstract SendResult SendToCarrier(RawUnreliableNoAckEndpoint endpoint, UnionDataList message);

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

        private readonly object _initLock = new();
        private bool _initAttempted;
        private bool _initSucceeded;
        private IRawUnreliableHandler? _clientHandler;
        private Func<IEndPoint, IRawUnreliableHandler?>? _handlerFactory;

        protected bool TryInitialize(IRawUnreliableHandler? handler, Func<IEndPoint, IRawUnreliableHandler?>? factory)
        {
            lock (_initLock)
            {
                if (_initAttempted || HasStartBeenAttempted || !IsValid)
                    return false;
                _initAttempted = true;
                _clientHandler = handler;
                _handlerFactory = factory;
                _initSucceeded = true;
                return true;
            }
        }

        /// <summary>
        /// The client handler bound by <see cref="TryInitialize"/>. Null for a server.
        /// </summary>
        protected IRawUnreliableHandler? ClientHandler => _clientHandler;

        internal SerializedCallbackDispatcher? _dispatcher;
        internal RawUnreliableNoAckEndpoint? _clientEndpoint;
        private readonly Dictionary<IEndPoint, RawUnreliableNoAckEndpoint> _routes = new();
        private volatile bool _stopping;

        protected override bool TryStart()
        {
            if (!_initSucceeded)
                return false;

            _stopping = false;
            _dispatcher = new SerializedCallbackDispatcher(1000, Name + ".dispatcher", Log);
            if (!StartCarrier())
            {
                _dispatcher.Close();
                _dispatcher = null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Default is the server behavior. The client subclass overrides this to
        /// start its single endpoint.
        /// </summary>
        protected override void OnStarted()
        {
        }

        protected override void OnStopped(StopReason reason)
        {
            _stopping = true;

            StopCarrier(reason);

            var dispatcher = _dispatcher;
            if (dispatcher != null)
            {
                dispatcher.Enqueue(() => TeardownAllEndpoints(reason));
                dispatcher.Close();
            }
            else
            {
                TeardownAllEndpoints(reason);
            }
        }

        private void TeardownAllEndpoints(StopReason reason)
        {
            var endpoints = new List<RawUnreliableNoAckEndpoint>();
            if (_clientEndpoint != null) endpoints.Add(_clientEndpoint);
            endpoints.AddRange(_routes.Values);

            foreach (var ep in endpoints)
            {
                if (ep.TryBeginStop())
                {
                    ep.Conformance.BeforeEndpointStopStateTransitionGate.Hit();
                    ep.MarkInvalid();
                }
                TeardownEndpoint(ep, reason);
            }
        }

        protected RawUnreliableNoAckEndpoint CreateEndpoint(IRawUnreliableHandler handler, IEndPoint? remote)
        {
            var ep = new RawUnreliableNoAckEndpoint(this, handler, remote)
            {
                SendDelegate = SendToCarrier,
                StopDelegate = StopEndpoint
            };
            return ep;
        }

        private void TeardownEndpoint(RawUnreliableNoAckEndpoint ep, StopReason reason)
        {
            if (ep.TeardownDone) return;
            ep.MarkTeardownDone();

            ep.Conformance.BeforeHandlerStoppedGate.Hit();

            if (ep.OnStartedCompleted)
            {
                try { ep.Handler.OnStopped(reason); }
                catch (Exception e) { Log.wtf(e); }
            }

            if (ep.RemoteEndPoint != null &&
                _routes.TryGetValue(ep.RemoteEndPoint, out var current) &&
                ReferenceEquals(current, ep))
            {
                _routes.Remove(ep.RemoteEndPoint);
            }
        }

        internal bool StopEndpoint(RawUnreliableNoAckEndpoint ep, StopReason? reason)
        {
            if (!ep.TryBeginStop())
                return false;

            ep.Conformance.BeforeEndpointStopStateTransitionGate.Hit();
            ep.MarkInvalid();

            var resolvedReason = reason ?? new StopReasons.Unknown(Name);

            if (_dispatcher == null || !_dispatcher.Enqueue(() => TeardownEndpoint(ep, resolvedReason)))
                TeardownEndpoint(ep, resolvedReason);

            if (ReferenceEquals(ep, _clientEndpoint))
            {
                if (_dispatcher == null || !_dispatcher.Enqueue(() => Stop(resolvedReason)))
                    Stop(resolvedReason);
            }

            return true;
        }

        private void ProcessServerInbound(IEndPoint source, UnionDataList message)
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
            try { handler = _handlerFactory!(source); }
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
                _routes.Remove(source);
                ep.MarkInvalid();
                message.Release();
                return;
            }

            DeliverToEndpoint(ep, message);
        }

        private void DeliverToEndpoint(RawUnreliableNoAckEndpoint ep, UnionDataList message)
        {
            if (!ep.IsValid)
            {
                message.Release();
                return;
            }

            ep.Conformance.AfterReceivedGate.Hit();

            if (_stopping || !IsStarted || !ep.IsValid)
            {
                message.Release();
                return;
            }

            try { ep.Handler.OnReceived(message); }
            catch (Exception e) { Log.wtf(e); }
        }

        /// <summary>
        /// Inbound entry point for concrete carriers. Pass null as the source for
        /// a client transport; pass the source route for a server transport.
        /// </summary>
        protected void OnCarrierInbound(IEndPoint? source, UnionDataList message)
        {
            var dispatcher = _dispatcher;
            if (dispatcher == null)
            {
                message.Release();
                return;
            }

            if (source == null)
            {
                if (!dispatcher.Enqueue(() =>
                {
                    var ep = _clientEndpoint;
                    if (ep == null || _stopping || !IsStarted)
                    {
                        message.Release();
                        return;
                    }
                    DeliverToEndpoint(ep, message);
                }))
                {
                    message.Release();
                }
            }
            else
            {
                if (!dispatcher.Enqueue(() => ProcessServerInbound(source!, message)))
                {
                    message.Release();
                }
            }
        }
    }
}
