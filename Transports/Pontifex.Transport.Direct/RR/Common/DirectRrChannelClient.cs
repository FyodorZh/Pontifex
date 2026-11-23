using System;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;
using Scriba;

namespace Pontifex.RR.Direct
{
    /// <summary>
    /// One lazily connected in-process channel of an RR Direct client. The carrier owns the channel and
    /// the delivery systems it installs; the transport owns frame contents.
    /// </summary>
    internal sealed class DirectRrChannelClient
    {
        private readonly object _lock = new object();
        private readonly IEndPoint _server;
        private readonly IEndPoint _client;
        private readonly ILogger _log;
        private readonly Action<UnionDataList> _inbound;
        private readonly Action? _onDisposed;
        private readonly Action? _onLiveSession;
        private readonly Func<IDeliverySystem>? _clientToServerFactory;
        private readonly Func<IDeliverySystem>? _serverToClientFactory;

        private volatile Channel? _channel;
        private Action? _disposedSubscription;
        private bool _liveSessionReported;

        public DirectRrChannelClient(
            IEndPoint server,
            IEndPoint client,
            ILogger logger,
            Action<UnionDataList> inbound,
            Action? onDisposed = null,
            Action? onLiveSession = null,
            Func<IDeliverySystem>? clientToServerFactory = null,
            Func<IDeliverySystem>? serverToClientFactory = null)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _log = logger ?? throw new ArgumentNullException(nameof(logger));
            _inbound = inbound ?? throw new ArgumentNullException(nameof(inbound));
            _onDisposed = onDisposed;
            _onLiveSession = onLiveSession;
            _clientToServerFactory = clientToServerFactory;
            _serverToClientFactory = serverToClientFactory;
        }

        /// <summary>
        /// Gets the connected channel, or <see langword="null"/> before the first send or after a loss.
        /// </summary>
        internal Channel? CurrentChannel => _channel;

        /// <summary>
        /// Sends one complete frame, connecting on demand.
        /// </summary>
        /// <param name="frame">The frame to send. The carrier owns it on every path.</param>
        /// <param name="acceptWithoutPeer">Whether a missing server is accepted as
        /// <see cref="SendResult.Ok"/>.</param>
        /// <returns>The send result. A missing peer never yields
        /// <see cref="SendResult.NotConnected"/>.</returns>
        public SendResult TrySend(UnionDataList frame, bool acceptWithoutPeer)
        {
            if (frame == null!)
            {
                return SendResult.InvalidMessage;
            }

            SendResult result;
            Channel? purgeChannel;
            bool notifyLive;

            lock (_lock)
            {
                result = SendOrConnectUnderLock(
                    frame,
                    acceptWithoutPeer,
                    out purgeChannel,
                    out notifyLive);

                if (notifyLive)
                {
                    _liveSessionReported = true;
                    _onLiveSession?.Invoke();
                }
            }

            Purge(purgeChannel);

            return result;
        }

        /// <summary>
        /// Stops the carrier, removing its channel from the server registry.
        /// </summary>
        public void Stop()
        {
            Channel? channel;
            Action? subscription;

            lock (_lock)
            {
                channel = _channel;
                subscription = _disposedSubscription;
                _channel = null;
                _disposedSubscription = null;
            }

            if (channel == null)
            {
                return;
            }

            if (subscription != null)
            {
                channel.Disposed -= subscription;
            }

            Purge(channel);
        }

        private SendResult SendOrConnectUnderLock(UnionDataList frame, bool acceptWithoutPeer,
            out Channel? purgeChannel, out bool notifyLive)
        {
            purgeChannel = null;
            notifyLive = false;

            Channel? channel = _channel;
            if (channel != null && channel.IsDisposed)
            {
                // Clear under the lock and purge the stale registry entry before reconnecting. The
                // channel is already disposed, so its own disposal notification cannot re-enter.
                ClearChannelUnderLock();
                purgeChannel = channel;
                ChannelManager.Instance.Disconnect(_server, _client);
                channel = null;
            }

            if (channel == null)
            {
                _disposedSubscription = null;
                SendResult connectResult = ConnectAndSendUnderLock(
                    frame,
                    acceptWithoutPeer,
                    out Channel? connectPurge,
                    out notifyLive);
                purgeChannel = connectPurge;
                return connectResult;
            }

            return SendUnderLock(channel, frame, acceptWithoutPeer, out purgeChannel, out notifyLive);
        }

