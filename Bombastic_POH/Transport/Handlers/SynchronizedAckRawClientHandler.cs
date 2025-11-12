using System;
using Shared;
using Transport.Abstractions.Endpoints.Client;
using Transport.Abstractions.Handlers;
using Transport.Abstractions.Handlers.Client;
using Shared.Buffer;
using Shared.Concurrent;

namespace Transport.Handlers
{
    /// <summary>
    /// Враппер над клиентским хендлером. Делает взаимодействие однопоточным, кроме вызова  GetAckData()
    /// </summary>
    public class SynchronizedAckRawClientHandler : IAckRawClientHandler
    {
        private readonly IAckRawClientHandler mHandler;
        private readonly ConcurrentQueueValve<IMemoryBufferHolder> mReceivedDataQueue;

        private readonly Intention mBufferOverflowIntention = new Intention();
        private readonly Action mOnBufferOverflow;

        private bool mDisconnectServiced = true;
        private StopReason mDisconnectReason;

        private IAckRawServerEndpoint mNotServicedConnectedEndPoint;
        private ByteArraySegment mAckResponse;

        private bool mStopServiced = true;
        private StopReason mStopReason;

        public SynchronizedAckRawClientHandler(IAckRawClientHandler handler, Action onBufferOverflow)
        {
            if (handler == null)
            {
                throw new ArgumentNullException("handler");
            }

            mReceivedDataQueue = new ConcurrentQueueValve<IMemoryBufferHolder>(
                new LimitedConcurrentQueue<IMemoryBufferHolder>(500),
                holder => holder.Release(),
                holder => holder.Release());

            mOnBufferOverflow = onBufferOverflow;

            mHandler = handler;
        }

        /// <summary>
        /// Не однопоточный
        /// </summary>
        /// <returns></returns>
        byte[] IAckHandler.GetAckData()
        {
            return mHandler.GetAckData();
        }

        void IAckRawClientHandler.OnConnected(IAckRawServerEndpoint endPoint, ByteArraySegment ackResponse)
        {
            mNotServicedConnectedEndPoint = endPoint;
            mAckResponse = ackResponse;
        }

        void IRawBaseHandler.OnDisconnected(StopReason reason)
        {
            mDisconnectServiced = false;
            mDisconnectReason = reason;
            mReceivedDataQueue.CloseValve();
        }

        void IAckRawClientHandler.OnStopped(StopReason reason)
        {
            mStopServiced = false;
            mStopReason = reason;
            mReceivedDataQueue.CloseValve();
        }

        void IRawBaseHandler.OnReceived(IMemoryBufferHolder receivedBuffer)
        {
            if (!mReceivedDataQueue.Put(receivedBuffer))
            {
                mReceivedDataQueue.CloseValve();
                mBufferOverflowIntention.Set();
            }
        }

        public void Service()
        {
            if (mBufferOverflowIntention.TryToRealize())
            {
                mOnBufferOverflow();
            }

            ServiceConnected();
            ServiceReceived();
            ServiceDisconnect();
            ServiceStop();
        }

        private void ServiceConnected()
        {
            if (mNotServicedConnectedEndPoint != null)
            {
                mHandler.OnConnected(mNotServicedConnectedEndPoint, mAckResponse);
                mNotServicedConnectedEndPoint = null;
                mAckResponse = new ByteArraySegment();
            }
        }

        private void ServiceReceived()
        {
            IMemoryBufferHolder buffer;
            while (mReceivedDataQueue.TryPop(out buffer))
            {
                mHandler.OnReceived(buffer);
            }
        }

        private void ServiceDisconnect()
        {
            if (!mDisconnectServiced)
            {
                mHandler.OnDisconnected(mDisconnectReason);
                mDisconnectServiced = true;
            }
        }

        private void ServiceStop()
        {
            if (!mStopServiced)
            {
                mHandler.OnStopped(mStopReason);
                mStopServiced = true;
            }
        }
    }
}
