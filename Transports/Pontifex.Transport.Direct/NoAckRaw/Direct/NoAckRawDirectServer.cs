using System;
using System.Collections.Concurrent;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.NoAck.Raw.Direct
{
    public abstract class NoAckRawDirectServer : NoAckRawTransport
    {
        private readonly IEndPoint _serverEp;
        private readonly ConcurrentDictionary<IEndPoint, Channel> _channels = new();
        private SerializedCallbackQueue<(IEndPoint, UnionDataList)>? _callbackQueue;

        public event Action<IEndPoint, UnionDataList>? OnReceived;

        public int MessageMaxByteSize => DirectInfo.MessageMaxByteSize;

        protected NoAckRawDirectServer(string serverName, string transportName, ILogger logger, IMemoryRental memoryRental)
            : base(transportName, logger, memoryRental)
        {
            _serverEp = new StringEndPoint(serverName);
        }

        protected sealed override bool TryStart()
        {
            _callbackQueue = new SerializedCallbackQueue<(IEndPoint, UnionDataList)>(
                $"srv-cb-{_serverEp}",
                pair =>
                {
                    var (clientEp, message) = pair;
                    if (_channels.TryGetValue(clientEp, out var channel))
                    {
                        Conformance.BeforeTrySendStateDecisionGate.Hit();
                        channel.SendToClient(message);
                    }
                    else
                        message.Release();
                });
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
                OnBeforeChannelRemoved(channel);
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
                        handler(clientEp, message);
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

            _channels.TryAdd(channel.ClientEp, channel);
            OnChannelAdded(channel);
        }

        protected virtual void OnChannelAdded(Channel channel) { }

        protected virtual void OnBeforeChannelRemoved(Channel channel) { }

        protected SendResult SendToClient(IEndPoint destination, UnionDataList message)
        {
            if (_channels.TryGetValue(destination, out var channel))
            {
                _callbackQueue?.Post((destination, message));
                return SendResult.Ok;
            }

            message.Release();
            return SendResult.InvalidAddress;
        }

        public override string ToString()
        {
            try { return $"direct-server[{_serverEp}]"; }
            catch (Exception) { return "direct-server[unknown]"; }
        }
    }
}
