using System;
using System.Collections.Concurrent;

namespace Pontifex
{
    internal sealed class ChannelManager
    {
        public static readonly ChannelManager Instance = new ChannelManager();

        private readonly ConcurrentDictionary<ServerKey, ServerEntry> _servers =
            new ConcurrentDictionary<ServerKey, ServerEntry>();

        private ChannelManager()
        {
        }

        public bool RegisterServer(string scheme, IEndPoint serverEp, Action<Channel> onChannelCreated)
        {
            if (scheme == null)
            {
                throw new ArgumentNullException(nameof(scheme));
            }

            var entry = new ServerEntry(onChannelCreated);
            return _servers.TryAdd(new ServerKey(scheme, serverEp), entry);
        }

        public void UnregisterServer(string scheme, IEndPoint serverEp)
        {
            if (_servers.TryRemove(new ServerKey(scheme, serverEp), out var entry))
            {
                entry.DisconnectAll();
            }
        }

        public Channel? Connect(string scheme, IEndPoint serverEp, IEndPoint clientEp)
        {
            if (_servers.TryGetValue(new ServerKey(scheme, serverEp), out var entry))
            {
                var channel = new Channel(clientEp, serverEp);
                if (entry.AddClient(clientEp, channel))
                {
                    try
                    {
                        entry.InvokeOnChannelCreated(channel);
                        return channel;
                    }
                    catch (Exception)
                    {
                        // A throwing server callback must not leave a registered channel behind.
                        entry.RemoveClient(clientEp);
                        return null;
                    }
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
        public bool Disconnect(string scheme, IEndPoint serverEp, IEndPoint clientEp)
        {
            if (_servers.TryGetValue(new ServerKey(scheme, serverEp), out var entry))
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
        public bool Detach(string scheme, IEndPoint serverEp, IEndPoint clientEp)
        {
            if (_servers.TryGetValue(new ServerKey(scheme, serverEp), out var entry))
            {
                return entry.DetachClient(clientEp);
            }

            return false;
        }

        /// <summary>
        /// The registry key: a protocol scheme plus the server endpoint. The same id may exist under
        /// different Direct protocols without mixing channels.
        /// </summary>
        private readonly struct ServerKey : IEquatable<ServerKey>
        {
            private readonly string _scheme;
            private readonly IEndPoint _endPoint;

            public ServerKey(string scheme, IEndPoint endPoint)
            {
                _scheme = scheme ?? throw new ArgumentNullException(nameof(scheme));
                _endPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
            }

            public bool Equals(ServerKey other)
            {
                return string.Equals(_scheme, other._scheme, StringComparison.Ordinal)
                    && _endPoint.Equals(other._endPoint);
            }

            public override bool Equals(object? obj)
            {
                return obj is ServerKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_scheme.GetHashCode() * 397) ^ _endPoint.GetHashCode();
                }
            }
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
