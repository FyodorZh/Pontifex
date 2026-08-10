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
    /// <see cref="AdmitSession"/>.
    /// </summary>
    public abstract class RawReliableServerTransport<TFactory> : RawReliableTransport
        where TFactory : class
    {
        private readonly object _sessionDriverLock = new();
        private ILogicDriver<INonPeriodicLogicDriverCtl>? _sessionDriver;

        protected RawReliableServerTransport(string typeName, ILogger logger, IMemoryRental memory, RawReliableTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        protected bool TryInitializeServer(TFactory factory)
        {
            return TryInitialize(null, factory);
        }

        protected override IEndPoint? ClientRemoteEndPoint => null;

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
