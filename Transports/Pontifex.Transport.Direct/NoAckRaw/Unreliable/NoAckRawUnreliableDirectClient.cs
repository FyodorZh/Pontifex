using System;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;
using Scriba;

namespace Pontifex.NoAck.Raw.Unreliable.Direct
{
    public sealed class NoAckRawUnreliableDirectClient : NoAckRawUnreliableClientTransport, INoAckRawUnreliableClient
    {
        private readonly IEndPoint _serverEp;
        private readonly IEndPoint _clientEp;
        private SerializedCallbackQueue<UnionDataList>? _callbackQueue;
        private volatile Channel? _channel;
        private volatile IDeliverySystem _clientDeliverySystem = new PerfectDeliverySystem();
        private volatile IDeliverySystem _serverDeliverySystem = new PerfectDeliverySystem();

        private bool _askedForReliableDelivery;

        public event Action<UnionDataList>? OnReceived;

        public int MessageMaxByteSize => DirectInfo.MessageMaxByteSize;

        public override TransportType Type => TransportType.NoAckRawUnreliable;

        public NoAckRawUnreliableDirectClient(string serverName, ILogger logger, IMemoryRental memoryRental)
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

        protected override bool TryStart()
        {
            _callbackQueue = new SerializedCallbackQueue<UnionDataList>(
                100,
                $"cli-cb-{_serverEp}",
                message =>
                {
                    var channel = _channel;
                    if (channel != null)
                    {
                        Conformance.BeforeSendCommitGate.Hit();
                        channel.SendToServer(message);
                        Conformance.AfterSendCommitGate.Hit();
                    }
                    else
                        message.Release();
                },
                message => message.Release());
            _callbackQueue.ExceptionHandler += ex => Log.wtf(ex);
            
            var channel = DirectTransportManager.Instance.Connect(_serverEp, _clientEp);
            if (channel == null)
            {
                Log.e("Failed to connect to server '{0}'", _serverEp);
                _callbackQueue.Dispose();
                _callbackQueue = null;
                return false;
            }

            OnChannelConnected(channel);
            _channel = channel;
            return true;
        }

        private void OnChannelConnected(Channel channel)
        {
            channel.ClientHandler = (message) =>
            {
                var handler = OnReceived;
                if (handler != null)
                {
                    try
                    {
                        Conformance.AfterReceivedGate.Hit();
                        handler(message);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }
                }
                else
                {
                    message.Release();
                }
            };

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

        protected override void OnStarted() { }

        protected override void OnStopped(StopReason reason)
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

        private SendResult SendToServer(UnionDataList message)
        {
            if (_channel == null)
            {
                message?.Release();
                return SendResult.Error;
            }

            if (message == null!)
            {
                return SendResult.InvalidMessage;
            }
            
            if (message.GetDataSize() > DirectInfo.MessageMaxByteSize)
            {
                message.Release();
                return SendResult.MessageTooBig;
            }

            if (_callbackQueue?.Post(message) ?? false)
            {
                return SendResult.Ok;
            }
            message.Release();
            return SendResult.Error;
        }

        public SendResult TrySend(UnionDataList message) => SendToServer(message);

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
