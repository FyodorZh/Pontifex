using System;
using NewProtocol;
using NewProtocol.Client;
using Shared;
using Shared.Buffer;
using Transport;
using Transport.Abstractions.Clients;

namespace TransportStressTest
{
    public class ClientSideStressTest : AckRawClientProtocol
    {
        private readonly int mBlockSize;
        private readonly int mSendPeriod;

        private StressTestProtocol mProtocol;
        private bool mIsConnected;

        private DateTime mNextSendTime;

        public ClientSideStressTest(IAckRawClient transport, int blockSize, int sendPeriod)
            : base(transport, new VoidHashDB(), NowDateTimeProvider.Instance)
        {
            mBlockSize = blockSize;
            mSendPeriod = sendPeriod;
        }

        protected override Protocol ConstructProtocol()
        {
            mProtocol = new StressTestProtocol();
            mProtocol.Response.Register(OnResponse);
            return mProtocol;
        }

        protected override byte[] GetAckData()
        {
            return new byte[] {1, 2, 3};
        }

        protected override bool OnConnecting(bool protocolIsValid, ByteArraySegment ackResponse)
        {
            return true;
        }

        protected override void OnConnected()
        {
            base.OnConnected();
            mIsConnected = true;
        }

        protected override void OnStopped(StopReason reason)
        {
            mIsConnected = false;
            Log.i("Client.Stopped with reason {@reason}", reason.Print());
        }


        protected override void OnTick(DateTime now)
        {
            if (mIsConnected)
            {
                if (now > mNextSendTime)
                {
                    mNextSendTime = now.AddMilliseconds(mSendPeriod);

                    using (var bufferAccessor = ConcurrentUsageMemoryBufferPool.Instance.Allocate().ExposeAccessorOnce())
                    {
                        var array = CollectableByteArraySegmentWrapper.Construct(mBlockSize);

                        byte[] bytes = array.Array;
                        int offset = array.Offset;
                        for (int i = 0; i < mBlockSize; ++i)
                        {
                            bytes[offset + i] = (byte)i;
                        }

                        bufferAccessor.Buffer.PushAbstractArray(array);

                        mProtocol.Request.Send(bufferAccessor.Acquire());
                    }
                }
            }
            base.OnTick(now);
        }

        private void OnResponse(IMemoryBufferHolder buffer)
        {
            //buffer => { Log.i("Client.Received"); });
        }
    }
}