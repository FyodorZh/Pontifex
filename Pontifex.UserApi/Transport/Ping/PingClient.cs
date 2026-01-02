using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Shared;
using Shared.Buffer;
using Shared.Pooling;
using Shared.Utils;
using Transport;
using Transport.Abstractions;
using Transport.Abstractions.Endpoints.Client;
using Transport.Abstractions.Handlers.Client;
using TimeSpan = System.TimeSpan;

namespace NewProtocol.Ping
{
    public class PingClient: INoAckUnreliableRRClientHandler, IPeriodicLogic
    {
        public struct PingInfo
        {
            public bool IsLost;
            public TimeSpan Dt;
            public DateTime RemoteTime;
        }

        private struct SentPacketInfo
        {
            public int Id;
            public DateTime SentTime;
        }

        private struct ReceivedPacketInfo
        {
            public int Id;
            public DateTime ReceivedTime;
            public DateTime RemoteTime;
        }


        private readonly Transport.Abstractions.Clients.INoAckUnreliableRRClient mTransport;
        private readonly DeltaTime mTimeOut;

        private volatile INoAckUnreliableRRServerEndpoint mEndpoint;

        private ILogicDriverCtl mDriver;

        private DateTime mLastSentTime;
        private int mPingId;

        private SingleUsageMemoryBufferPool mBufferPool;

        private ILogger Log = global::Log.StaticLogger;

        private readonly List<SentPacketInfo> mSentList = new List<SentPacketInfo>();
        private readonly IConcurrentQueue<ReceivedPacketInfo> mReceiveList = new TinyConcurrentQueue<ReceivedPacketInfo>();

        private readonly MessageIdSource mMessageIdSource = new MessageIdSource();

        public event Action<PingInfo> OnPing;

        public DeltaTime PingPeriod { get; set; }
        public int MessageSize { get; set; }

        public event Action<int> OnMessageReceived;

        public PingClient(Transport.Abstractions.Clients.INoAckUnreliableRRClient transport, DeltaTime pingPeriod, DeltaTime timeOut)
        {
            if (transport != null && transport.Init(this))
            {
                mTransport = transport;
            }

            mTimeOut = timeOut;

            PingPeriod = pingPeriod;
            MessageSize = 32;
        }

        public bool Start(ILogger logger)
        {
            Log = logger;
            if (mTransport != null)
            {
                mBufferPool = new SingleUsageMemoryBufferPool(MemoryBufferConstructor.Instance, logger);
                return mTransport.Start(null, logger);
            }

            return false;
        }

        public void Stop()
        {
            if (mTransport != null)
            {
                mTransport.Stop();
            }
            else
            {
                StopLogic();
            }
        }

        void INoAckUnreliableRRClientHandler.Started(INoAckUnreliableRRServerEndpoint endpoint)
        {
            mEndpoint = endpoint;
            var driver = new PeriodicLogicThreadedDriver(DeltaTime.FromMiliseconds(1), 128);
            driver.Start(this, Log);
        }

        void INoAckUnreliableRRClientHandler.Received(Message data)
        {
            IMemoryBufferHolder bufferHolder;
            if (mBufferPool.AllocateAndDeserialize(data.Data, out bufferHolder))
            {
                data.Release();
                using (var bufferAccessor = bufferHolder.ExposeAccessorOnce())
                {
                    var buffer = bufferAccessor.Buffer;
                    int receiveSize = buffer.Size;

                    int id;
                    long remoteTime;

                    if (buffer.PopFirst().AsInt32(out id) &&
                        buffer.PopFirst().AsInt64(out remoteTime))
                    {
                        mReceiveList.Put(new ReceivedPacketInfo() {Id = id, ReceivedTime = HighResDateTime.UtcNow, RemoteTime = DateTime.FromBinary(remoteTime)});

                        var onReceived = OnMessageReceived;
                        if (onReceived != null)
                        {
                            onReceived(receiveSize);
                        }

                        return;
                    }
                    Log.e("Ping protocol fail");
                }
            }
            else
            {
                data.Release();
                Log.e("Failed to deserialize ping message");
            }

            var driver = mDriver;
            if (driver != null)
            {
                driver.Stop();
            }
        }

        void INoAckUnreliableRRClientHandler.Stopped()
        {
            mEndpoint = null;
            var driver = mDriver;
            if (driver != null)
            {
                driver.Stop();
            }
        }

        bool IPeriodicLogic.LogicStarted(ILogicDriverCtl driver)
        {
            mDriver = driver;
            return true;
        }

        void IPeriodicLogic.LogicTick()
        {
            var now = HighResDateTime.UtcNow;

            ReceivedPacketInfo receivedPacket;
            while (mReceiveList.TryPop(out receivedPacket))
            {
                int count = mSentList.Count;
                for (int i = 0; i < count; ++i)
                {
                    if (mSentList[i].Id == receivedPacket.Id)
                    {
                        PingInfo info = new PingInfo()
                        {
                            IsLost = false,
                            RemoteTime = receivedPacket.RemoteTime,
                            Dt = receivedPacket.ReceivedTime - mSentList[i].SentTime
                        };

                        var onPing = OnPing;
                        if (onPing != null)
                        {
                            onPing(info);
                        }

                        mSentList[i] = mSentList[count - 1];
                        mSentList.RemoveAt(count - 1);

                        break;
                    }
                }
            }

            for (int i = 0; i < mSentList.Count; ++i)
            {
                if ((now - mSentList[i].SentTime).TotalMilliseconds > mTimeOut.MilliSeconds)
                {
                    PingInfo info = new PingInfo()
                    {
                        IsLost = true
                    };

                    var onPing = OnPing;
                    if (onPing != null)
                    {
                        onPing(info);
                    }

                    mSentList[i] = mSentList[mSentList.Count - 1];
                    mSentList.RemoveAt(mSentList.Count - 1);
                    --i;
                }
            }

            if ((now - mLastSentTime).TotalMilliseconds > PingPeriod.MilliSeconds)
            {
                mLastSentTime = now;
                var endPoint = mEndpoint;
                if (endPoint != null)
                {
                    using (var bufferAccessor = ConcurrentUsageMemoryBufferPool.Instance.Allocate().ExposeAccessorOnce())
                    {
                        int id = mPingId++;

                        bufferAccessor.Buffer.PushInt64(now.ToBinary());
                        bufferAccessor.Buffer.PushInt32(id);

                        int dsize = MessageSize - bufferAccessor.Buffer.Size;
                        if (dsize > 0)
                        {
                            var array = CollectableByteArraySegmentWrapper.Construct(dsize);
                            bufferAccessor.Buffer.PushAbstractArray(array, false);
                        }

                        var owner = ConcurrentPools.Acquire<CollectableMacroOwner<Message>>();
                        owner.Put(new Message(mMessageIdSource.GenNext(), bufferAccessor.Acquire()));
                        if (endPoint.Send(owner) != SendResult.Ok)
                        {
                            StopLogic();
                        }
                        else
                        {
                            mSentList.Add(new SentPacketInfo(){Id = id, SentTime = now});
                        }
                    }
                }
            }
        }

        void IPeriodicLogic.LogicStopped()
        {
            mDriver = null;
            if (mTransport != null)
            {
                mTransport.Stop();
            }
        }

        private void StopLogic()
        {
            var driver = mDriver;
            if (driver != null)
            {
                driver.Stop();
            }
        }
    }
}