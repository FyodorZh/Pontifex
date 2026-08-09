using System;
using System.Collections.Generic;
using System.Threading;
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
    /// Base class for all RawReliable Direct client transports. Owns the
    /// in-process channel and the outbound callback queue, and connects eagerly
    /// during Start: if the server is not registered, Start fails. A peer
    /// connection close is detected by polling the channel, so that a send
    /// issued immediately after the peer stops observes the dead channel and
    /// returns <see cref="SendResult.Error"/> while the endpoint is still
    /// nominally connected.
    /// </summary>
    public abstract class RawReliableDirectClientTransport : RawReliableAckClientTransport
    {
        private static readonly TimeSpan PeerDisconnectPollPeriod = TimeSpan.FromMilliseconds(50);

        private readonly IEndPoint _serverEp;
        private readonly IEndPoint _clientEp;
        private SerializedCallbackQueue<(RawReliableEndpoint, UnionDataList)>? _callbackQueue;
        private volatile Channel? _channel;
        private Timer? _peerDisconnectTimer;

        protected abstract int QueueCapacity { get; }

        protected RawReliableDirectClientTransport(string typeName, string serverName, ILogger logger, IMemoryRental memoryRental)
            : base(typeName, logger, memoryRental)
        {
            _serverEp = new StringEndPoint(serverName);
            _clientEp = new GuidEndPoint(Guid.NewGuid());
        }

        protected override IEndPoint? ClientRemoteEndPoint => _serverEp;

        protected override bool StartCarrier()
        {
            _callbackQueue = new SerializedCallbackQueue<(RawReliableEndpoint, UnionDataList)>(
                QueueCapacity,
                $"cli-cb-{_serverEp}",
                pair =>
                {
                    var (endpoint, message) = pair;
                    var channel = _channel;
                    if (channel == null)
                    {
                        message.Release();
                        return;
                    }
                    endpoint.Conformance.BeforeSendCommitGate.Hit();
                    channel.SendToServer(message);
                    endpoint.Conformance.AfterSendCommitGate.Hit();
                },
                pair => pair.Item2.Release());
            _callbackQueue.ExceptionHandler += ex => Log.wtf(ex);

            var channel = DirectTransportManager.Instance.Connect(_serverEp, _clientEp);
            if (channel == null)
            {
                _callbackQueue.Dispose();
                _callbackQueue = null;
                return false;
            }

            channel.ClientHandler = message => OnCarrierInbound(null, message);
            _channel = channel;
            _peerDisconnectTimer = new Timer(_ => CheckPeerDisconnected(), null, PeerDisconnectPollPeriod, PeerDisconnectPollPeriod);
            return true;
        }

        protected override void StopCarrier(StopReason reason)
        {
            _peerDisconnectTimer?.Dispose();
            _peerDisconnectTimer = null;

            var channel = _channel;
            if (channel != null)
            {
                _channel = null;
                DirectTransportManager.Instance.Disconnect(_serverEp, _clientEp);
            }
            _callbackQueue?.Dispose();
            _callbackQueue = null;
        }

        private void CheckPeerDisconnected()
        {
            var channel = _channel;
            if (channel == null || !channel.IsDisposed || !IsStarted)
                return;

            var timer = Interlocked.Exchange(ref _peerDisconnectTimer, null);
            timer?.Dispose();

            PostStop(new GracefulRemoteIntention(_serverEp.ToString()));
        }

        protected override SendResult SendToCarrier(RawReliableEndpoint endpoint, UnionDataList message)
        {
            var channel = _channel;
            if (channel == null || channel.IsDisposed)
            {
                message.Release();
                return SendResult.Error;
            }

            if (_callbackQueue?.Post((endpoint, message)) ?? false)
            {
                return SendResult.Ok;
            }
            message.Release();
            return SendResult.Error;
        }

        protected override void SendHandshakeToCarrier(UnionDataList ackData)
        {
            var channel = _channel;
            if (channel == null || channel.IsDisposed)
            {
                ackData.Release();
                FailEstablishment(new TextFail(Name, "Channel is not available"));
                return;
            }

            channel.SendToServer(ackData);
        }

        public override string ToString()
        {
            try { return $"direct-client[{_serverEp}]"; }
            catch (Exception) { return "direct-client[unknown]"; }
        }
    }
}
