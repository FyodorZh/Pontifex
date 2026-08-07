using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable.Direct
{
    /// <summary>
    /// Base class for all RawUnreliable Direct server transports. Owns the
    /// in-process channel registry and callback queue shared by the Ack and
    /// NoAck contract variants. The generic parameter is the variant
    /// handler-factory delegate type.
    /// </summary>
    public abstract class RawUnreliableDirectServerTransport<TFactory> : RawUnreliableServerTransport<TFactory>
        where TFactory : Delegate
    {
        private readonly IEndPoint _serverEp;
        private readonly ConcurrentDictionary<IEndPoint, Channel> _channels = new();
        private SerializedCallbackQueue<(RawUnreliableEndpoint, UnionDataList)>? _callbackQueue;

        protected abstract int QueueCapacity { get; }

        protected RawUnreliableDirectServerTransport(string typeName, string serverName, ILogger logger, IMemoryRental memoryRental)
            : base(typeName, logger, memoryRental)
        {
            _serverEp = new StringEndPoint(serverName);
        }

        protected override bool StartCarrier()
        {
            _callbackQueue = new SerializedCallbackQueue<(RawUnreliableEndpoint, UnionDataList)>(
                QueueCapacity,
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
