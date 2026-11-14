using System;
using System.Collections.Concurrent;

namespace Pontifex
{
    internal sealed class ChannelManager
    {
        public static readonly ChannelManager Instance = new ChannelManager();

        private readonly ConcurrentDictionary<IEndPoint, ServerEntry> _servers =
            new ConcurrentDictionary<IEndPoint, ServerEntry>();

        private ChannelManager()
        {
        }

        public bool RegisterServer(IEndPoint serverEp, Action<Channel> onChannelCreated)
        {
            var entry = new ServerEntry(onChannelCreated);
            return _servers.TryAdd(serverEp, entry);
        }

        public void UnregisterServer(IEndPoint serverEp)
        {
            if (_servers.TryRemove(serverEp, out var entry))
            {
                entry.DisconnectAll();
            }
        }

        public Channel? Connect(IEndPoint serverEp, IEndPoint clientEp)
        {
            if (_servers.TryGetValue(serverEp, out var entry))
            {
                var channel = new Channel(clientEp, serverEp);
                if (entry.AddClient(clientEp, channel))
                {
                    entry.InvokeOnChannelCreated(channel);
                    return channel;
                }

                channel.Dispose();
            }

            return null;
        }

        /// <summary>
        /// Removes a client channel from the server registry and disposes it.
        /// </summary>
        /// <returns><see langword="true"/> when the channel was registered and has been disposed; otherwise
        /// <see langword="false"/>. A missing entry does not mean the channel is alive: it may have been
        /// detached by its owner already.</returns>
        public bool Disconnect(IEndPoint serverEp, IEndPoint clientEp)
        {
            if (_servers.TryGetValue(serverEp, out var entry))
            {
                return entry.RemoveClient(clientEp);
            }

            return false;
        }

        /// <summary>
        /// Removes a client channel from the server registry without disposing it. Used when a session
        /// endpoint takes ownership of an established channel.
        /// </summary>
        /// <returns><see langword="true"/> when the channel was removed from the registry.</returns>
        public bool Detach(IEndPoint serverEp, IEndPoint clientEp)
        {
            if (_servers.TryGetValue(serverEp, out var entry))
            {
                return entry.DetachClient(clientEp);
            }

            return false;
        }

        private sealed class ServerEntry
        {
            private readonly Action<Channel> _onChannelCreated;
            private readonly ConcurrentDictionary<IEndPoint, Channel> _clients =
                new ConcurrentDictionary<IEndPoint, Channel>();

            public ServerEntry(Action<Channel> onChannelCreated)
            {
                _onChannelCreated = onChannelCreated;
            }

            public bool AddClient(IEndPoint clientEp, Channel channel)
            {
                return _clients.TryAdd(clientEp, channel);
            }

            public bool RemoveClient(IEndPoint clientEp)
            {
                if (_clients.TryRemove(clientEp, out var channel))
                {
                    channel.Dispose();
                    return true;
                }

                return false;
            }

            public bool DetachClient(IEndPoint clientEp)
            {
                return _clients.TryRemove(clientEp, out _);
            }

            public void InvokeOnChannelCreated(Channel channel)
            {
                _onChannelCreated(channel);
            }

            public void DisconnectAll()
            {
                foreach (var kv in _clients)
                {
                    kv.Value.Dispose();
                }

                _clients.Clear();
            }
        }
    }
}
