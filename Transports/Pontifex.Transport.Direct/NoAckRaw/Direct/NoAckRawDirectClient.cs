using System;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.NoAck.Raw.Direct
{
    public abstract class NoAckRawDirectClient : AnyTransport
    {
        private readonly IEndPoint _serverEp;
        private readonly IEndPoint _clientEp;
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
            var channel = DirectTransportManager.Instance.Connect(_serverEp, _clientEp);
            if (channel == null)
            {
                Log.e("Failed to connect to server '{0}'", _serverEp);
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
                    try { handler(message); }
                    catch (Exception e) { FailException("OnReceived", e); }
                }
                else { message.Release(); }
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
        }

        protected virtual void OnBeforeChannelDisconnect(Channel channel) { }

        protected SendResult SendToServer(UnionDataList message)
        {
            var channel = _channel;
            if (channel == null)
            {
                message.Release();
                return SendResult.NotConnected;
            }
            return channel.SendToServer(message);
        }

        public override string ToString()
        {
            try { return $"direct-client[{_serverEp}]"; }
            catch (Exception) { return "direct-client[unknown]"; }
        }
    }
}
