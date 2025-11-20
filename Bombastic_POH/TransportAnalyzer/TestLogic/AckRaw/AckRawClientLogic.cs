using System.Text;
using System.Threading;
using Shared;
using Transport;
using Transport.Abstractions.Endpoints.Client;
using Transport.Abstractions.Handlers.Client;
using Shared.Buffer;

namespace TransportAnalyzer.TestLogic
{
    class AckRawClientLogic : AckRawCommonLogic, IAckRawClientHandler
    {
        private volatile IAckRawServerEndpoint mEndpoint;

        private long mSendId = 0;
        private long mReceiveId = 0;

        private readonly int mUnconfirmedTicks;
        private readonly long mLastTickId;

        public override string ToString()
        {
            return string.Format("MessageId={0}", Interlocked.Read(ref mReceiveId));
        }

        public AckRawClientLogic(int uncofirmedTicks = 1, long lastTickId = -1)
        {
            mUnconfirmedTicks = uncofirmedTicks;
            mLastTickId = lastTickId;
        }

        public void WriteAckData(UnionDataList ackData)
        {
            ackData.PutFirst("stress");
        }

        public void OnConnected(IAckRawServerEndpoint endPoint, ByteArraySegment ackResponse)
        {
            if (!AckUtils.CheckPrefix(ackResponse, AckResponse).IsValid)
            {
                endPoint.Disconnect(new Transport.StopReasons.TextFail("stress", "Wrong ack response"));
                return;
            }

            mEndpoint = endPoint;
            var thread = new Thread(Work) { IsBackground = true };
            thread.Start();
        }

        public void OnDisconnected(StopReason reason)
        {
            mEndpoint = null;
        }

        public void OnStopped(StopReason reason)
        {
            Log.i("TEST STOPPED");
        }

        public void OnReceived(IMemoryBufferHolder receivedBuffer)
        {
            using (var bufferAccessor = receivedBuffer.ExposeAccessorOnce())
            {
                ByteArraySegment data;
                var id = Interlocked.Increment(ref mReceiveId);
                if (!bufferAccessor.Buffer.PopFirst().AsArray(out data) || !CheckBuffer(id, data) || id != mLastTickId)
                {
                    Log.e("Message check failed #" + id);
                    var endpoint = mEndpoint;
                    if (endpoint != null)
                    {
                        endpoint.Disconnect(new Transport.StopReasons.UserFail("Message check failed #" + id));
                    }
                }
            }
        }

        private void Work()
        {
            while (mEndpoint != null)
            {
                var endpoint = mEndpoint;

                if (mSendId - mReceiveId < mUnconfirmedTicks)
                {
                    var buffer = ConcurrentUsageMemoryBufferPool.Instance.AllocateAndPush(GenBuffer(Interlocked.Increment(ref mSendId)));
                    Log.i("SendToServer");
                    endpoint.Send(buffer);
                }
                Thread.Sleep(50);
            }
            Log.i("StopWork");
        }
    }
}
