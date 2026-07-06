using System;
using Actuarius.Collections;
using Actuarius.Concurrent;
using Pontifex.Ack;
using Pontifex.Ack.Raw;
using Pontifex.Utils;

namespace Pontifex.Handlers
{
    /// <summary>
    /// Враппер над клиентским хендлером. Делает взаимодействие однопоточным из подконтрольного треда,
    /// кроме вызова  GetAckData()
    /// </summary>
    public class SynchronizedAckRawClientHandler : IAckRawReliableClientHandler
    {
        private readonly IAckRawReliableClientHandler _handler;
        private readonly ConcurrentQueueValve<UnionDataList> _receivedDataQueue;

        private readonly Intention _bufferOverflowIntention = new Intention();
        private readonly Action _onBufferOverflow;

        private bool _disconnectServiced = true;
        private StopReason? _disconnectReason;

        private IAckRawReliableClientSideEndpoint? _notServicedConnectedEndPoint;
        private UnionDataList? _ackResponse;

        private bool _stopServiced = true;
        private StopReason? _stopReason;

        public SynchronizedAckRawClientHandler(IAckRawReliableClientHandler handler, Action onBufferOverflow)
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
        void IAckRawClientHandler.FillAckData(UnionDataList ackData)
        {
            _handler.FillAckData(ackData);
        }

        void IAckRawReliableClientHandler.OnConnected(IAckRawReliableClientSideEndpoint endPoint, UnionDataList ackResponse)
        {
            _notServicedConnectedEndPoint = endPoint;
            _ackResponse = ackResponse;
        }

        void IAckRawBaseHandler.OnDisconnected(StopReason reason)
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

        void IAckRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
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
                _handler.OnStopped(reason: _stopReason!);
                _stopServiced = true;
            }
        }
    }
}
