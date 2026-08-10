using System;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Base class for RawReliableAck server transports. Implements the server
    /// side of the ACK admission handshake: the acknowledger validates a new
    /// source's ACK data, the accepted session produces an ACK response, and
    /// the server OnConnected fires only after the response is committed for
    /// outbound delivery. Concrete transports supply the carrier send hooks.
    /// </summary>
    public abstract class RawReliableAckServerTransport : RawReliableServerTransport<IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>>, IRawReliableAckServer
    {
        protected RawReliableAckServerTransport(string typeName, ILogger logger, IMemoryRental memory, RawReliableTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        public bool Init(IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler> acknowledger)
        {
            if (acknowledger == null!)
                throw new ArgumentNullException(nameof(acknowledger));
            return TryInitializeServer(acknowledger);
        }

        protected override void AdmitSession(IEndPoint source, UnionDataList ackData)
        {
            Conformance.BeforeAcknowledgerGate.Hit();

            IRawReliableAckServerHandler? handler;
            try
            {
                handler = ((IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>)HandlerFactory!).TryAck(ackData);
            }
            catch (Exception e)
            {
                Log.wtf(e);
                ackData.Release();
                SendRejectionToClient(source);
                return;
            }

            if (handler == null)
            {
                SendRejectionToClient(source);
                return;
            }

            var ep = CreateEndpoint(handler, source);

            var ackResponse = Memory.CollectablePool.Acquire<UnionDataList>();
            try
            {
                handler.FillAckResponse(ackResponse);
            }
            catch (Exception e)
            {
                Log.wtf(e);
                ackResponse.Release();
                SendRejectionToClient(source);
                return;
            }

            if (ackResponse.GetDataSize() > MessageMaxByteSize)
            {
                ackResponse.Release();
                SendRejectionToClient(source);
                return;
            }

            _routes[source] = ep;
            ep.MarkValid();
            ep.MarkConnected();

            OnSessionAdmitted(ep);

            Conformance.BeforeAckResponseCommitGate.Hit();
            SendAckResponseToClient(source, ackResponse);

            ep.MarkOnStartedCompleted();
            Conformance.BeforeHandlerConnectedGate.Hit();

            try
            {
                handler.OnConnected(ep);
            }
            catch (Exception e)
            {
                Log.wtf(e);
                StopEndpoint(ep, new ExceptionFail(Name, e, "server handler.OnConnected threw"));
            }

            OnSessionDeliveryReady(ep);
        }

        /// <summary>
        /// Commits the accepted session's ACK response to the carrier for the
        /// connecting client. Ownership of <paramref name="ackResponse"/>
        /// transfers to the carrier.
        /// </summary>
        protected abstract void SendAckResponseToClient(IEndPoint source, UnionDataList ackResponse);

        /// <summary>
        /// Signals rejection of a connection attempt to the connecting client.
        /// The client-visible rejection mechanism is implementation-defined;
        /// the Direct transport sends an explicit rejection packet.
        /// </summary>
        protected abstract void SendRejectionToClient(IEndPoint source);
    }
}
