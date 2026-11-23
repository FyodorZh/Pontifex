using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Pontifex.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.RR.Direct
{
    /// <summary>
    /// In-process server channel registry of the RR Direct carriers. It accepts one channel per client
    /// endpoint and routes responses back through the channel that delivered the request.
    /// </summary>
    internal sealed class DirectRrChannelServer
    {
        private readonly object _locker = new object();
        private readonly IEndPoint _server;
        private readonly ILogger _log;
        private readonly Action<IEndPoint, UnionDataList> _inbound;
        private readonly Action<IEndPoint>? _onClientDisconnected;
        private readonly ConcurrentDictionary<IEndPoint, Channel> _channels =
            new ConcurrentDictionary<IEndPoint, Channel>();

        private bool _stopping;

        public DirectRrChannelServer(
            string serverId,
            ILogger logger,
            Action<IEndPoint, UnionDataList> inbound,
            Action<IEndPoint>? onClientDisconnected = null)
        {
            if (serverId == null)
            {
                throw new ArgumentNullException(nameof(serverId));
            }

            _server = new StringEndPoint(serverId);
            _log = logger ?? throw new ArgumentNullException(nameof(logger));
            _inbound = inbound ?? throw new ArgumentNullException(nameof(inbound));
            _onClientDisconnected = onClientDisconnected;
        }

        /// <summary>
        /// Gets or sets a test hook invoked after the server registration succeeds and before
        /// <c>TryStart</c> returns. The hook may block.
        /// </summary>
        internal Action? StartGateForTests { get; set; }

        /// <summary>
        /// Gets the number of channels currently registered with this server.
        /// </summary>
        internal int RegisteredClientCount => _channels.Count;

        /// <summary>
        /// Registers the server with the process-wide channel manager.
        /// </summary>
        /// <returns><see langword="true"/> when the server id was free; otherwise
        /// <see langword="false"/>.</returns>
        public bool TryStart()
        {
            lock (_locker)
            {
                if (_stopping)
                {
                    return false;
                }

                if (!ChannelManager.Instance.RegisterServer(_server, OnChannelCreated))
                {
                    _log.e("Failed to register RR Direct server '{0}'. Name already in use.", _server);
                    return false;
                }
            }

            StartGateForTests?.Invoke();
            return true;
        }

        /// <summary>
        /// Sends one frame to a registered client.
        /// </summary>
        /// <param name="destination">The client route that received the request.</param>
        /// <param name="frame">The frame to send. The carrier owns it on every path.</param>
        /// <returns>The send result. A missing destination yields
        /// <see cref="SendResult.Error"/>.</returns>
        public SendResult TrySend(IEndPoint destination, UnionDataList frame)
        {
            if (destination == null!)
            {
                frame?.Release();
                return SendResult.Error;
            }

            if (frame == null!)
            {
                return SendResult.InvalidMessage;
            }

            if (!_channels.TryGetValue(destination, out Channel? channel))
            {
                frame.Release();
                return SendResult.Error;
            }

            SendResult result;
            try
            {
                result = channel.SendToClient(frame);
            }
            catch (Exception ex)
            {
                _log.wtf(ex);
                frame.Release();
                return SendResult.Error;
            }

            return result == SendResult.NotConnected ? SendResult.Error : result;
        }

        /// <summary>
        /// Stops the server, unregisters the id, and disposes every registered channel.
        /// </summary>
        public void Stop()
        {
            lock (_locker)
            {
                _stopping = true;
            }

            ChannelManager.Instance.UnregisterServer(_server);

            foreach (KeyValuePair<IEndPoint, Channel> pair in _channels)
            {
                if (_channels.TryRemove(pair.Key, out Channel? channel))
                {
                    channel.Dispose();
                }
            }

            _channels.Clear();
        }

        /// <summary>
        /// Rejects a channel through the same purge path used by the carrier.
        /// </summary>
        internal void RejectChannelForTests(IEndPoint clientEndPoint, Channel channel)
        {
            RejectChannel(clientEndPoint, channel);
        }

        private void OnChannelCreated(Channel channel)
        {
            bool stopping;
            lock (_locker)
            {
                stopping = _stopping;
            }

            if (stopping)
            {
                RejectChannel(channel.ClientEp, channel);
                return;
            }

            if (!_channels.TryAdd(channel.ClientEp, channel))
            {
                RejectChannel(channel.ClientEp, channel);
                return;
            }

            channel.ServerHandler = (clientEndPoint, message) => _inbound(clientEndPoint, message);
            channel.Disposed += () => OnChannelDisposed(channel);

            // A concurrent stop may have swept the registry before the channel was added, and the channel
            // may already have been disposed while it was being registered. Both cases leave no entry.
            lock (_locker)
            {
                stopping = _stopping;
            }

            if (channel.IsDisposed || stopping)
            {
                RemoveChannel(channel);
                RejectChannel(channel.ClientEp, channel);
            }
        }

        private void OnChannelDisposed(Channel channel)
        {
            bool removed;
            lock (_locker)
            {
                removed = _channels.TryGetValue(channel.ClientEp, out Channel? current)
                          && ReferenceEquals(current, channel)
                          && _channels.TryRemove(channel.ClientEp, out _);
            }

            if (removed)
            {
                _onClientDisconnected?.Invoke(channel.ClientEp);
            }
        }

        private void RemoveChannel(Channel channel)
        {
            if (_channels.TryGetValue(channel.ClientEp, out Channel? current)
                && ReferenceEquals(current, channel))
            {
                _channels.TryRemove(channel.ClientEp, out _);
            }
        }

        private void RejectChannel(IEndPoint clientEndPoint, Channel channel)
        {
            ChannelManager.Instance.Disconnect(_server, clientEndPoint);
            channel.Dispose();
        }
    }
}
