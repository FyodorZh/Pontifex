using System;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;
using Scriba;

namespace Pontifex.Raw.Unreliable.NoAck.Direct
{
    public sealed class RawUnreliableNoAckDirectClient : RawUnreliableNoAckClientTransport, IRawUnreliableNoAckClient
    {
        private readonly object _connectLock = new();
        private readonly IEndPoint _serverEp;
        private readonly IEndPoint _clientEp;
        private SerializedCallbackQueue<(RawUnreliableNoAckEndpoint, UnionDataList)>? _callbackQueue;
        private volatile Channel? _channel;
        private volatile IDeliverySystem _clientDeliverySystem = new PerfectDeliverySystem();
        private volatile IDeliverySystem _serverDeliverySystem = new PerfectDeliverySystem();

        private bool _askedForReliableDelivery;

        public int MessageMaxByteSize => DirectInfo.MessageMaxByteSize;

        public override TransportType Type => TransportType.RawUnreliableNoAck;

        public RawUnreliableNoAckDirectClient(string serverName, ILogger logger, IMemoryRental memoryRental)
            : base("direct-noack-raw-unreliable", logger, memoryRental)
        {
            _serverEp = new StringEndPoint(serverName);
            _clientEp = new GuidEndPoint(Guid.NewGuid());
        }

        public void SetDeliverySystem(IDeliverySystem clientDeliverySystem, IDeliverySystem serverDeliverySystem)
        {
            _clientDeliverySystem = clientDeliverySystem;
            _serverDeliverySystem = serverDeliverySystem;
            _channel?.SetClientDeliverySystem(_clientDeliverySystem);
            _channel?.SetServerDeliverySystem(_serverDeliverySystem);
        }

        protected override IEndPoint? ClientRemoteEndPoint => _serverEp;

        protected override bool StartCarrier()
        {
            _callbackQueue = new SerializedCallbackQueue<(RawUnreliableNoAckEndpoint, UnionDataList)>(
                100,
                $"cli-cb-{_serverEp}",
                pair =>
                {
                    var (endpoint, message) = pair;
                    var channel = _channel;
                    if (channel == null)
                    {
                        message.Release();
                        return;
                    }
                    endpoint.Conformance.BeforeSendCommitGate.Hit();
                    channel.SendToServer(message);
                    endpoint.Conformance.AfterSendCommitGate.Hit();
                },
                pair => pair.Item2.Release());
            _callbackQueue.ExceptionHandler += ex => Log.wtf(ex);

            return true;
        }

        protected override void StopCarrier(StopReason reason)
        {
            var channel = _channel;
            if (channel != null)
            {
                _channel = null;
                channel.SetClientDeliverySystem(new PerfectDeliverySystem());
                channel.SetServerDeliverySystem(new PerfectDeliverySystem());
                DirectTransportManager.Instance.Disconnect(_serverEp, _clientEp);
            }
            _callbackQueue?.Dispose();
            _callbackQueue = null;
        }

        protected override SendResult SendToCarrier(RawUnreliableNoAckEndpoint endpoint, UnionDataList message)
        {
            var channel = _channel;
            if (channel == null)
            {
                lock (_connectLock)
                {
                    channel = _channel;
                    if (channel == null)
                    {
                        channel = DirectTransportManager.Instance.Connect(_serverEp, _clientEp);
                        if (channel == null)
                        {
                            message.Release();
                            return SendResult.Error;
                        }
                        OnChannelConnected(channel);
                        _channel = channel;
                    }
                }
            }

            if (message.GetDataSize() > DirectInfo.MessageMaxByteSize)
            {
                message.Release();
                return SendResult.MessageTooBig;
            }

            if (_callbackQueue?.Post((endpoint, message)) ?? false)
            {
                return SendResult.Ok;
            }
            message.Release();
            return SendResult.Error;
        }

        private void OnChannelConnected(Channel channel)
        {
            channel.ClientHandler = message => OnCarrierInbound(null, message);

            if (_askedForReliableDelivery)
            {
                channel.SetClientDeliverySystem(new PerfectDeliverySystem());
                channel.SetServerDeliverySystem(new PerfectDeliverySystem());
            }
            else
            {
                channel.SetClientDeliverySystem(_clientDeliverySystem);
                channel.SetServerDeliverySystem(_serverDeliverySystem);
            }
        }

        public override string ToString()
        {
            try { return $"direct-client[{_serverEp}]"; }
            catch (Exception) { return "direct-client[unknown]"; }
        }

        protected override bool TryMakeReliableForDebug()
        {
            _askedForReliableDelivery = true;
            return IsStarted == false;
        }
    }
}
