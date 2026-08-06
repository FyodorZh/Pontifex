using System;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex.Raw.Unreliable.NoAck
{
    public abstract class RawUnreliableNoAckClientTransport : RawUnreliableNoAckTransport
    {
        protected RawUnreliableNoAckClientTransport(string typeName, ILogger logger, IMemoryRental memory,
            RawUnreliableNoAckTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        public bool Init(IRawUnreliableHandler handler)
        {
            if (handler == null!)
                throw new ArgumentNullException(nameof(handler));
            return TryInitialize(handler, null);
        }

        protected override void OnStarted()
        {
            var handler = ClientHandler;
            if (handler == null) return;

            var ep = CreateEndpoint(handler, ClientRemoteEndPoint);
            _clientEndpoint = ep;

            var dispatcher = _dispatcher;
            if (dispatcher == null) return;

            dispatcher.Post(() =>
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
                    dispatcher.Post(() => Stop(new StopReasons.ExceptionFail(Name, e, "client handler.OnStarted threw")));
                }
            });
        }
    }
}
