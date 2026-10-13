using System;
using System.Net.Sockets;
using System.Threading;
using Actuarius.Collections;
using Actuarius.Memory;

namespace Pontifex.Raw.Reliable.Ack.Tcp
{
    internal class LowLevelTcpSender
    {
        private readonly Socket _socket;
        private readonly IProducer<IMultiRefByteArray> _dataToSendSource;

        private SocketAsyncEventArgs? _asyncArgs;

        private IMultiRefByteArray? _currentBuffer;

        private int _chainState;

        private int _destroyed;

        private readonly object _locker = new();

        public event Action? ChainStopped;
        public event Action<Exception>? ErrorOccured;

        public LowLevelTcpSender(Socket socket, IProducer<IMultiRefByteArray> dataToSendSource)
        {
            _socket = socket;
            _dataToSendSource = dataToSendSource;
            _asyncArgs = new SocketAsyncEventArgs();
            _asyncArgs.Completed += (_, _) => ContinueSendingChain();
            _asyncArgs.SocketFlags = SocketFlags.None;
        }

        public void StartSendingChain()
        {
            if (Interlocked.CompareExchange(ref _chainState, 1, 0) == 0)
            {
                ContinueSendingChain();
            }
        }

        public bool Destroy()
        {
            if (Interlocked.CompareExchange(ref _destroyed, 1, 0) == 0)
            {
                Volatile.Write(ref _chainState, 0);
                _asyncArgs?.Dispose();
                _asyncArgs = null;
                return true;
            }

            return false;
        }

        private void ContinueSendingChain()
        {
            try
            {
                while (Volatile.Read(ref _destroyed) == 0)
                {
                    _currentBuffer?.Release();
                    _currentBuffer = null;
                    if (_dataToSendSource.TryPop(out var buffer))
                    {
                        _currentBuffer = buffer;
                        _asyncArgs!.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
                        if (_socket.SendAsync(_asyncArgs))
                        {
                            break;
                        }
                    }
                    else
                    {
                        Volatile.Write(ref _chainState, 0);
                        ChainStopped?.Invoke();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _currentBuffer?.Release();
                _currentBuffer = null;
                Fail(ex);
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