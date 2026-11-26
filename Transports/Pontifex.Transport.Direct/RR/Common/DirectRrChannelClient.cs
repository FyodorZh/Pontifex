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
        private readonly string _scheme;
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
        private bool _stopped;

        public DirectRrChannelClient(
            string scheme,
            IEndPoint server,
            IEndPoint client,
            ILogger logger,
            Action<UnionDataList> inbound,
            Action? onDisposed = null,
            Action? onLiveSession = null,
            Func<IDeliverySystem>? clientToServerFactory = null,
            Func<IDeliverySystem>? serverToClientFactory = null)
        {
            _scheme = scheme ?? throw new ArgumentNullException(nameof(scheme));
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
                }
            }

            if (notifyLive)
            {
                // Owner callbacks run outside the carrier lock: the owner may re-enter the carrier.
                _onLiveSession?.Invoke();
            }

            Purge(purgeChannel);

            return result;
        }

        /// <summary>
        /// Stops the carrier, removing its channel from the server registry.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The stopped state is latched under the carrier lock,
        /// so a send that races the stop can never reconnect, and the registry entry is removed under
        /// the same lock, so a racing send cannot be refused by a stale entry.</description></item>
        /// </list>
        /// </remarks>
        public void Stop()
        {
            Channel? channel;

            lock (_lock)
            {
                _stopped = true;
                channel = _channel;
                _channel = null;

                if (channel != null && _disposedSubscription != null)
                {
                    channel.Disposed -= _disposedSubscription;
                }

                _disposedSubscription = null;

                if (channel != null)
                {
                    ChannelManager.Instance.Disconnect(_scheme, _server, _client);
                }
            }

            if (channel == null)
            {
                return;
            }

            Purge(channel);
        }

        private SendResult SendOrConnectUnderLock(UnionDataList frame, bool acceptWithoutPeer,
            out Channel? purgeChannel, out bool notifyLive)
        {
            purgeChannel = null;
            notifyLive = false;

            if (_stopped)
            {
                frame.Release();
                return acceptWithoutPeer ? SendResult.Ok : SendResult.Error;
            }

            Channel? channel = _channel;
            if (channel != null && channel.IsDisposed)
            {
                // Clear under the lock and purge the stale registry entry before reconnecting. The
                // channel is already disposed, so its own disposal notification cannot re-enter.
                ClearChannelUnderLock();
                purgeChannel = channel;
                ChannelManager.Instance.Disconnect(_scheme, _server, _client);
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
                // The channel already released the frame. Clearing and removing the registry entry
                // happens under the lock, so a concurrent send cannot reconnect into a stale entry.
                ClearChannelUnderLock();
                ChannelManager.Instance.Disconnect(_scheme, _server, _client);
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
                channel = ChannelManager.Instance.Connect(_scheme, _server, _client);
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
                    ReleaseCreated(clientToServer, clientToServerInstalled);
                    ReleaseCreated(serverToClient, serverToClientInstalled);
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

                ReleaseCreated(clientToServer, clientToServerInstalled);
                ReleaseCreated(serverToClient, serverToClientInstalled);

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

                // Remove the registry entry under the lock for the same reason as Stop: a send that
                // races this notification must not be refused by a stale entry.
                ChannelManager.Instance.Disconnect(_scheme, _server, _client);
                notify = true;
            }

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

            ChannelManager.Instance.Disconnect(_scheme, _server, _client);

            if (!channel.IsDisposed)
            {
                channel.Dispose();
            }
        }

        private static void ReleaseCreated(IDeliverySystem? system, bool installed)
        {
            if (system == null || installed)
            {
                // An installed system is owned by the channel until its disposal, or was released by
                // the install itself when the channel was already disposed. The carrier must not
                // release it a second time.
                return;
            }

            DeliverySystemLifetime.Release(system);
        }
    }
}
