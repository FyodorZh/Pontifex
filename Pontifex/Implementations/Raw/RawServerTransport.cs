using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw
{
    /// <summary>
    /// Base class for all Raw server transports. Owns initialization storage,
    /// the serialized work dispatcher, the endpoint route table, the server
    /// handler factory, the server-side inbound path, and the carrier lifecycle
    /// and teardown scaffolding shared by the RawReliable and RawUnreliable
    /// server families. Concrete transports implement the carrier hooks
    /// (<see cref="StartCarrier"/>, <see cref="StopCarrier"/>) and the
    /// family-specific dispatch operations.
    /// </summary>
    public abstract class RawServerTransport : AnyTransport, IRawTransport
    {
        protected new IRawConformanceControl Conformance => (IRawConformanceControl)base.Conformance;

        protected RawServerTransport(string typeName, ILogger logger, IMemoryRental memory, RawConformanceControl conformanceControl)
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
        private object? _handlerFactory;

        protected bool TryInitializeServer(object factory)
        {
            lock (_initLock)
            {
                if (_initAttempted || HasStartBeenAttempted || !IsValid)
                    return false;
                _initAttempted = true;
                _handlerFactory = factory;
                _initSucceeded = true;
                return true;
            }
        }

        /// <summary>
        /// The family-specific server handler factory stored by
        /// <see cref="TryInitializeServer"/>.
        /// </summary>
        protected object? HandlerFactory => _handlerFactory;

        // ── Dispatcher ────────────────────────────────────────────────────

        internal SerializedCallbackQueue<RawWorkItem>? _dispatcher;
        internal readonly ConcurrentDictionary<IEndPoint, RawEndpoint> _routes = new();
        internal volatile bool _stopping;

        private void DispatchWork(RawWorkItem item)
        {
            try
            {
                switch (item.Kind)
                {
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
                if (item.Kind is RawWorkKind.ProcessServer)
                    item.Message!.Release();
            }
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
            var endpoints = new List<RawEndpoint>(_routes.Values);

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
        /// Shared endpoint stop driver. Returns true for the one call that
        /// begins stopping a valid endpoint. Posts the endpoint teardown.
        /// Carriers with per-connection serialization may invoke this from
        /// their own context.
        /// </summary>
        protected internal bool StopEndpoint(RawEndpoint ep, StopReason? reason)
        {
            if (!ep.TryBeginStop())
                return false;
            ep.HitEndpointStopStateTransitionGate();
            ep.MarkInvalid();

            var resolvedReason = reason ?? new StopReasons.Unknown(Name);

            ExecuteEndpointTeardown(ep, resolvedReason);

            return true;
        }

        /// <summary>
        /// Inbound entry point for concrete server carriers.
        /// </summary>
        protected void OnCarrierInbound(IEndPoint source, UnionDataList message)
        {
            var dispatcher = _dispatcher;
            if (dispatcher == null)
            {
                message.Release();
                return;
            }

            if (!dispatcher.Post(RawWorkItem.ProcessServer(source, message)))
            {
                message.Release();
            }
        }
    }
}
