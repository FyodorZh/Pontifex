using System;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Base class for RawReliableAck client transports. Implements the client
    /// side of the ACK admission handshake: builds the ACK data, sends it as
    /// the first outbound message, and completes the logical connection when a
    /// valid ACK response arrives. Concrete transports supply the carrier send
    /// hook and the handshake response decoding.
    /// </summary>
    public abstract class RawReliableAckClientTransport : RawReliableClientTransport, IRawReliableAckClient
    {
        protected RawReliableAckClientTransport(string typeName, ILogger logger, IMemoryRental memory, RawReliableAckTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl ?? new RawReliableAckTransportConformanceControl())
        {
        }

        public bool Init(IRawReliableAckClientHandler handler)
        {
            if (handler == null!)
                throw new ArgumentNullException(nameof(handler));
            return TryInitialize(handler);
        }

        protected override void BeginHandshake()
        {
            var handler = (IRawReliableAckClientHandler)ClientHandler!;

            var ackData = Memory.CollectablePool.Acquire<UnionDataList>();
            try
            {
                handler.FillAckData(ackData);
            }
            catch (Exception e)
            {
                ackData.Release();
                Log.wtf(e);
                FailEstablishment(new ExceptionFail(Name, e, "client handler.FillAckData threw"));
                return;
            }

            if (ackData.GetDataSize() > MessageMaxByteSize)
            {
                ackData.Release();
                FailEstablishment(new TextFail(Name, "ACK data exceeds MessageMaxByteSize"));
                return;
            }

            SendHandshakeToCarrier(ackData);
        }

        /// <summary>
        /// Commits the ACK data to the carrier as the first outbound message.
        /// Ownership of <paramref name="ackData"/> transfers to the carrier.
        /// </summary>
        protected abstract void SendHandshakeToCarrier(UnionDataList ackData);

        /// <summary>
        /// Completes the logical connection after a successful handshake.
        /// The ACK response ownership transfers to the client handler.
        /// </summary>
        protected void CompleteConnect(UnionDataList ackResponse)
        {
            var handler = (IRawReliableAckClientHandler)ClientHandler!;
            var ep = (RawReliableEndpoint)_clientEndpoint!;

            ep.MarkConnected();
            MarkConnected();
            ep.MarkOnStartedCompleted();

            Conformance.BeforeHandlerConnectedGate.Hit();

            try
            {
                handler.OnConnected(ep, ackResponse);
            }
            catch (Exception e)
            {
                Log.wtf(e);
                StopEndpoint(ep, new ExceptionFail(Name, e, "client handler.OnConnected threw"));
            }
        }

        /// <summary>
        /// Fails the logical connection establishment: the client receives
        /// OnStopped without OnConnected or OnDisconnected.
        /// </summary>
        protected void FailEstablishment(StopReason reason)
        {
            MarkHandshakeFailed();
            Stop(reason);
        }
    }
}
