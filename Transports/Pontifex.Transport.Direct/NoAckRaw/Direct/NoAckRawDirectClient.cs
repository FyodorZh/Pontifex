using System;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.NoAck.Raw.Direct
{
    public abstract class NoAckRawDirectClient : NoAckRawTransport
    {
        private readonly IEndPoint _serverEp;
        private readonly IEndPoint _clientEp;
        private SerializedCallbackQueue<UnionDataList>? _callbackQueue;
        protected volatile Channel? _channel;

        public event Action<UnionDataList>? OnReceived;

        public int MessageMaxByteSize => DirectInfo.MessageMaxByteSize;

        protected NoAckRawDirectClient(string serverName, string transportName, ILogger logger, IMemoryRental memoryRental)
            : base(transportName, logger, memoryRental)
        {
            _serverEp = new StringEndPoint(serverName);
            _clientEp = new GuidEndPoint(Guid.NewGuid());
        }

        protected sealed override bool TryStart()
        {
            _callbackQueue = new SerializedCallbackQueue<UnionDataList>(
                100,
                $"cli-cb-{_serverEp}",
                message =>
                {
                    var channel = _channel;
                    if (channel != null)
                    {
                        Conformance.BeforeTrySendStateDecisionGate.Hit();
                        channel.SendToServer(message);
                    }
                    else
                        message.Release();
                },
                message => message.Release());
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

        protected virtual void OnChannelConnected(Channel channel)
        {
            channel.ClientHandler = (message) =>
            {
                var handler = OnReceived;
                if (handler != null)
                {
                    try
                    {
                        handler(message);
                    }
                    catch
                    {
                        message.Release();
                        throw;
                    }
                }
                else
                {
                    message.Release();
                }
            };
        }

        protected override void OnStarted() { }

        protected override void OnStopped(StopReason reason)
        {
            var channel = _channel;
            if (channel != null)
            {
                _channel = null;
                OnBeforeChannelDisconnect(channel);
                DirectTransportManager.Instance.Disconnect(_serverEp, _clientEp);
            }
            _callbackQueue?.Dispose();
            _callbackQueue = null;
        }

        protected virtual void OnBeforeChannelDisconnect(Channel channel) { }

        protected SendResult SendToServer(UnionDataList message)
        {
            if (_channel == null)
            {
                message.Release();
                return SendResult.NotConnected;
            }

            _callbackQueue?.Post(message);
            return SendResult.Ok;
        }

        public override string ToString()
        {
            try { return $"direct-client[{_serverEp}]"; }
            catch (Exception) { return "direct-client[unknown]"; }
        }
    }
}
