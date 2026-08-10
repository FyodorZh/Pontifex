using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw
{
    /// <summary>
    /// Base class for all Raw transports. Owns initialization storage, the
    /// serialized work dispatcher, endpoint routing, carrier lifecycle hooks,
    /// and teardown scaffolding shared by the RawReliable and RawUnreliable
    /// families. Concrete transports implement the carrier hooks
    /// (<see cref="StartCarrier"/>, <see cref="StopCarrier"/>) and the
    /// family-specific dispatch operations.
    /// </summary>
    public abstract class RawTransport : AnyTransport
    {
        protected new IRawConformanceControl Conformance => (IRawConformanceControl)base.Conformance;
        
        protected RawTransport(string typeName, ILogger logger, IMemoryRental memory, RawConformanceControl conformanceControl) 
            : base(typeName, logger, memory, conformanceControl)
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

        // ── Initialization ────────────────────────────────────────────────

        private readonly object _initLock = new();
        private bool _initAttempted;
        private bool _initSucceeded;
        private IRawHandler? _clientHandler;
        private object? _handlerFactory;

        protected bool TryInitialize(IRawHandler? handler, object? factory)
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
        /// The family-specific server handler factory stored by
        /// <see cref="TryInitialize"/>, or null for a client transport.
        /// </summary>
        protected object? HandlerFactory => _handlerFactory;

        /// <summary>
        /// The client handler bound by <see cref="TryInitialize"/>. Null for a server.
        /// </summary>
        protected IRawHandler? ClientHandler => _clientHandler;

        // ── Dispatcher ────────────────────────────────────────────────────

        internal SerializedCallbackQueue<RawWorkItem>? _dispatcher;
        internal readonly ConcurrentDictionary<IEndPoint, RawEndpoint> _routes = new();
        internal RawEndpoint? _clientEndpoint;
        internal volatile bool _stopping;

        private void DispatchWork(RawWorkItem item)
        {
            try
            {
                switch (item.Kind)
                {
                    case RawWorkKind.StartClient:
                        StartClient();
                        break;
                    case RawWorkKind.DeliverClient:
                        DeliverClientInbound(item.Message!);
                        break;
                    case RawWorkKind.ProcessServer:
                        ProcessServerInbound(item.Source!, item.Message!);
                        break;
                    case RawWorkKind.TeardownEndpoint:
                        TeardownEndpoint(item.Endpoint!, item.Reason!);
                        break;
                    case RawWorkKind.TeardownAll:
                        TeardownAllEndpoints(item.Reason!);
                        break;
                    case RawWorkKind.Stop:
                        Stop(item.Reason);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                if (item.Kind is RawWorkKind.DeliverClient or RawWorkKind.ProcessServer)
                    item.Message!.Release();
            }
        }

        /// <summary>
        /// Family hook invoked on the dispatcher thread when a client transport
        /// starts and must begin its client-side lifecycle (start the endpoint,
        /// or begin the connection handshake).
        /// </summary>
        protected virtual void StartClient() { }

        /// <summary>
        /// Family hook invoked on the dispatcher thread for a client inbound
        /// message. The default delivers to the client endpoint.
        /// </summary>
        protected virtual void DeliverClientInbound(UnionDataList message)
        {
            var ep = _clientEndpoint;
            if (ep == null || _stopping || !IsStarted)
            {
                message.Release();
                return;
            }
            DeliverToEndpoint(ep, message);
        }

        /// <summary>
        /// Family hook invoked on the dispatcher thread for a server inbound
        /// message addressed by its source route.
        /// </summary>
        protected abstract void ProcessServerInbound(IEndPoint source, UnionDataList message);

        /// <summary>
        /// Family-specific teardown of one endpoint. Runs on the dispatcher
        /// thread or inline when no dispatcher is available.
        /// </summary>
        protected abstract void TeardownEndpoint(RawEndpoint ep, StopReason reason);

        /// <summary>
        /// Family-specific delivery of one accepted inbound message to an
        /// endpoint. The default releases the message on failure and logs
        /// handler exceptions.
        /// </summary>
        protected virtual void DeliverToEndpoint(RawEndpoint ep, UnionDataList message)
        {
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

            try { ep.RawHandler.OnReceived(message); }
            catch (Exception e) { Log.wtf(e); }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────

        /// <summary>
        /// Capacity of the serialized work dispatcher queue. Concrete transports
        /// serving many concurrent connections may raise this to avoid dropping
        /// inbound work under load.
        /// </summary>
        protected virtual int DispatcherCapacity => 1000;

        protected override bool TryStart()
        {
            if (!_initSucceeded)
                return false;

            _stopping = false;
            _dispatcher = new SerializedCallbackQueue<RawWorkItem>(DispatcherCapacity, Name + ".dispatcher", DispatchWork, DispatchWork);
            if (!StartCarrier())
            {
                _dispatcher.Dispose();
                _dispatcher = null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Default is the server behavior. Client base classes override this to
        /// start their client lifecycle.
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
                if (!dispatcher.Post(RawWorkItem.TeardownAll(reason)))
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
            var endpoints = new List<RawEndpoint>();
            if (_clientEndpoint != null) endpoints.Add(_clientEndpoint);
            endpoints.AddRange(_routes.Values);

            foreach (var ep in endpoints)
            {
                if (ep.TryBeginStop())
                {
                    ep.HitEndpointStopStateTransitionGate();
                    ep.MarkInvalid();
                }
                TeardownEndpoint(ep, reason);
            }
        }

        /// <summary>
        /// Runs one endpoint's teardown. The default schedules it on the
        /// transport's serialized dispatcher so session callbacks stay
        /// serialized. Carriers with per-connection serialization (for example
        /// a TCP connection driven by a single receive loop) may override this
        /// to execute teardown in the connection's own serialized context,
        /// ensuring <c>OnDisconnected</c> cannot race <c>OnReceived</c>.
        /// </summary>
        protected virtual void ExecuteEndpointTeardown(RawEndpoint ep, StopReason reason)
        {
            if (_dispatcher == null || !_dispatcher.Post(RawWorkItem.TeardownEndpoint(ep, reason)))
                TeardownEndpoint(ep, reason);
        }

        /// <summary>
        /// Stops the owning transport on the dispatcher thread, or synchronously
        /// when no dispatcher is available. Used by carriers to react to a peer
        /// disconnect from a non-dispatcher context.
        /// </summary>
        protected void PostStop(StopReason reason)
        {
            if (_dispatcher == null || !_dispatcher.Post(RawWorkItem.Stop(reason)))
                Stop(reason);
        }

        /// <summary>
        /// Shared endpoint stop/disconnect driver. Returns true for the one call
        /// that begins stopping a valid endpoint. Posts the endpoint teardown and,
        /// for the client endpoint, the owning transport stop. Carriers with
        /// per-connection serialization may invoke this from their own context.
        /// </summary>
        protected internal bool StopEndpoint(RawEndpoint ep, StopReason? reason)
        {
            if (!ep.TryBeginStop())
                return false;
            ep.HitEndpointStopStateTransitionGate();
            ep.MarkInvalid();

            var resolvedReason = reason ?? new StopReasons.Unknown(Name);

            ExecuteEndpointTeardown(ep, resolvedReason);

            if (ReferenceEquals(ep, _clientEndpoint))
            {
                if (_dispatcher == null || !_dispatcher.Post(RawWorkItem.Stop(resolvedReason)))
                    Stop(resolvedReason);
            }

            return true;
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
                if (!dispatcher.Post(RawWorkItem.DeliverClient(message)))
                {
                    message.Release();
                }
            }
            else
            {
                if (!dispatcher.Post(RawWorkItem.ProcessServer(source, message)))
                {
                    message.Release();
                }
            }
        }
    }
}
