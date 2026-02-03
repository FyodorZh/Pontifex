using System;
using System.Net.Sockets;
using System.Threading;
using Actuarius.Collections;
using Actuarius.Memory;
using Scriba;

namespace Pontifex.Transports.Tcp
{
    internal class LowLevelTcpSender
    {
        private readonly Socket _socket;
        private readonly IProducer<IMultiRefByteArray> _dataToSendSource;

        private SocketAsyncEventArgs? _asyncArgs;

        private IMultiRefByteArray? _currentBuffer;


        private int _countToRun = -1;

        private int _destroyed;

        private readonly ILogger Log;

        public event Action? ChainStopped;
        public event Action<Exception>? ErrorOccured;

        public LowLevelTcpSender(Socket socket, IProducer<IMultiRefByteArray> dataToSendSource, ILogger logger)
        {
            _socket = socket;
            _dataToSendSource = dataToSendSource;
            _asyncArgs = new SocketAsyncEventArgs();
            _asyncArgs.Completed += SendCallback;
            _asyncArgs.SocketFlags = SocketFlags.None;
            Log = logger;
        }

        public void Run()
        {
            if (Interlocked.CompareExchange(ref _countToRun, 100, -1) == -1)
            {
                if (!_dataToSendSource.TryPop(out var buffer))
                {
                    Interlocked.Exchange(ref _countToRun, -1);
                    ChainStopped?.Invoke();
                    return;
                }
                
                DoSend(buffer);
            }
        }

        public bool Destroy()
        {
            if (Interlocked.CompareExchange(ref _destroyed, 1, 0) == 0)
            {
                _asyncArgs?.Dispose();
                _asyncArgs = null;
                return true;
            }

            return false;
        }

        private void DoSend(IMultiRefByteArray buffer)
        {
            _currentBuffer?.Release();
            
            using var disposer = buffer.AsDisposable();
            try
            {
                _currentBuffer = buffer.Acquire();
                _asyncArgs!.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);

                Log.i("Send" + buffer.Count);
                if (!_socket.SendAsync(_asyncArgs))
                {
                    SendCallback(_socket, _asyncArgs);
                }
            }
            catch (Exception ex)
            {
                _currentBuffer?.Release();
                _currentBuffer = null;
                Fail(ex);
            }
        }
        
        private void SendCallback(object sender, SocketAsyncEventArgs args)
        {
            _currentBuffer?.Release();
            _currentBuffer = null;

            _countToRun -= 1;

            if (_countToRun >= 0 && Volatile.Read(ref _destroyed) == 0)
            {
                if (!_dataToSendSource.TryPop(out var buffer))
                {
                    Interlocked.Exchange(ref _countToRun, -1);
                    ChainStopped?.Invoke();
                    return;
                }

                DoSend(buffer);
            }
        }
        
        private void Fail(Exception ex)
        {
            if (Destroy())
            {
                ErrorOccured?.Invoke(ex);
            }
        }
    }
}