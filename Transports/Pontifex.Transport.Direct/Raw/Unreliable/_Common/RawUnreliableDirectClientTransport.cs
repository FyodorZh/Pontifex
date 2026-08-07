using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;
using Scriba;

namespace Pontifex.Raw.Unreliable.Direct
{
    /// <summary>
    /// Base class for all RawUnreliable Direct client transports. Owns the
    /// in-process channel, delivery systems, and callback queue shared by the
    /// Ack and NoAck contract variants.
    /// </summary>
    public abstract class RawUnreliableDirectClientTransport : RawUnreliableClientTransport
    {
        private readonly object _connectLock = new();
        private readonly IEndPoint _serverEp;
        private readonly IEndPoint _clientEp;
        private SerializedCallbackQueue<(RawUnreliableEndpoint, UnionDataList)>? _callbackQueue;
        private volatile Channel? _channel;
        private volatile IDeliverySystem _clientDeliverySystem = new PerfectDeliverySystem();
        private volatile IDeliverySystem _serverDeliverySystem = new PerfectDeliverySystem();

        private bool _askedForReliableDelivery;

        protected abstract int QueueCapacity { get; }

        protected RawUnreliableDirectClientTransport(string typeName, string serverName, ILogger logger, IMemoryRental memoryRental)
            : base(typeName, logger, memoryRental)
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
            _callbackQueue = new SerializedCallbackQueue<(RawUnreliableEndpoint, UnionDataList)>(
                QueueCapacity,
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

        protected override SendResult SendToCarrier(RawUnreliableEndpoint endpoint, UnionDataList message)
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

            if (message.GetDataSize() > MessageMaxByteSize)
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
