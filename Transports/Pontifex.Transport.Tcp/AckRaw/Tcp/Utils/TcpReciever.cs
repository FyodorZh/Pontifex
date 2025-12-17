using System;
using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.Transports.Tcp
{
    internal class TcpReceiver
    {
        private volatile bool _stopped;

        private readonly Socket _socket;
        private readonly UnionDataListCompositor _packetCompositor;

        private Action<Exception> _onFailed;
        private readonly Action _onStopped;

        private volatile SocketAsyncEventArgs _asyncArgs = new SocketAsyncEventArgs();

        private int _wasStopped;

        public TcpReceiver(Socket socket, Action<UnionDataList> onReceived, Action<Exception> onFailed, Action onStopped, IMemoryRental memoryRental)
        {
            if (onReceived == null)
            {
                throw new ArgumentNullException(nameof(onReceived));
            }
            if (onFailed == null)
            {
                throw new ArgumentNullException(nameof(onFailed));
            }

            byte[] buffer = new byte[1024 * 8];

            _asyncArgs.Completed += ReadCallback;
            _asyncArgs.SocketFlags = SocketFlags.None;
            _asyncArgs.SetBuffer(buffer, 0, buffer.Length);

            _socket = socket;

            _onFailed = onFailed;
            _onStopped = onStopped;

            _packetCompositor = new UnionDataListCompositor(onReceived, memoryRental.CollectablePool, memoryRental.ByteArraysPool);
        }

        public void Start()
        {
            try
            {
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
            if (System.Threading.Interlocked.Exchange(ref _wasStopped, 1) == 0)
            {
                if (_asyncArgs != null)
                {
                    _asyncArgs.Dispose();
                    _asyncArgs = null;
                }

                _packetCompositor.Dispose();

                if (_onStopped != null)
                {
                    _onStopped();
                }
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
                        _packetCompositor.PushData(args.Buffer, args.Offset, args.BytesTransferred);

                        // show must go on
                        if (!_stopped)
                        {
                            isSyncWork = !_socket.ReceiveAsync(_asyncArgs);
                            sender = _socket;
                            args = _asyncArgs;
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
