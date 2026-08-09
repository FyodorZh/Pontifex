using System;
using System.Threading;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;

namespace Pontifex.Raw.Direct
{
    /// <summary>
    /// In-process point-to-point carrier shared by the Raw Direct transports.
    /// One channel links a single client endpoint to a single server endpoint.
    /// Each direction has an independent <see cref="IDeliverySystem"/> that may
    /// reorder, drop, or duplicate messages for simulation purposes.
    /// </summary>
    public sealed class Channel : IDisposable
    {
        private readonly IEndPoint _clientEp;
        private readonly IEndPoint _serverEp;
        private volatile Action<UnionDataList>? _clientHandler;
        private volatile Action<IEndPoint, UnionDataList>? _serverHandler;
        private volatile IDeliverySystem _clientDeliverySystem;
        private volatile IDeliverySystem _serverDeliverySystem;
        private volatile bool _disposed;
        private Action? _clientClosed;
        private Action? _serverClosed;

        public Channel(IEndPoint clientEp, IEndPoint serverEp)
        {
            _clientEp = clientEp;
            _serverEp = serverEp;
            _clientDeliverySystem = new PerfectDeliverySystem();
            _serverDeliverySystem = new PerfectDeliverySystem();
            _clientDeliverySystem.Delivered += OnClientDeliveredMessage;
            _serverDeliverySystem.Delivered += OnServerDeliveredMessage;
        }

        public IEndPoint ClientEp => _clientEp;

        public bool IsDisposed => _disposed;

        public Action<UnionDataList>? ClientHandler
        {
            set => _clientHandler = value;
        }

        public Action<IEndPoint, UnionDataList>? ServerHandler
        {
            set => _serverHandler = value;
        }

        /// <summary>
        /// Invoked exactly once when the channel is disposed, so the client side
        /// can observe the peer connection closing. Set once during setup.
        /// </summary>
        public Action? ClientClosed
        {
            set => _clientClosed = value;
        }

        /// <summary>
        /// Invoked exactly once when the channel is disposed, so the server side
        /// can observe the peer connection closing. Set once during setup.
        /// </summary>
        public Action? ServerClosed
        {
            set => _serverClosed = value;
        }

        /// <summary>
        /// It is possible and acceptable for messages that are processed right now to be undelivered.
        /// The most important invariant is to release messages.
        /// </summary>
        public void SetClientDeliverySystem(IDeliverySystem clientDeliverySystem)
        {
            if (clientDeliverySystem != _clientDeliverySystem)
            {
                clientDeliverySystem.Delivered += OnClientDeliveredMessage;
                var oldClient = Interlocked.Exchange(ref _clientDeliverySystem, clientDeliverySystem);
                oldClient.Delivered -= OnClientDeliveredMessage;
                oldClient.Clear();
            }
        }

        /// <summary>
        /// It is possible and acceptable for messages that are processed right now to be undelivered.
        /// The most important invariant is to release messages.
        /// </summary>
        public void SetServerDeliverySystem(IDeliverySystem serverDeliverySystem)
        {
            if (serverDeliverySystem != _serverDeliverySystem)
            {
                serverDeliverySystem.Delivered += OnServerDeliveredMessage;
                var oldServer = Interlocked.Exchange(ref _serverDeliverySystem, serverDeliverySystem);
                oldServer.Delivered -= OnServerDeliveredMessage;
                oldServer.Clear();
            }
        }
        
        private void OnClientDeliveredMessage(UnionDataList message)
        {
            var handler = _clientHandler;

            if (handler != null)
                handler(message);
            else
                message.Release();
        }

        private void OnServerDeliveredMessage(UnionDataList message)
        {
            var handler = _serverHandler;

            if (handler != null)
                handler(_clientEp, message);
            else
                message.Release();
        }

        public SendResult SendToClient(UnionDataList message)
        {
            if (_disposed)
            {
                message.Release();
                return SendResult.NotConnected;
            }

            _clientDeliverySystem.Deliver(message);
            return SendResult.Ok;
        }

        public SendResult SendToServer(UnionDataList message)
        {
            if (_disposed)
            {
                message.Release();
                return SendResult.NotConnected;
            }

            _serverDeliverySystem.Deliver(message);
            return SendResult.Ok;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            var clientClosed = Interlocked.Exchange(ref _clientClosed, null);
            var serverClosed = Interlocked.Exchange(ref _serverClosed, null);

            var oldClient = Interlocked.Exchange(ref _clientDeliverySystem, new PerfectDeliverySystem());
            oldClient.Delivered -= OnClientDeliveredMessage;
            oldClient.Clear();

            var oldServer = Interlocked.Exchange(ref _serverDeliverySystem, new PerfectDeliverySystem());
            oldServer.Delivered -= OnServerDeliveredMessage;
            oldServer.Clear();

            _clientHandler = null;
            _serverHandler = null;

            clientClosed?.Invoke();
            serverClosed?.Invoke();
        }
    }
}
