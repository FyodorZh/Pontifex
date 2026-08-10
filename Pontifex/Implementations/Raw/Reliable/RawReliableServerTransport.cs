using System;
using Actuarius.Memory;
using Operarius;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Base class for all RawReliable server transports. The generic parameter
    /// is the variant session-admission mechanism: Ack servers supply an
    /// acknowledger, NoAck servers a source handler factory. A server route
    /// delivers to an existing session; a new source is admitted by the variant
    /// <see cref="AdmitSession"/>. Carries the RawReliable shared machinery on
    /// the server side: endpoint creation, delivery, inbound injection, session
    /// teardown, and the session routing hooks.
    /// </summary>
    public abstract class RawReliableServerTransport<TFactory> : RawServerTransport
        where TFactory : class
    {
        protected new RawReliableTransportConformanceControl Conformance => (RawReliableTransportConformanceControl)base.Conformance;

        private readonly object _sessionDriverLock = new();
        private ILogicDriver<INonPeriodicLogicDriverCtl>? _sessionDriver;

        protected RawReliableServerTransport(string typeName, ILogger logger, IMemoryRental memory, RawReliableTransportConformanceControl conformanceControl)
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        protected bool TryInitializeServer(TFactory factory)
        {
            return base.TryInitializeServer(factory);
        }

        /// <summary>
        /// A server transport has no configured remote destination.
        /// </summary>
        protected virtual IEndPoint? ClientRemoteEndPoint => null;

        /// <summary>
        /// Commits an accepted message to the carrier for the given endpoint.
        /// Ownership of the message transfers to the carrier; it must release it
        /// on any non-<see cref="SendResult.Ok"/> result.
        /// </summary>
        protected abstract SendResult SendToCarrier(RawReliableEndpoint endpoint, UnionDataList message);

        /// <summary>
        /// Creates the endpoint for a session and wires its send, disconnect,
        /// and inbound-injection operations. Carriers may override to return a
        /// <see cref="RawReliableEndpoint"/> subclass that exposes
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
        /// Injects an inbound message into a specific endpoint's receive path as
        /// if it had arrived from the carrier. Used by the endpoint conformance
        /// control's <c>InjectInboundData</c>. Malformed or oversized data is
        /// discarded and the session is disconnected without stopping the
        /// transport.
        /// </summary>
        internal void InjectInboundToEndpoint(RawReliableEndpoint ep, UnionDataList data)
        {
            if (data == null!)
            {
                Log.e("RawReliable server transport: injected inbound data is null");
                StopEndpoint(ep, new StopReasons.TextFail(Name, "Injected inbound data is null"));
                return;
            }

            if (data.GetDataSize() > MessageMaxByteSize)
            {
                data.Release();
                Log.e("RawReliable server transport: injected oversized inbound data");
                StopEndpoint(ep, new StopReasons.TextFail(Name, "Injected inbound data exceeds MessageMaxByteSize"));
                return;
            }

            if (ep.RemoteEndPoint == null)
            {
                data.Release();
                Log.e("RawReliable server transport: injected inbound data has no source route");
                StopEndpoint(ep, new StopReasons.TextFail(Name, "Injected inbound data has no source route"));
                return;
            }

            OnCarrierInbound(ep.RemoteEndPoint, data);
        }

        /// <summary>
        /// Runs one server session's teardown: the handler's OnDisconnected
        /// fires when the session connected, the route is removed, and the
        /// session-end hook releases the peer connection.
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

            if (ep.RemoteEndPoint != null &&
                _routes.TryGetValue(ep.RemoteEndPoint, out var current) &&
                ReferenceEquals(current, ep))
            {
                _routes.TryRemove(ep.RemoteEndPoint, out _);
            }

            if (ep.RemoteEndPoint != null)
            {
                OnServerSessionEnded(ep.RemoteEndPoint);
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
        /// Invoked after a server session endpoint is torn down, with the
        /// session's source route. Carriers use this to release the peer
        /// connection so the client observes the disconnect.
        /// </summary>
        protected virtual void OnServerSessionEnded(IEndPoint source)
        {
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

            AdmitSession(source, message);
        }

        /// <summary>
        /// Variant hook invoked on the dispatcher thread for the first inbound
        /// message from a new source route. The message ownership transfers to
        /// this method; it must be released or delivered exactly once.
        /// </summary>
        protected abstract void AdmitSession(IEndPoint source, UnionDataList message);

        /// <summary>
        /// Creates the driver used to schedule per-session processing (outbound
        /// commit, teardown, control work). The transport controls
        /// processing-time allocation by choosing the driver and the
        /// per-session scheduling granularity. Returns null to keep all session
        /// processing on the transport's serialized dispatcher (the Direct
        /// transport's behavior).
        /// </summary>
        protected virtual ILogicDriver<INonPeriodicLogicDriverCtl>? CreateSessionDriver() => null;

        /// <summary>
        /// The lazily-created session driver for this server transport, or null
        /// when <see cref="CreateSessionDriver"/> returns null. Finished on
        /// transport stop.
        /// </summary>
        protected ILogicDriver<INonPeriodicLogicDriverCtl>? SessionDriver
        {
            get
            {
                lock (_sessionDriverLock)
                {
                    if (_sessionDriver == null)
                    {
                        var driver = CreateSessionDriver();
                        if (driver != null)
                        {
                            driver.ErrorStream += ex => Log.wtf(ex);
                        }
                        _sessionDriver = driver;
                    }
                    return _sessionDriver;
                }
            }
        }

        /// <summary>
        /// Invoked after a new server session endpoint is created and wired to
        /// its carrier, before the session's OnConnected. A transport that
        /// supplies a session driver registers the session's scheduled logic
        /// here. The default does nothing.
        /// </summary>
        protected virtual void OnSessionAdmitted(RawReliableEndpoint endpoint)
        {
        }

        /// <summary>
        /// Invoked after the session's <c>OnConnected</c> has completed, when
        /// the session is ready to receive regular inbound messages. A carrier
        /// that delivers inbound directly on its own threads buffers messages
        /// until this point so that regular messages are never exposed to the
        /// application handler before its <c>OnConnected</c> ran.
        /// </summary>
        protected virtual void OnSessionDeliveryReady(RawReliableEndpoint endpoint)
        {
        }

        protected override void OnStopped(StopReason reason)
        {
            base.OnStopped(reason);
            ILogicDriver<INonPeriodicLogicDriverCtl>? driver;
            lock (_sessionDriverLock)
            {
                driver = _sessionDriver;
                _sessionDriver = null;
            }
            driver?.Finish();
        }
    }
}
