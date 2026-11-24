using System;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.RR.Direct;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.RR.Reliable.Direct
{
    /// <summary>
    /// In-process RR reliable server. The reliable core's dispatch queue serializes
    /// <c>OnRequested</c> and is created before the channel registry entry.
    /// </summary>
    internal sealed class RRReliableDirectServer : RRReliableServerTransport
    {
        private readonly IEndPoint _serverEndPoint;

        private DirectRrChannelServer? _carrier;

        public RRReliableDirectServer(
            string serverId,
            TimeSpan responseTtl,
            ILogger logger,
            IMemoryRental memory,
            IRRReliableTimerFactory? timerFactory = null)
            : base(RRDirectInfo.ReliableTransportName + "_rr", responseTtl, logger, memory,
                timerFactory)
        {
            if (serverId == null)
            {
                throw new ArgumentNullException(nameof(serverId));
            }

            _serverEndPoint = new StringEndPoint(serverId);
            _carrier = new DirectRrChannelServer(RRDirectInfo.ReliableTransportName, serverId, Log, OnInbound, OnSessionClosed);
        }

        public override int MessageMaxByteSize => RRDirectInfo.MessageMaxByteSize;

        /// <summary>
        /// Gets the carrier of this server.
        /// </summary>
        internal DirectRrChannelServer? CurrentCarrier => _carrier;

        protected override bool StartCarrier()
        {
            DirectRrChannelServer? carrier = _carrier;
            if (carrier == null)
            {
                return false;
            }

            // The dispatch queue must exist before the registry can deliver a request.
            if (!StartDispatch())
            {
                return false;
            }

            if (!carrier.TryStart())
            {
                StopDispatch();
                return false;
            }

            return true;
        }

        protected override void StopCarrier(StopReason reason)
        {
            DirectRrChannelServer? carrier = _carrier;
            _carrier = null;
            carrier?.Stop();
            StopDispatch();
        }

        protected override SendResult SendFrame(IEndPoint destination, UnionDataList frame)
        {
            DirectRrChannelServer? carrier = _carrier;
            if (carrier == null)
            {
                frame.Release();
                return SendResult.Error;
            }

            return carrier.TrySend(destination, frame);
        }

        private void OnInbound(IEndPoint source, UnionDataList frame)
        {
            OnFrameReceived(source, frame);
        }

        public override string ToString()
        {
            try
            {
                return $"direct-rr-reliable-server[{_serverEndPoint}]";
            }
            catch (Exception)
            {
                return "direct-rr-reliable-server[unknown]";
            }
        }
    }
}
