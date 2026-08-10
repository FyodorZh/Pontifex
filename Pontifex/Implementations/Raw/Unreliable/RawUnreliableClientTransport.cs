using System;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Base class for all RawUnreliable client transports. Owns the client
    /// lifecycle: the client endpoint is created and started when the transport
    /// starts. Carries the RawUnreliable shared machinery on the client side:
    /// endpoint creation, client teardown, and the client start helper.
    /// </summary>
    public abstract class RawUnreliableClientTransport : RawClientTransport, IRawUnreliableClient, IRawUnreliableTransportDebug
    {
        protected new RawUnreliableTransportConformanceControl Conformance => (RawUnreliableTransportConformanceControl)base.Conformance;

        protected RawUnreliableClientTransport(string typeName, ILogger logger, IMemoryRental memory, RawUnreliableTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl ?? new RawUnreliableTransportConformanceControl())
        {
        }

        public bool Init(IRawUnreliableHandler handler)
        {
            if (handler == null!)
                throw new ArgumentNullException(nameof(handler));
            return TryInitialize(handler);
        }

        /// <summary>
        /// Commits an accepted message to the carrier for the given endpoint.
        /// Ownership of the message transfers to the carrier; it must release it
        /// on any non-<see cref="SendResult.Ok"/> result.
        /// </summary>
        protected abstract SendResult SendToCarrier(RawUnreliableEndpoint endpoint, UnionDataList message);

        /// <summary>
        /// The configured remote destination for the client endpoint.
        /// </summary>
        protected abstract IEndPoint? ClientRemoteEndPoint { get; }

        /// <summary>
        /// Enables transport-wide reliable debug mode before Start. Returns false
        /// when the implementation cannot provide the test mode.
        /// </summary>
        protected internal abstract bool TryMakeReliableForDebug();

        bool IRawUnreliableTransportDebug.TryMakeReliableForDebug() => TryMakeReliableForDebug();

        /// <summary>
        /// Creates the endpoint for the logical connection and wires its send
        /// and stop operations.
        /// </summary>
        protected RawUnreliableEndpoint CreateEndpoint(IRawUnreliableHandler handler, IEndPoint? remote)
        {
            var ep = new RawUnreliableEndpoint(this, handler, remote)
            {
                SendDelegate = SendToCarrier,
                StopDelegate = StopEndpoint
            };
            return ep;
        }

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

        /// <summary>
        /// Runs the client endpoint teardown: the handler's OnStopped fires when
        /// the endpoint was started.
        /// </summary>
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
        }

        protected override void OnStarted()
        {
            var handler = (IRawUnreliableHandler?)ClientHandler;
            if (handler == null) return;

            var ep = CreateEndpoint(handler, ClientRemoteEndPoint);
            _clientEndpoint = ep;

            var dispatcher = _dispatcher;
            if (dispatcher == null) return;

            if (!dispatcher.Post(RawWorkItem.StartClient()))
                StartClient();
        }

        protected override void StartClient()
        {
            StartClientEndpoint((RawUnreliableEndpoint)_clientEndpoint!);
        }
    }
}