        private SendResult SendUnderLock(Channel channel, UnionDataList frame, bool acceptWithoutPeer,
            out Channel? purgeChannel, out bool notifyLive)
        {
            purgeChannel = null;
            notifyLive = false;

            SendResult result;
            try
            {
                result = channel.SendToServer(frame);
            }
            catch (Exception ex)
            {
                _log.wtf(ex);
                frame.Release();
                return SendResult.Error;
            }

            if (result == SendResult.NotConnected)
            {
                // The channel already released the frame. Clearing first makes the reentrant disposal
                // notification skip the owner callback.
                ClearChannelUnderLock();
                purgeChannel = channel;
                return acceptWithoutPeer ? SendResult.Ok : SendResult.Error;
            }

            if (result == SendResult.Ok)
            {
                notifyLive = !_liveSessionReported;
            }

            return result;
        }

        private SendResult ConnectAndSendUnderLock(UnionDataList frame, bool acceptWithoutPeer,
            out Channel? purgeChannel, out bool notifyLive)
        {
            purgeChannel = null;
            notifyLive = false;

            Channel? channel = null;
            Action? subscription = null;
            IDeliverySystem? clientToServer = null;
            IDeliverySystem? serverToClient = null;
            bool clientToServerInstalled = false;
            bool serverToClientInstalled = false;

            try
            {
                channel = ChannelManager.Instance.Connect(_server, _client);
                if (channel == null)
                {
                    frame.Release();
                    return acceptWithoutPeer ? SendResult.Ok : SendResult.Error;
                }

                subscription = () => OnChannelDisposed(channel);
                channel.Disposed += subscription;
                channel.ClientHandler = _inbound;

                if (_clientToServerFactory != null)
                {
                    clientToServer = _clientToServerFactory();
                    channel.SetServerDeliverySystem(clientToServer);
                    clientToServerInstalled = true;
                }

                if (_serverToClientFactory != null)
                {
                    serverToClient = _serverToClientFactory();
                    channel.SetClientDeliverySystem(serverToClient);
                    serverToClientInstalled = true;
                }

                if (channel.IsDisposed)
                {
                    channel.Disposed -= subscription;
                    subscription = null;
                    channel.ClientHandler = null;
                    purgeChannel = channel;
                    ReleaseCreated(clientToServer, clientToServerInstalled, channel);
                    ReleaseCreated(serverToClient, serverToClientInstalled, channel);
                    frame.Release();
                    return acceptWithoutPeer ? SendResult.Ok : SendResult.Error;
                }

                _channel = channel;
                _disposedSubscription = subscription;
                subscription = null;
            }
            catch (Exception ex)
            {
                _log.wtf(ex);

                if (channel != null && subscription != null)
                {
                    channel.Disposed -= subscription;
                }

                if (channel != null)
                {
                    channel.ClientHandler = null;
                    purgeChannel = channel;
                }

                ReleaseCreated(clientToServer, clientToServerInstalled, channel);
                ReleaseCreated(serverToClient, serverToClientInstalled, channel);

                frame.Release();
                return SendResult.Error;
            }

            return SendUnderLock(channel, frame, acceptWithoutPeer, out purgeChannel, out notifyLive);
        }

        private void OnChannelDisposed(Channel channel)
        {
            bool notify = false;

            lock (_lock)
            {
                if (!ReferenceEquals(_channel, channel))
                {
                    return;
                }

                _channel = null;

                if (_disposedSubscription != null)
                {
                    channel.Disposed -= _disposedSubscription;
                    _disposedSubscription = null;
                }

                notify = true;
            }

            ChannelManager.Instance.Disconnect(_server, _client);

            if (notify)
            {
                _onDisposed?.Invoke();
            }
        }

        private void ClearChannelUnderLock()
        {
            Channel? channel = _channel;
            _channel = null;

            if (channel != null && _disposedSubscription != null)
            {
                channel.Disposed -= _disposedSubscription;
            }

            _disposedSubscription = null;
        }

        private void Purge(Channel? channel)
        {
            if (channel == null)
            {
                return;
            }

            ChannelManager.Instance.Disconnect(_server, _client);

            if (!channel.IsDisposed)
            {
                channel.Dispose();
            }
        }

        private static void ReleaseCreated(IDeliverySystem? system, bool installed, Channel? channel)
        {
            if (system == null)
            {
                return;
            }

            // A live channel owns its installed systems and releases them when it is disposed. Once the
            // channel is disposed it cannot own a system installed afterwards: the disposal already
            // exchanged and released both delivery systems, so a late install would leak.
            if (installed && channel != null && !channel.IsDisposed)
            {
                return;
            }

            DeliverySystemLifetime.Release(system);
        }
    }
}
