using System;
using Actuarius.Collections;
using Actuarius.ConcurrentPrimitives;
using Actuarius.Memory;
using Pontifex.Utils;
using Transport.Abstractions.Endpoints.Client;
using Transport.Abstractions.Handlers;
using Transport.Abstractions.Handlers.Client;

namespace Transport.Handlers
{
    /// <summary>
    /// Враппер над клиентским хендлером. Делает взаимодействие однопоточным из подконтрольного треда,
    /// кроме вызова  GetAckData()
    /// </summary>
    public class SynchronizedAckRawClientHandler : IAckRawClientHandler
    {
        private readonly IAckRawClientHandler _handler;
        private readonly ConcurrentQueueValve<UnionDataList> _receivedDataQueue;

        private readonly Intention _bufferOverflowIntention = new Intention();
        private readonly Action _onBufferOverflow;

        private bool _disconnectServiced = true;
        private StopReason? _disconnectReason;

        private IAckRawServerEndpoint? _notServicedConnectedEndPoint;
        private UnionDataList? _ackResponse;

        private bool _stopServiced = true;
        private StopReason? _stopReason;

        public SynchronizedAckRawClientHandler(IAckRawClientHandler handler, Action onBufferOverflow)
        {
            _receivedDataQueue = new ConcurrentQueueValve<UnionDataList>(
                new LimitedConcurrentQueue<UnionDataList>(500),
                holder => holder.Release(),
                holder => holder.Release());

            _onBufferOverflow = onBufferOverflow;

            _handler = handler;
        }

        /// <summary>
        /// Не однопоточный
        /// </summary>
        /// <returns></returns>
        void IAckHandler.WriteAckData(UnionDataList ackData)
        {
            _handler.WriteAckData(ackData);
        }

        void IAckRawClientHandler.OnConnected(IAckRawServerEndpoint endPoint, UnionDataList ackResponse)
        {
            _notServicedConnectedEndPoint = endPoint;
            _ackResponse = ackResponse;
        }

        void IRawBaseHandler.OnDisconnected(StopReason reason)
        {
            _disconnectServiced = false;
            _disconnectReason = reason;
            _receivedDataQueue.CloseValve();
        }

        void IAckRawClientHandler.OnStopped(StopReason reason)
        {
            _stopServiced = false;
            _stopReason = reason;
            _receivedDataQueue.CloseValve();
        }

        void IRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
        {
            if (!_receivedDataQueue.Put(receivedBuffer))
            {
                _receivedDataQueue.CloseValve();
                _bufferOverflowIntention.Set();
            }
        }

        public void Service()
        {
            if (_bufferOverflowIntention.TryToRealize())
            {
                _onBufferOverflow();
            }

            ServiceConnected();
            ServiceReceived();
            ServiceDisconnect();
            ServiceStop();
        }

        private void ServiceConnected()
        {
            if (_notServicedConnectedEndPoint != null)
            {
                _handler.OnConnected(_notServicedConnectedEndPoint, _ackResponse!);
                _notServicedConnectedEndPoint = null;
                _ackResponse = null;
            }
        }

        private void ServiceReceived()
        {
            while (_receivedDataQueue.TryPop(out var buffer))
            {
                _handler.OnReceived(buffer);
            }
        }

        private void ServiceDisconnect()
        {
            if (!_disconnectServiced)
            {
                _handler.OnDisconnected(_disconnectReason!);
                _disconnectServiced = true;
            }
        }

        private void ServiceStop()
        {
            if (!_stopServiced)
            {
                _handler.OnStopped(_stopReason!);
                _stopServiced = true;
            }
        }

        public void Setup(IMemoryRental memory, ILogger logger)
        {
            _handler.Setup(memory, logger);
        }
    }
}
