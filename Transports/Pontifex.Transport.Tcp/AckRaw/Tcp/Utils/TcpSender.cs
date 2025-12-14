using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Pontifex.Transports.Tcp
{
    /// <summary>
    /// Отсылает ассинхронно в сокет
    /// </summary>
    internal class TcpSender
    {
        private readonly Socket mSocket;
        private readonly Queue<Packet> mQueueToSend = new Queue<Packet>();

        private bool mSendingNow; // !volatile but synchronized

        private Action<Exception> mOnFailed;

        private volatile bool mStopped;
        private Action mOnStopped;
        private bool mIntentionToStop;
        private readonly object mStopLock = new object();

        private byte[] mBufferToSend = new byte[1024];

        private SocketAsyncEventArgs mAsyncArgs = new SocketAsyncEventArgs();
        private volatile PacketType mCurrentMessageType;

        public TcpSender(Socket socket, Action<Exception> onFailed)
        {
            mSocket = socket;
            mOnFailed = onFailed;
            mAsyncArgs.Completed += SendCallback;
            mAsyncArgs.SocketFlags = SocketFlags.None;
        }

        public void Stop(Action onStopped)
        {
            lock (mStopLock)
            {
                if (!mIntentionToStop)
                {
                    if (mStopped)
                    {
                        onStopped();
                    }
                    else
                    {
                        mOnStopped = onStopped;
                        Send(new Packet(PacketType.Disconnect, ConcurrentUsageMemoryBufferPool.Instance.Allocate()));
                    }
                    mIntentionToStop = true;
                }
            }
        }

        public SendResult Send(Packet packet)
        {
            using (var bufferAccessor = packet.Buffer.Acquire().ExposeAccessorOnce())
            {
                if (bufferAccessor.Buffer.Size + (sizeof(int) + sizeof(byte) * 2) * 3 > TcpInfo.MessageMaxByteSize) // *3 just to be sure
                {
                    return SendResult.MessageToBig;
                }
            }

            lock (mStopLock)
            {
                if (mStopped || mIntentionToStop)
                {
                    return SendResult.Error;
                }
            }

            bool haveToSendNow;
            lock (mQueueToSend)
            {
                haveToSendNow = !mSendingNow;
                if (!haveToSendNow)
                {
                    mQueueToSend.Enqueue(packet);
                }
                else
                {
                    mSendingNow = true;
                }
            }

            if (haveToSendNow)
            {
                return DoSend(packet);
            }
            return SendResult.Ok;
        }

        private void SendCallback(object sender, SocketAsyncEventArgs args)
        {
            try
            {
                if (mCurrentMessageType == PacketType.Disconnect)
                {
                    lock (mStopLock)
                    {
                        mStopped = true;
                        if (mOnStopped != null)
                        {
                            mOnStopped();
                        }

                        DisposeAsyncEventArgs();
                    }
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
                return;
            }

            if (!mStopped)
            {
                Packet packet = new Packet();
                lock (mQueueToSend)
                {
                    if (mQueueToSend.Count > 0)
                    {
                        packet = mQueueToSend.Dequeue();
                    }
                    else
                    {
                        mSendingNow = false;
                    }
                }

                if (mSendingNow)
                {
                    DoSend(packet);
                }
            }
            else
            {
                mSendingNow = false;
            }
        }

        private SendResult DoSend(Packet packet)
        {
            try
            {
                int size = PacketCompositor.EncodePacketTo(packet, ref mBufferToSend);
                mAsyncArgs.SetBuffer(mBufferToSend, 0, size);

                mCurrentMessageType = packet.Type;
                if (!mSocket.SendAsync(mAsyncArgs))
                {
                    SendCallback(mSocket, mAsyncArgs);
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
                return SendResult.Error;
            }
            return SendResult.Ok;
        }

        private void Fail(Exception ex)
        {
            lock (mStopLock)
            {
                if (!mStopped)
                {
                    mStopped = true;
                    if (mOnStopped != null)
                    {
                        mOnStopped();
                    }

                    DisposeAsyncEventArgs();
                }
            }

            var failedHandler = System.Threading.Interlocked.Exchange(ref mOnFailed, null);
            if (failedHandler != null)
            {
                failedHandler(ex);
            }
        }

        private void DisposeAsyncEventArgs()
        {
            if (mAsyncArgs != null)
            {
                mAsyncArgs.Dispose();
                mAsyncArgs = null;
            }
        }
    }
}
