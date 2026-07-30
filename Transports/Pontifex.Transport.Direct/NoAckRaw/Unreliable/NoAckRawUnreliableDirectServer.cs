using System;
using System.Collections.Concurrent;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.NoAck.Raw.Unreliable.Direct
{
    public sealed class NoAckRawUnreliableDirectServer : NoAckRawUnreliableServerTransport, INoAckRawUnreliableServer
    {
        private readonly IEndPoint _serverEp;
        private readonly ConcurrentDictionary<IEndPoint, Channel> _channels = new();
        private SerializedCallbackQueue<(IEndPoint, UnionDataList)>? _callbackQueue;

        public event Action<IEndPoint, UnionDataList>? OnReceived;

        public int MessageMaxByteSize => DirectInfo.MessageMaxByteSize;

        public override TransportType Type => TransportType.NoAckRawUnreliable;

        public NoAckRawUnreliableDirectServer(string serverName, ILogger logger, IMemoryRental memoryRental)
            : base("direct-noack-raw-unreliable", logger, memoryRental)
        {
            _serverEp = new StringEndPoint(serverName);
        }

        protected override bool TryStart()
        {
            _callbackQueue = new SerializedCallbackQueue<(IEndPoint, UnionDataList)>(
                100,
                $"srv-cb-{_serverEp}",
                pair =>
                {
                    var (clientEp, message) = pair;
                    if (_channels.TryGetValue(clientEp, out var channel))
                    {
                        Conformance.BeforeSendCommitGate.Hit();
                        channel.SendToClient(message);
                        Conformance.AfterSendCommitGate.Hit();
                    }
                    else
                        message.Release();
                },
                pair => pair.Item2.Release());
            _callbackQueue.ExceptionHandler += ex => Log.wtf(ex);
            
            if (!DirectTransportManager.Instance.RegisterServer(_serverEp, OnChannelCreated))
            {
                Log.e("Failed to register server '{0}'. Name already in use.", _serverEp);
                _callbackQueue.Dispose();
                _callbackQueue = null;
                return false;
            }
            return true;
        }

        protected override void OnStarted() { }

        protected override void OnStopped(StopReason reason)
        {
            DirectTransportManager.Instance.UnregisterServer(_serverEp);
            foreach (var channel in _channels.Values)
            {
                channel.Dispose();
            }
            _channels.Clear();
            _callbackQueue?.Dispose();
            _callbackQueue = null;
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
                        Conformance.AfterReceivedGate.Hit();
                        handler(clientEp, message);
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

            _channels.TryAdd(channel.ClientEp, channel);
        }

        private SendResult SendToClient(IEndPoint destination, UnionDataList message)
        {
            if (!IsStarted)
            {
                message?.Release();
                return SendResult.Error;
            }

            if (message == null!)
            {
                return SendResult.InvalidMessage;
            }
            
            if (!_channels.TryGetValue(destination, out _))
            {
                message.Release();
                return SendResult.InvalidAddress;
            }

            if (message.GetDataSize() > DirectInfo.MessageMaxByteSize)
            {
                message.Release();
                return SendResult.MessageTooBig;
            }

            if (_callbackQueue?.Post((destination, message)) ?? false)
            {
                return SendResult.Ok;
            }
            message.Release();
            return SendResult.Error;
        }

        public SendResult TrySend(IEndPoint destination, UnionDataList message) => SendToClient(destination, message);

        public override string ToString()
        {
            try { return $"direct-server[{_serverEp}]"; }
            catch (Exception) { return "direct-server[unknown]"; }
        }
    }
}
