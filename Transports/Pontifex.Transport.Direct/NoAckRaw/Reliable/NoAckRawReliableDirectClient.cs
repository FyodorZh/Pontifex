using System;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Transports.Core;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.NoAck.Raw.Reliable.Direct
{
    public sealed class NoAckRawReliableDirectClient : AbstractTransport, INoAckRawReliableClient
    {
        private readonly IEndPoint _serverEp;
        private readonly IEndPoint _clientEp;

        private Channel? _channel;
        
        public override TransportType Type => TransportType.NoAckRawReliable;

        public NoAckRawReliableDirectClient(string serverName, ILogger logger, IMemoryRental memoryRental)
            : base(DirectInfo.TransportName, logger, memoryRental)
        {
            _serverEp = new StringEndPoint(serverName);
            _clientEp = new GuidEndPoint(Guid.NewGuid());
        }

        public event Action<UnionDataList>? OnReceived;

        public int MessageMaxByteSize => DirectInfo.MessageMaxByteSize;

        protected override bool TryStart()
        {
            var channel = DirectTransportManager.Instance.Connect(_serverEp, _clientEp);
            if (channel == null)
            {
                Log.e("Failed to connect to server '{0}'", _serverEp);
                return false;
            }

            channel.ClientHandler = (message) =>
            {
                var handler = OnReceived;
                if (handler != null)
                {
                    try
                    {
                        handler(message);
                    }
                    catch (Exception e)
                    {
                        FailException("OnReceived", e);
                    }
                }
                else
                {
                    message.Release();
                }
            };

            _channel = channel;
            return true;
        }

        protected override void OnStarted()
        {
        }

        protected override void OnStopped(StopReason reason)
        {
            var channel = _channel;
            if (channel != null)
            {
                _channel = null;
                DirectTransportManager.Instance.Disconnect(_serverEp, _clientEp);
            }
        }

        SendResult INoAckRawReliableClient.Send(UnionDataList message)
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
            try
            {
                return $"direct-client[{_serverEp}]";
            }
            catch (Exception)
            {
                return "direct-client[unknown]";
            }
        }
    }
}
