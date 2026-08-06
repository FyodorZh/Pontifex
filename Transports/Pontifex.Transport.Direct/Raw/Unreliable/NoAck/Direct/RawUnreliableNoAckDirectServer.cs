using System;
using System.Collections.Concurrent;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable.NoAck.Direct
{
    public sealed class RawUnreliableNoAckDirectServer : RawUnreliableNoAckServerTransport, IRawUnreliableNoAckServer
    {
        private readonly IEndPoint _serverEp;
        private readonly ConcurrentDictionary<IEndPoint, Channel> _channels = new();
        private SerializedCallbackQueue<(RawUnreliableEndpoint, UnionDataList)>? _callbackQueue;

        public override int MessageMaxByteSize => DirectInfo.MessageMaxByteSize;

        public override TransportType Type => TransportType.RawUnreliableNoAck;

        public RawUnreliableNoAckDirectServer(string serverName, ILogger logger, IMemoryRental memoryRental)
            : base("direct-noack-raw-unreliable", logger, memoryRental)
        {
            _serverEp = new StringEndPoint(serverName);
        }

        protected override bool StartCarrier()
        {
            _callbackQueue = new SerializedCallbackQueue<(RawUnreliableEndpoint, UnionDataList)>(
                100,
                $"srv-cb-{_serverEp}",
                pair =>
                {
                    var (endpoint, message) = pair;
                    if (!_channels.TryGetValue(endpoint.RemoteEndPoint!, out var channel))
                    {
                        message.Release();
                        return;
                    }
                    endpoint.Conformance.BeforeSendCommitGate.Hit();
                    channel.SendToClient(message);
                    endpoint.Conformance.AfterSendCommitGate.Hit();
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

        protected override void StopCarrier(StopReason reason)
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

        protected override SendResult SendToCarrier(RawUnreliableEndpoint endpoint, UnionDataList message)
        {
            if (!IsStarted)
            {
                message.Release();
                return SendResult.Error;
            }

            if (!_channels.TryGetValue(endpoint.RemoteEndPoint!, out _))
            {
                message.Release();
                return SendResult.InvalidAddress;
            }

            if (_callbackQueue?.Post((endpoint, message)) ?? false)
            {
                return SendResult.Ok;
            }
            message.Release();
            return SendResult.Error;
        }

        private void OnChannelCreated(Channel channel)
        {
            channel.ServerHandler = (clientEp, message) => OnCarrierInbound(clientEp, message);
            _channels.TryAdd(channel.ClientEp, channel);
        }

        public override string ToString()
        {
            try { return $"direct-server[{_serverEp}]"; }
            catch (Exception) { return "direct-server[unknown]"; }
        }

        protected override bool TryMakeReliableForDebug() => true;
    }
}
