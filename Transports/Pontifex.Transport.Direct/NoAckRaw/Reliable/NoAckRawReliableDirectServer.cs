using System;
using System.Collections.Concurrent;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Transports.Core;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.NoAck.Raw.Reliable.Direct
{
    public sealed class NoAckRawReliableDirectServer : AbstractTransport, INoAckRawReliableServer
    {
        private readonly IEndPoint _serverEp;
        private readonly ConcurrentDictionary<IEndPoint, Channel> _channels = new ConcurrentDictionary<IEndPoint, Channel>();

        public override TransportType Type => TransportType.NoAckRawReliable;
        
        public NoAckRawReliableDirectServer(string serverName, ILogger logger, IMemoryRental memoryRental)
            : base(DirectInfo.TransportName, logger, memoryRental)
        {
            _serverEp = new StringEndPoint(serverName);
        }

        public event Action<IEndPoint, UnionDataList>? OnReceived;

        public int MessageMaxByteSize => DirectInfo.MessageMaxByteSize;

        protected override bool TryStart()
        {
            if (!DirectTransportManager.Instance.RegisterServer(_serverEp, OnChannelCreated))
            {
                Log.e("Failed to register server '{0}'. Name already in use.", _serverEp);
                return false;
            }

            return true;
        }

        protected override void OnStarted()
        {
        }

        protected override void OnStopped(StopReason reason)
        {
            DirectTransportManager.Instance.UnregisterServer(_serverEp);
            _channels.Clear();
        }

        private void OnChannelCreated(Channel channel)
        {
            channel.ServerHandler = (clientEp, message) =>
            {
                var handler = OnReceived;
                if (handler != null)
                {
                    try
                    {
                        handler(clientEp, message);
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

            _channels.TryAdd(channel.ClientEp, channel);
        }

        SendResult INoAckRawReliableServer.Send(IEndPoint destination, UnionDataList message)
        {
            if (_channels.TryGetValue(destination, out var channel))
            {
                return channel.SendToClient(message);
            }

            message.Release();
            return SendResult.InvalidAddress;
        }

        public override string ToString()
        {
            try
            {
                return $"direct-server[{_serverEp}]";
            }
            catch (Exception)
            {
                return "direct-server[unknown]";
            }
        }
    }
}
