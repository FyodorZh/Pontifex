using System;
using System.Threading;
using NewProtocol;
using NewProtocol.Server;
using Shared;
using Transport;
using Transport.Abstractions.Servers;

namespace TransportAnalyzer.TestLogic
{
    class AckRawTestFactory : AckRawServerProtocolFactory<AckRawProtocol>
    {
        private readonly Action<AckRawTestServer> mOnConnected;
        private readonly Action<AckRawTestServer> mOnDisconnected;

        public AckRawTestFactory(IAckRawServer transport, Action<AckRawTestServer> onConnected, Action<AckRawTestServer> onDisconnected)
            : base(new ModelsHashDB(), transport)
        {
            mOnConnected = onConnected;
            mOnDisconnected = onDisconnected;
        }

        protected override AckRawServerProtocol ConstructSSP(UnionDataList ackData, ILogger logger)
        {
            if (ackData.Check("AckRawTestProtocol") && ackData.Elements.Count == 0)
            {
                return new AckRawTestServer(mOnConnected, mOnDisconnected);
            }
            logger.e("Invalid ack data");
            return null;
        }
    }

    class AckRawTestServer : AckRawServerProtocol
    {
        private readonly Action<AckRawTestServer> mOnConnected;
        private readonly Action<AckRawTestServer> mOnDisconnected;

        private AckRawProtocol mProtocol;

        private long mReceiveId;

        public AckRawTestServer(Action<AckRawTestServer> onConnected, Action<AckRawTestServer> onDisconnected)
            : base(UtcNowDateTimeProvider.Instance)
        {
            mOnConnected = onConnected;
            mOnDisconnected = onDisconnected;
        }

        public void StopTransport()
        {
            var endpoint = EndPoint;
            if (endpoint != null)
            {
                endpoint.Disconnect(StopReason.UserIntention);
            }
        }

        protected override Protocol ConstructProtocol()
        {
            mProtocol =  new AckRawProtocol();
            mProtocol.Request.SetProcessor(Processor);
            return mProtocol;
        }

        protected override void OnDisconnected(StopReason reason)
        {
            Log.i("Disconnected. Reason: {@reason}", reason.Print());
            base.OnDisconnected(reason);
            mOnDisconnected(this);
        }

        protected override byte[] GetAckResponse()
        {
            return AckUtils.AckString("OK");
        }

        protected override void OnConnected()
        {
            var bytes = new BytesBuffer();
            bytes.Data = new byte[] {77};
            mProtocol.OnAck.Send(bytes);

            mOnConnected(this);
        }

        private void Processor(IRequest<BytesBuffer, BytesBuffer> request)
        {
            byte[] data = request.Data.Data;

            int len = data.Length;
            for (int i = 0; i < len / 2; ++i)
            {
                byte tmp = data[i];
                data[i] = data[len - 1 - i];
                data[len - 1 - i] = tmp;
            }

            long id = Interlocked.Increment(ref mReceiveId);
            if (!AckRawCommonLogic.CheckBuffer(id, new ByteArraySegment(data)))
            {
                Log.e("Message check failed #" + id);
                Stop();
                request.Fail("Wrong message");
                return;
            }

            request.Response(new BytesBuffer{ Data = data });
        }

        public override string ToString()
        {
            return RemoteEndPoint.ToString();
        }
    }
}
