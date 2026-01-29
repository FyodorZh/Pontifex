using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Transports.Tcp
{
    /// <summary>
    /// Отсылает ассинхронно в сокет
    /// </summary>
    internal class TcpSender
    {
        private readonly Socket _socket;
        private readonly Queue<UnionDataList> _queueToSend = new Queue<UnionDataList>();
        
        private readonly IMemoryRental _memoryRental;

        private bool _sendingNow; // !volatile but synchronized

        private Action<Exception>? _onFailed;

        private volatile bool _stopped;
        private Action? _onStopped;
        private bool _intentionToStop;
        private readonly object _stopLock = new object();

        private IMultiRefByteArray? _bufferToSend;

        private SocketAsyncEventArgs? _asyncArgs = new SocketAsyncEventArgs();
        private volatile PacketType _currentMessageType;

        private ILogger Log;

        public TcpSender(Socket socket, Action<Exception> onFailed, IMemoryRental memoryRental, ILogger logger)
        {
            _socket = socket;
            _onFailed = onFailed;
            _asyncArgs.Completed += SendCallback;
            _asyncArgs.SocketFlags = SocketFlags.None;

            _memoryRental = memoryRental;
            Log = logger;
        }

        public void Stop(Action onStopped)
        {
            lock (_stopLock)
            {
                if (!_intentionToStop)
                {
                    if (_stopped)
                    {
                        onStopped();
                    }
                    else
                    {
                        _onStopped = onStopped;
                        var packet = _memoryRental.CollectablePool.Acquire<UnionDataList>();
                        packet.PutFirst(new UnionData((byte)PacketType.Disconnect));
                        Send(packet);
                    }
                    _intentionToStop = true;
                }
            }
        }

        public SendResult Send(UnionDataList packet)
        {
            using var packetDisposer = packet.AsDisposable();

            if (packet.PeekFirstType() != UnionDataType.Byte)
            {
                return SendResult.InvalidMessage;
            }

            switch ((PacketType)packet.Elements[0].Alias.ByteValue)
            {
                case PacketType.AckRequest:
                case PacketType.AckResponse:
                case PacketType.Regular:
                case PacketType.Disconnect:
                case PacketType.Ping:
                    break;
                default:
                    return SendResult.InvalidMessage;
            }
            
            if (packet.GetDataSize() > TcpInfo.MessageMaxByteSize)
            {
                return SendResult.MessageToBig;
            }

            lock (_stopLock)
            {
                if (_stopped || _intentionToStop)
                {
                    return SendResult.Error;
                }
            }

            bool haveToSendNow;
            lock (_queueToSend)
            {
                haveToSendNow = !_sendingNow;
                if (!haveToSendNow)
                {
                    _queueToSend.Enqueue(packet.Acquire());
                }
                else
                {
                    _sendingNow = true;
                }
            }

            if (haveToSendNow)
            {
                return DoSend(packet.Acquire());
            }
            return SendResult.Ok;
        }

        private void SendCallback(object sender, SocketAsyncEventArgs args)
        {
            _bufferToSend?.Release();
            _bufferToSend = null;
            
            try
            {
                if (_currentMessageType == PacketType.Disconnect)
                {
                    lock (_stopLock)
                    {
                        _stopped = true;
                        _onStopped?.Invoke();
                        DisposeAsyncEventArgs();
                    }
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
                return;
            }

            if (!_stopped)
            {
                UnionDataList? packet = null;
                lock (_queueToSend)
                {
                    if (_queueToSend.Count > 0)
                    {
                        packet = _queueToSend.Dequeue();
                    }
                    else
                    {
                        _sendingNow = false;
                    }
                }

                if (_sendingNow)
                {
                    DoSend(packet!);
                }
            }
            else
            {
                _sendingNow = false;
            }
        }

        private SendResult DoSend(UnionDataList packet)
        {
            using var packetDisposer = packet.AsDisposable();
            try
            {
                if (_bufferToSend != null)
                {
                    Log.e("Buffer leak detected");
                    _bufferToSend.Release();
                }
                _bufferToSend = UnionDataListCompositor.Encode(packet, _memoryRental.ByteArraysPool);
                
                _asyncArgs!.SetBuffer(_bufferToSend.Array, _bufferToSend.Offset, _bufferToSend.Count);

                _currentMessageType = (PacketType)packet.Elements[0].Alias.ByteValue;
                if (!_socket.SendAsync(_asyncArgs))
                {
                    SendCallback(_socket, _asyncArgs);
                }
            }
            catch (Exception ex)
            {
                _bufferToSend?.Release();
                _bufferToSend = null;
                Fail(ex);
                return SendResult.Error;
            }
            return SendResult.Ok;
        }

        private void Fail(Exception ex)
        {
            lock (_stopLock)
            {
                if (!_stopped)
                {
                    _stopped = true;
                    if (_onStopped != null)
                    {
                        _onStopped();
                    }

                    DisposeAsyncEventArgs();
                }
            }

            var failedHandler = System.Threading.Interlocked.Exchange(ref _onFailed, null);
            if (failedHandler != null)
            {
                failedHandler(ex);
            }
        }

        private void DisposeAsyncEventArgs()
        {
            if (_asyncArgs != null)
            {
                _asyncArgs.Dispose();
                _asyncArgs = null;
            }
        }
    }
}
