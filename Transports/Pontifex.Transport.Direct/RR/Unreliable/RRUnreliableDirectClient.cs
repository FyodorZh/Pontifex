using System;
using Actuarius.Memory;
using Operarius;
using Pontifex.Endpoints;
using Pontifex.RR.Direct;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;
using Scriba;

namespace Pontifex.RR.Unreliable.Direct
{
    /// <summary>
    /// In-process RR unreliable client. The transport owns one fixed client endpoint and a lazy
    /// channel carrier.
    /// </summary>
    internal sealed class RRUnreliableDirectClient : RRUnreliableClientTransport
    {
        private readonly IEndPoint _clientEndPoint;
        private readonly Func<IDeliverySystem>? _clientToServerFactory;
        private readonly Func<IDeliverySystem>? _serverToClientFactory;

        private DirectRrChannelClient? _carrier;

        public RRUnreliableDirectClient(
            IEndPoint serverEndPoint,
            TimeSpan requestTtl,
            ILogger logger,
            IMemoryRental memory,
            Func<IDeliverySystem>? clientToServerFactory = null,
            Func<IDeliverySystem>? serverToClientFactory = null,
            IDateTimeProvider? timeProvider = null)
            : base(RRDirectInfo.UnreliableTransportName + "_rr", serverEndPoint, requestTtl, logger,
                memory, timeProvider)
        {
            _clientEndPoint = new GuidEndPoint(Guid.NewGuid());
            _clientToServerFactory = clientToServerFactory;
            _serverToClientFactory = serverToClientFactory;
        }

        public override int MessageMaxByteSize => RRDirectInfo.MessageMaxByteSize;

        /// <summary>
        /// Gets the carrier of this client, or <see langword="null"/> before the start or after the
        /// stop.
        /// </summary>
        internal DirectRrChannelClient? CurrentCarrier => _carrier;

        /// <summary>
        /// Gets the connected channel of this client, or <see langword="null"/> before the first send
        /// or after a loss.
        /// </summary>
        internal Channel? CurrentChannel => _carrier?.CurrentChannel;

        protected override bool StartCarrier()
        {
            _carrier = new DirectRrChannelClient(
                RRDirectInfo.UnreliableTransportName,
                ServerEndPoint,
                _clientEndPoint,
                Log,
                frame => OnFrameReceived(ServerEndPoint, frame),
                clientToServerFactory: _clientToServerFactory,
                serverToClientFactory: _serverToClientFactory);

            return true;
        }

        protected override void StopCarrier(StopReason reason)
        {
            DirectRrChannelClient? carrier = _carrier;
            _carrier = null;
            carrier?.Stop();
        }

        protected override SendResult SendFrame(UnionDataList frame)
        {
            DirectRrChannelClient? carrier = _carrier;
            if (carrier == null)
            {
                frame.Release();
                return SendResult.Ok;
            }

            return carrier.TrySend(frame, acceptWithoutPeer: true);
        }

        public override string ToString()
        {
            try
            {
                return $"direct-rr-client[{ServerEndPoint}]";
            }
            catch (Exception)
            {
                return "direct-rr-client[unknown]";
            }
        }
    }
}
