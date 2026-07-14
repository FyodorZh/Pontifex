using System;
using System.Threading;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;

namespace Pontifex.NoAck.Raw.Direct
{
    public sealed class Channel : IDisposable
    {
        private readonly IEndPoint _clientEp;
        private readonly IEndPoint _serverEp;
        private volatile Action<UnionDataList>? _clientHandler;
        private volatile Action<IEndPoint, UnionDataList>? _serverHandler;
        private volatile IDeliverySystem? _clientDeliverySystem;
        private volatile IDeliverySystem? _serverDeliverySystem;
        private volatile bool _disposed;

        public Channel(IEndPoint clientEp, IEndPoint serverEp)
        {
            _clientEp = clientEp;
            _serverEp = serverEp;
        }

        public IEndPoint ClientEp => _clientEp;

        public Action<UnionDataList>? ClientHandler
        {
            set => _clientHandler = value;
        }

        public Action<IEndPoint, UnionDataList>? ServerHandler
        {
            set => _serverHandler = value;
        }

        /// <summary>
        /// It is possible and acceptable for messages that are processed right now to be undelivered.
        /// The most important invariant is to release messages.
        /// </summary>
        public void SetDeliverySystem(IDeliverySystem? clientDeliverySystem, IDeliverySystem? serverDeliverySystem)
        {
            if (clientDeliverySystem != _clientDeliverySystem)
            {
                if (clientDeliverySystem != null)
                    clientDeliverySystem.Delivered += OnClientDeliveredMessage;

                var oldClient = Interlocked.Exchange(ref _clientDeliverySystem, clientDeliverySystem);
                if (oldClient != null)
                {
                    oldClient.Delivered -= OnClientDeliveredMessage;
                    oldClient.Clear();
                }
            }

            if (serverDeliverySystem != _serverDeliverySystem)
            {
                if (serverDeliverySystem != null)
                    serverDeliverySystem.Delivered += OnServerDeliveredMessage;

                var oldServer = Interlocked.Exchange(ref _serverDeliverySystem, serverDeliverySystem);
                if (oldServer != null)
                {
                    oldServer.Delivered -= OnServerDeliveredMessage;
                    oldServer.Clear();
                }
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

            var ds = _clientDeliverySystem;
            if (ds != null)
            {
                ds.Deliver(message);
                return SendResult.Ok;
            }

            var handler = _clientHandler;
            if (handler != null)
            {
                handler(message);
                return SendResult.Ok;
            }

            message.Release();
            return SendResult.Error;
        }

        public SendResult SendToServer(UnionDataList message)
        {
            if (_disposed)
            {
                message.Release();
                return SendResult.NotConnected;
            }

            var ds = _serverDeliverySystem;
            if (ds != null)
            {
                ds.Deliver(message);
                return SendResult.Ok;
            }

            var handler = _serverHandler;
            if (handler != null)
            {
                handler(_clientEp, message);
                return SendResult.Ok;
            }

            message.Release();
            return SendResult.Error;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            var oldClient = Interlocked.Exchange(ref _clientDeliverySystem, null);
            if (oldClient != null)
            {
                oldClient.Delivered -= OnClientDeliveredMessage;
                oldClient.Clear();
            }

            var oldServer = Interlocked.Exchange(ref _serverDeliverySystem, null);
            if (oldServer != null)
            {
                oldServer.Delivered -= OnServerDeliveredMessage;
                oldServer.Clear();
            }

            _clientHandler = null;
            _serverHandler = null;
        }
    }
}
