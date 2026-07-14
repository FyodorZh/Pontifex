using System;
using Actuarius.Memory;
using Pontifex.NoAck.Raw.Direct;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;
using Scriba;

namespace Pontifex.NoAck.Raw.Unreliable.Direct
{
    public sealed class NoAckRawUnreliableDirectClient : NoAckRawDirectClient, INoAckRawUnreliableClient
    {
        private volatile IDeliverySystem? _clientDeliverySystem;
        private volatile IDeliverySystem? _serverDeliverySystem;

        public override TransportType Type => TransportType.NoAckRawUnreliable;

        public NoAckRawUnreliableDirectClient(string serverName, ILogger logger, IMemoryRental memoryRental)
            : base(serverName, "direct-noack-raw-unreliable", logger, memoryRental)
        {
        }

        public void SetDeliverySystem(IDeliverySystem? clientDeliverySystem, IDeliverySystem? serverDeliverySystem)
        {
            _clientDeliverySystem = clientDeliverySystem;
            _serverDeliverySystem = serverDeliverySystem;
            _channel?.SetDeliverySystem(clientDeliverySystem, serverDeliverySystem);
        }

        protected override void OnChannelConnected(Channel channel)
        {
            base.OnChannelConnected(channel);
            channel.SetDeliverySystem(_clientDeliverySystem, _serverDeliverySystem);
        }

        protected override void OnBeforeChannelDisconnect(Channel channel)
        {
            channel.SetDeliverySystem(null, null);
        }

        public SendResult TrySend(UnionDataList message) => SendToServer(message);
    }
}
