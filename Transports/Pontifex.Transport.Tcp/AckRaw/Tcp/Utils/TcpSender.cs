using System;
using System.Net.Sockets;
using Actuarius.Collections;
using Actuarius.Concurrent;
using Actuarius.Memory;
using Operarius;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Transports.Tcp
{
    internal class TcpSender : INonPeriodicLogic 
    {
        [Flags]
        public enum State { Constructed = 1, Connected = 2, Disconnecting = 4, InError = 8, Stopped = 16}
        
        private readonly Socket _socket;
        private readonly int _messageMaxSize;
        private readonly int _messagePartMaxSize;

        private readonly InverseDelegateProducer<IMultiRefByteArray> _bufferProducer;
        private readonly LowLevelTcpSender _lowLevelTcpSender;
        
        private readonly IMemoryRental _memoryRental;
        
        private readonly object _stageLock = new();
        private volatile State _stage = State.Constructed; 

        private INonPeriodicLogicDriverCtl? _driver;
        
        private readonly ConcurrentQueueValve<UnionDataList> _packetsToSend;
        
        public event Action? Disconnected;
        public event Action<Exception>? ErrorOccured;
        public event Action? Stopped;

        public State Stage => _stage;
        
        public TcpSender(Socket socket, int messageMaxSize, int messagePartMaxSize, IMemoryRental memoryRental, ILogger logger)
        {
            _socket = socket;
            _messageMaxSize = messageMaxSize;
            _messagePartMaxSize = messagePartMaxSize;
            _memoryRental = memoryRental;

            _packetsToSend = new ConcurrentQueueValve<UnionDataList>(new SystemConcurrentQueue<UnionDataList>(),
                packet => packet.Release());

            _bufferProducer = new InverseDelegateProducer<IMultiRefByteArray>(ProcessDataToSend);
            _lowLevelTcpSender = new LowLevelTcpSender(socket, _bufferProducer, logger);

            _lowLevelTcpSender.ChainStopped += () =>
            {
                if (_packetsToSend.Count > 0)
                {
                    _driver?.RequestInvocation();
                }
                else if (_stage == State.Disconnecting)
                {
                    Disconnected?.Invoke();
                    _driver?.Stop();
                }
            };
            _lowLevelTcpSender.ErrorOccured += Fail;
        }
        
        bool ILogic<INonPeriodicLogicDriverCtl>.LogicStarted(INonPeriodicLogicDriverCtl driver)
        {
            lock (_stageLock)
            {
                if (_stage != State.Constructed)
                {
                    Fail(new Exception($"Invalid TcpSender.Stage {_stage} in LogicStarted()"));
                    return false;
                }
                _driver = driver;
                _stage = State.Connected;
                driver.RequestInvocation();
                return true;
            }
        }
        
        void INonPeriodicLogic.LogicTick(INonPeriodicLogicDriverCtl driver)
        {
            _lowLevelTcpSender.Run();
        }

        void ILogic<INonPeriodicLogicDriverCtl>.LogicStopped()
        {
            lock (_stageLock)
            {
                _stage = State.Stopped;
            }

            try
            {
                _packetsToSend.CloseValve();
                _lowLevelTcpSender.Destroy();
                while (_bufferProducer.TryPop(out var buffer))
                {
                    buffer.Release();
                }
            }
            catch (Exception ex)
            {
                ErrorOccured?.Invoke(ex);
            }

            Stopped?.Invoke();
        }
        
        private void ProcessDataToSend(IConsumer<IMultiRefByteArray> dst)
        {
            if (_packetsToSend.TryPop(out var packet))
            {
                using var dispose = packet.AsDisposable();
                    
                if (!UnionDataListCompositor.Encode(packet, _memoryRental.ByteArraysPool, _messagePartMaxSize, dst))
                {
                    Fail(new Exception("Failed to encode packet"));
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
                case PacketType.Ping:
                    if (_stage == State.Disconnecting)
                    {
                        return SendResult.Ok;
                    }
                    if (_stage != State.Connected)
                    {
                        return SendResult.NotConnected;
                    }
                    break;
                case PacketType.Disconnect:
                    lock (_stageLock)
                    {
                        if (_stage != State.Disconnecting)
                        {
                            return SendResult.Error;
                        }
                    }
                    break;
                default:
                    return SendResult.InvalidMessage;
            }
            
            if (packet.GetDataSize() > _messageMaxSize)
            {
                return SendResult.MessageToBig;
            }

            switch (_packetsToSend.EnqueueEx(packet.Acquire()))
            {
                case ValveEnqueueResult.Ok:
                    _driver?.RequestInvocation();
                    return SendResult.Ok;
                case ValveEnqueueResult.Overflown:
                    return SendResult.BufferOverflow;
                case ValveEnqueueResult.Rejected:
                    return SendResult.NotConnected;
                default:
                    Fail(new Exception("Impossible error #1"));
                    return SendResult.Error;
            }
        }

        public void GracefulDisconnect()
        {
            bool sendDisconnect = false;
            lock (_stageLock)
            {
                if (_stage == State.Connected)
                {
                    _stage = State.Disconnecting;
                    sendDisconnect = true;
                }
            }

            if (sendDisconnect)
            {
                var packet = _memoryRental.CollectablePool.Acquire<UnionDataList>();
                packet.PutFirst(new UnionData((byte)PacketType.Disconnect));
                Send(packet);
            }
        }

        public void Stop()
        {
            _driver?.Stop();
        }

        private void Fail(Exception ex)
        {
            lock (_stageLock)
            {
                if (_stage != State.Stopped)
                {
                    _stage = State.InError;
                    _driver?.Stop();
                }
            }

            ErrorOccured?.Invoke(ex);
        }
    }
}