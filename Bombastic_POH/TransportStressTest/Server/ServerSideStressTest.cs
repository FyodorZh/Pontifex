using NewProtocol;
using NewProtocol.Server;
using Shared;
using Shared.Buffer;

namespace TransportStressTest
{
    public class ServerSideStressTest : AckRawServerProtocol
    {
        private StressTestProtocol mProtocol;

        public ServerSideStressTest()
            : base(NowDateTimeProvider.Instance)
        {
        }

        protected override Protocol ConstructProtocol()
        {
            mProtocol = new StressTestProtocol();
            mProtocol.Request.Register(OnRequest);
            return mProtocol;
        }

        protected override byte[] GetAckResponse()
        {
            return new byte[] {3, 2, 1};
        }

        protected override void OnConnected()
        {
        }

        private void OnRequest(IMemoryBufferHolder buffer)
        {
            mProtocol.Response.Send(buffer);
        }
    }
}