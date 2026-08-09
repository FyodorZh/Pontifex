using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw.Direct;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Direct
{
    /// <summary>
    /// Base class for all RawReliable Direct server transports. Owns the
    /// in-process channel registry and the outbound callback queue. The variant
    /// ACK send hooks remain abstract; the Ack Direct concrete transport
    /// supplies the handshake wire format.
    /// </summary>
    public abstract class RawReliableDirectServerTransport : RawReliableAckServerTransport
    {
        private readonly IEndPoint _serverEp;
        protected readonly ConcurrentDictionary<IEndPoint, Channel> _channels = new();
        private SerializedCallbackQueue<(RawReliableEndpoint, UnionDataList)>? _callbackQueue;

        protected abstract int QueueCapacity { get; }

        protected RawReliableDirectServerTransport(string typeName, string serverName, ILogger logger, IMemoryRental memoryRental)
            : base(typeName, logger, memoryRental)
        {
            _serverEp = new StringEndPoint(serverName);
        }

        protected override bool StartCarrier()
        {
            _callbackQueue = new SerializedCallbackQueue<(RawReliableEndpoint, UnionDataList)>(
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

        protected override SendResult SendToCarrier(RawReliableEndpoint endpoint, UnionDataList message)
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
            channel.ServerClosed = () => OnChannelClosed(channel);
            _channels.TryAdd(channel.ClientEp, channel);
        }

        private void OnChannelClosed(Channel channel)
        {
            _channels.TryRemove(channel.ClientEp, out _);
            DisconnectSessionEndpoint(channel.ClientEp, new GracefulRemoteIntention(_serverEp.ToString()));
        }

        protected override void OnServerSessionEnded(IEndPoint source)
        {
            if (_channels.TryRemove(source, out var channel))
            {
                channel.Dispose();
            }
        }

        public override string ToString()
        {
            try { return $"direct-server[{_serverEp}]"; }
            catch (Exception) { return "direct-server[unknown]"; }
        }
    }
}
