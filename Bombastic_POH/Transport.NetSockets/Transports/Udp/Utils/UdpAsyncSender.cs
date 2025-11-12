using System;
using System.Net;
using System.Net.Sockets;
using System.Security;
using Shared;
using Transport.Abstractions;
using Transport.Abstractions.Controls;
using Transport.Utils;

namespace Transport.Transports.Udp
{
    internal class UdpAsyncSender : PeriodicLogic
    {
        private class SendTask
        {
            private readonly MemoryChunkEncoder mEncoder;

            public EndPoint Address { get; set; }

            public SendTask(int capacity)
            {
                mEncoder = new MemoryChunkEncoder(capacity + 2 + 4);
            }

            public bool TryPush(MessageId id, IByteArray data)
            {
                return mEncoder.TryPush(id, data);
            }

            public bool IsEmpty
            {
                get { return mEncoder.IsEmpty; }
            }

            public void Clear()
            {
                Address = null;
                mEncoder.Clear();
            }

            public ByteArraySegment ShowData()
            {
                return mEncoder.ShowData();
            }

            public override string ToString()
            {
                return Address + ": " + mEncoder.ShowData();
            }
        }

        private readonly Socket mSocket;
        private readonly int mMaxMessageSize;
        private readonly Action<SocketException> mOnSocketFailed;
        private readonly ITrafficCollectorSink mTrafficCollector;

        private readonly IConcurrentQueue<SendTask> mQueueToSend = new LimitedConcurrentQueue<SendTask>(10000);

        private readonly IConcurrentQueue<SendTask> mTaskPool = new LimitedConcurrentQueue<SendTask>(10000);

        public UdpAsyncSender(Socket socket, int maxMessageSize, Action<SocketException> onSocketFailed, ITrafficCollectorSink trafficCollector)
            : base(5)
        {
            mSocket = socket;
            mMaxMessageSize = maxMessageSize;
            mOnSocketFailed = onSocketFailed;
            mTrafficCollector = trafficCollector;
        }

        protected override void LogicTick()
        {
            SendTask task;
            while (IsStarted && mQueueToSend.TryPop(out task))
            {
                try
                {
                    var data = task.ShowData();
                    var sent = mSocket.SendTo(data.Array, data.Offset, data.Count, SocketFlags.None, task.Address);
                    mTrafficCollector.IncOutTraffic(sent);
                    if (sent != data.Count)
                    {
                        Log.e("Udp.Sender: Failed to send {0} bytes. Only {1} bytes were sent", data.Count, sent);
                    }
                }
                catch (ArgumentNullException ex)
                {
                    Log.wtf(ex);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    Log.wtf(ex);
                }
                catch (ObjectDisposedException)
                {
                    Stop();
                }
                catch (SecurityException ex)
                {
                    Log.wtf(ex);
                    Stop();
                }
                catch (SocketException ex)
                {
                    Log.wtf("SocketException with code " + ex.ErrorCode, ex);
                    mOnSocketFailed(ex);
                }
                finally
                {
                    FreeTask(task);
                }
            }
        }

        public SendResult Send(Message message, EndPoint remoteEP)
        {
            try
            {
                if (remoteEP == null)
                {
                    return SendResult.InvalidAddress;
                }
                if (message.Data.Count > mMaxMessageSize)
                {
                    return SendResult.MessageToBig;
                }

                SendTask task = GetTask(remoteEP);
                if (task.TryPush(message.Id, message.Data))
                {
                    if (!mQueueToSend.Put(task))
                    {
                        FreeTask(task);
                        return SendResult.BufferOverflow;
                    }

                    return SendResult.Ok;
                }
                FreeTask(task);
                return SendResult.Error;
            }
            finally
            {
                message.Release();
            }
        }

        public SendResult Send(IMacroOwner<Message> messages, EndPoint remoteEP)
        {
            if (remoteEP == null)
            {
                return SendResult.InvalidAddress;
            }

            try
            {
                SendTask task = null;

                foreach (var msg in messages.Enumerate())
                {
                    if (msg.Data.Count > mMaxMessageSize)
                    {
                        return SendResult.MessageToBig;
                    }

                    if (task == null)
                    {
                        task = GetTask(remoteEP);
                    }

                    if (!task.TryPush(msg.Id, msg.Data))
                    {
                        if (!mQueueToSend.Put(task))
                        {
                            FreeTask(task);
                            return SendResult.BufferOverflow;
                        }
                        task = GetTask(remoteEP);
                        if (!task.TryPush(msg.Id, msg.Data))
                        {
                            FreeTask(task);
                            return SendResult.Error;
                        }
                    }
                }

                if (task != null)
                {
                    if (!task.IsEmpty)
                    {
                        if (!mQueueToSend.Put(task))
                        {
                            FreeTask(task);
                            return SendResult.BufferOverflow;
                        }
                    }
                    else
                    {
                        FreeTask(task);
                    }
                }

                return SendResult.Ok;
            }
            finally
            {
                messages.Release();
            }
        }

        private SendTask GetTask(EndPoint address)
        {
            SendTask task;
            if (!mTaskPool.TryPop(out task))
            {
                task = new SendTask(mMaxMessageSize + 2);
            }
            task.Address = address;
            return task;
        }

        private void FreeTask(SendTask task)
        {
            task.Clear();
            if (!mTaskPool.Put(task))
            {
                Log.w("Udp.FreeTasks.Pool overflow");
            }
        }
    }
}
