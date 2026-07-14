using System;
using System.Collections.Concurrent;

namespace Pontifex.NoAck.Raw.Direct
{
    internal sealed class DirectTransportManager
    {
        public static readonly DirectTransportManager Instance = new DirectTransportManager();

        private readonly ConcurrentDictionary<IEndPoint, ServerEntry> _servers =
            new ConcurrentDictionary<IEndPoint, ServerEntry>();

        private DirectTransportManager()
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

        public void Disconnect(IEndPoint serverEp, IEndPoint clientEp)
        {
            if (_servers.TryGetValue(serverEp, out var entry))
            {
                entry.RemoveClient(clientEp);
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
