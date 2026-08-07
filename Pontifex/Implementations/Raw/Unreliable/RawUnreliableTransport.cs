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
    /// Base class for all RawUnreliable transports. Owns all endpoint,
    /// handler, lifecycle, routing, and conformance machinery shared by the
    /// Ack and NoAck contract variants. Concrete transports implement only the
    /// abstract carrier hooks and call <see cref="OnCarrierInbound"/>.
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
        /// The maximum single-message size in bytes supported by the transport.
        /// Implemented by concrete transports; it must match the carrier limit.
        /// </summary>
        public abstract int MessageMaxByteSize { get; }

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

        private readonly object _initLock = new();
        private bool _initAttempted;
        private bool _initSucceeded;
        private IRawUnreliableHandler? _clientHandler;
        private object? _handlerFactory;

        protected bool TryInitialize(IRawUnreliableHandler? handler, object? factory)
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
        /// The variant server handler factory stored by <see cref="TryInitialize"/>,
        /// or null for a client transport. Cast by the generic server base.
        /// </summary>
        protected object? HandlerFactory => _handlerFactory;

        /// <summary>
        /// The client handler bound by <see cref="TryInitialize"/>. Null for a server.
        /// </summary>
        protected IRawUnreliableHandler? ClientHandler => _clientHandler;

        internal SerializedCallbackQueue<RawUnreliableWorkItem>? _dispatcher;
        internal RawUnreliableEndpoint? _clientEndpoint;
        private readonly Dictionary<IEndPoint, RawUnreliableEndpoint> _routes = new();
        private volatile bool _stopping;

        private void DispatchWork(RawUnreliableWorkItem item)
        {
            try
            {
                switch (item.Kind)
                {
                    case RawUnreliableWorkKind.StartClientEndpoint:
                        StartClientEndpoint(item.Endpoint!);
                        break;
                    case RawUnreliableWorkKind.DeliverClient:
                        {
                            var ep = _clientEndpoint;
                            if (ep == null || _stopping || !IsStarted)
                            {
                                item.Message!.Release();
                                break;
                            }
                            DeliverToEndpoint(ep, item.Message!);
                        }
                        break;
                    case RawUnreliableWorkKind.ProcessServer:
                        ProcessServerInbound(item.Source!, item.Message!);
                        break;
                    case RawUnreliableWorkKind.TeardownEndpoint:
                        TeardownEndpoint(item.Endpoint!, item.Reason!);
                        break;
                    case RawUnreliableWorkKind.TeardownAll:
                        TeardownAllEndpoints(item.Reason!);
                        break;
                    case RawUnreliableWorkKind.Stop:
                        Stop(item.Reason);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                if (item.Kind is RawUnreliableWorkKind.DeliverClient or RawUnreliableWorkKind.ProcessServer)
                    item.Message!.Release();
            }
        }

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
                if (dispatcher == null || !dispatcher.Post(RawUnreliableWorkItem.Stop(new StopReasons.ExceptionFail(Name, e, "client handler.OnStarted threw"))))
                    Stop(new StopReasons.ExceptionFail(Name, e, "client handler.OnStarted threw"));
            }
        }

        protected override bool TryStart()
        {
            if (!_initSucceeded)
                return false;

            _stopping = false;
            _dispatcher = new SerializedCallbackQueue<RawUnreliableWorkItem>(1000, Name + ".dispatcher", DispatchWork, DispatchWork);
            if (!StartCarrier())
            {
                _dispatcher.Dispose();
                _dispatcher = null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Default is the server behavior. The client base class overrides this to
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
                if (!dispatcher.Post(RawUnreliableWorkItem.TeardownAll(reason)))
                    TeardownAllEndpoints(reason);
                dispatcher.Dispose();
            }
            else
            {
                TeardownAllEndpoints(reason);
            }
        }

        private void TeardownAllEndpoints(StopReason reason)
        {
            var endpoints = new List<RawUnreliableEndpoint>();
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

        protected RawUnreliableEndpoint CreateEndpoint(IRawUnreliableHandler handler, IEndPoint? remote)
        {
            var ep = new RawUnreliableEndpoint(this, handler, remote)
            {
                SendDelegate = SendToCarrier,
                StopDelegate = StopEndpoint
            };
            return ep;
        }

        private void TeardownEndpoint(RawUnreliableEndpoint ep, StopReason reason)
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

        internal bool StopEndpoint(RawUnreliableEndpoint ep, StopReason? reason)
        {
            if (!ep.TryBeginStop())
                return false;

            ep.Conformance.BeforeEndpointStopStateTransitionGate.Hit();
            ep.MarkInvalid();

            var resolvedReason = reason ?? new StopReasons.Unknown(Name);

            if (_dispatcher == null || !_dispatcher.Post(RawUnreliableWorkItem.TeardownEndpoint(ep, resolvedReason)))
                TeardownEndpoint(ep, resolvedReason);

            if (ReferenceEquals(ep, _clientEndpoint))
            {
                if (_dispatcher == null || !_dispatcher.Post(RawUnreliableWorkItem.Stop(resolvedReason)))
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
                _routes.Remove(source);
                ep.MarkInvalid();
                message.Release();
                return;
            }

            DeliverToEndpoint(ep, message);
        }

        private void DeliverToEndpoint(RawUnreliableEndpoint ep, UnionDataList message)
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
                if (!dispatcher.Post(RawUnreliableWorkItem.DeliverClient(message)))
                {
                    message.Release();
                }
            }
            else
            {
                if (!dispatcher.Post(RawUnreliableWorkItem.ProcessServer(source, message)))
                {
                    message.Release();
                }
            }
        }
    }
}
