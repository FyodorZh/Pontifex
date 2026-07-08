using System;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.NoAck.Raw.Reliable.Direct
{
    internal sealed class Channel : IDisposable
    {
        private readonly object _lock = new();
        private readonly IEndPoint _clientEp;
        private readonly IEndPoint _serverEp;
        private Action<UnionDataList>? _clientHandler;
        private Action<IEndPoint, UnionDataList>? _serverHandler;
        private bool _disposed;

        public Channel(IEndPoint clientEp, IEndPoint serverEp)
        {
            _clientEp = clientEp;
            _serverEp = serverEp;
        }

        public IEndPoint ClientEp => _clientEp;

        public Action<UnionDataList>? ClientHandler
        {
            set
            {
                lock (_lock) { _clientHandler = value; }
            }
        }

        public Action<IEndPoint, UnionDataList>? ServerHandler
        {
            set
            {
                lock (_lock) { _serverHandler = value; }
            }
        }

        public SendResult SendToClient(UnionDataList message)
        {
            Action<UnionDataList>? handler;
            lock (_lock)
            {
                if (_disposed)
                {
                    message.Release();
                    return SendResult.NotConnected;
                }

                handler = _clientHandler;
            }

            if (handler != null)
            {
                using var disposer = message.AsDisposable();
                handler(message.Acquire());
                return SendResult.Ok;
            }

            message.Release();
            return SendResult.Error;
        }

        public SendResult SendToServer(UnionDataList message)
        {
            Action<IEndPoint, UnionDataList>? handler;
            lock (_lock)
            {
                if (_disposed)
                {
                    message.Release();
                    return SendResult.NotConnected;
                }

                handler = _serverHandler;
            }

            if (handler != null)
            {
                using var disposer = message.AsDisposable();
                handler(_clientEp, message.Acquire());
                return SendResult.Ok;
            }

            message.Release();
            return SendResult.Error;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;
                _clientHandler = null;
                _serverHandler = null;
            }
        }
    }
}
