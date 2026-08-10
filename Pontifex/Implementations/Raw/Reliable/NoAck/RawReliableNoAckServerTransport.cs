using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.NoAck
{
    /// <summary>
    /// Base class for RawReliableNoAck server transports. Admits a new source
    /// through a source-route handler factory; the triggering message is
    /// delivered to the new session. Symmetric with
    /// <see cref="Pontifex.Raw.Unreliable.NoAck.RawUnreliableNoAckServerTransport"/>
    /// but under the RawReliable contract.
    /// </summary>
    public abstract class RawReliableNoAckServerTransport : RawReliableServerTransport<Func<IEndPoint, IRawReliableHandler?>>
    {
        protected RawReliableNoAckServerTransport(string typeName, ILogger logger, IMemoryRental memory, RawReliableTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        public bool Init(Func<IEndPoint, IRawReliableHandler?> handlerFactory)
        {
            if (handlerFactory == null!)
                throw new ArgumentNullException(nameof(handlerFactory));
            return TryInitializeServer(handlerFactory);
        }

        protected override void AdmitSession(IEndPoint source, UnionDataList message)
        {
            IRawReliableHandler? handler;
            try
            {
                handler = ((Func<IEndPoint, IRawReliableHandler?>)HandlerFactory!)(source);
            }
            catch (Exception e)
            {
                Log.wtf(e);
                message.Release();
                return;
            }

            if (handler == null)
            {
                message.Release();
                return;
            }

            var ep = CreateEndpoint(handler, source);
            _routes[source] = ep;
            ep.MarkValid();
            ep.MarkOnStartedCompleted();

            OnSessionAdmitted(ep);
            OnSessionDeliveryReady(ep);

            Conformance.BeforeHandlerConnectedGate.Hit();

            DeliverToEndpoint(ep, message);
        }
    }
}
