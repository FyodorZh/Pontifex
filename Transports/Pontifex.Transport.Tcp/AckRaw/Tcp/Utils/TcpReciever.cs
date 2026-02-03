using System;
using System.Net.Sockets;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Transports.Tcp
{
    internal class TcpReceiver
    {
        private volatile bool _stopped;

        private readonly Socket _socket;
        private readonly UnionDataListCompositor _packetCompositor;

        private Action<Exception>? _onFailed;
        private readonly Action? _onStopped;

        private volatile SocketAsyncEventArgs? _asyncArgs = new SocketAsyncEventArgs();

        private int _wasStopped;

        private readonly ILogger Log;

        public TcpReceiver(Socket socket, Action<UnionDataList> onReceived, Action<Exception> onFailed, Action? onStopped, int messageMaxSize, IMemoryRental memoryRental, ILogger logger)
        {
            Log = logger;
            if (onReceived == null)
            {
                throw new ArgumentNullException(nameof(onReceived));
            }
            if (onFailed == null)
            {
                throw new ArgumentNullException(nameof(onFailed));
            }

            byte[] buffer = new byte[socket.SendBufferSize];

            _asyncArgs.Completed += ReadCallback;
            _asyncArgs.SocketFlags = SocketFlags.None;
            _asyncArgs.SetBuffer(buffer, 0, buffer.Length);

            _socket = socket;

            _onFailed = onFailed;
            _onStopped = onStopped;

            _packetCompositor = new UnionDataListCompositor(msg =>
            {
                Log.i("Received MSG SIZE" + (msg?.GetDataSize()?? -1));
                if (msg == null)
                {
                    Fail(new Exception("Failed to decompose packet from stream"));
                    return;
                }
                onReceived(msg);
            }, memoryRental.CollectablePool, memoryRental.ByteArraysPool, messageMaxSize);
        }

        public void Start()
        {
            try
            {
                if (_asyncArgs == null)
                {
                    throw new InvalidOperationException($"{nameof(_asyncArgs)} is null. TcpReceiver already stopped?");
                }
                if (!_socket.ReceiveAsync(_asyncArgs))
                {
                    ReadCallback(_socket, _asyncArgs);
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        public void Stop()
        {
            _stopped = true;
        }

        private void OnStopped()
        {
            if (Interlocked.Exchange(ref _wasStopped, 1) == 0)
            {
                if (_asyncArgs != null)
                {
                    _asyncArgs.Dispose();
                    _asyncArgs = null;
                }

                _packetCompositor.Dispose();

                _onStopped?.Invoke();
            }
        }

        private void ReadCallback(object sender, SocketAsyncEventArgs args)
        {
            bool isSyncWork = true;
            while (isSyncWork)
            {
                isSyncWork = false;
                try
                {
                    if (args.SocketError != SocketError.Success &&
                        args.SocketError != SocketError.WouldBlock)
                    {
                        throw new SocketException((int)args.SocketError);
                    }

                    int bytesRead = args.BytesTransferred;
                    if (bytesRead > 0)
                    {
                        _packetCompositor.PushData(args.Buffer, args.Offset, bytesRead);

                        // Show must go on
                        if (!_stopped)
                        {
                            var asyncArgs = _asyncArgs ?? throw new InvalidOperationException($"{nameof(_asyncArgs)} is null. TcpReceiver already stopped? (2)");
                            isSyncWork = !_socket.ReceiveAsync(asyncArgs);
                            args = asyncArgs;
                        }
                        else
                        {
                            OnStopped();
                        }
                    }
                    else
                    {
                        // Client call Shutdown - No more data will be received
                        Stop();
                        OnStopped();
                    }
                }
                catch (Exception ex)
                {
                    Fail(ex);
                }
            }
        }

        private void Fail(Exception ex)
        {
            Stop();
            var failedHandler = System.Threading.Interlocked.Exchange(ref _onFailed, null);
            if (failedHandler != null)
            {
                failedHandler(ex);
            }
            OnStopped();
        }
    }
}
