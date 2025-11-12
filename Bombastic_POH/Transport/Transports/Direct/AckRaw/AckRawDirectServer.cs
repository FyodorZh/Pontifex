using Shared;
using Transport.Endpoints;
using Transport.Transports.Core;

namespace Transport.Transports.Direct
{
    public class AckRawDirectServer : AckRawServer
    {
        private readonly StringEndPoint mLocalEp;
        private DirectServer mServer;

        public override int MessageMaxByteSize
        {
            get { return DirectInfo.MessageMaxByteSize; }
        }

        public AckRawDirectServer(string serverName)
            : base(DirectInfo.TransportName)
        {
            mLocalEp = new StringEndPoint(serverName);
        }

        protected override bool TryStart()
        {
            mServer = DirectTransportManager.Instance.StartServer(mLocalEp, OnConnecting);
            return mServer != null;
        }

        protected override void OnStopped(StopReason reason)
        {
            var server = mServer;
            if (server != null)
            {
                mServer = null;
                server.Stop();
            }
        }

        private IServerDirectCtl OnConnecting(ByteArraySegment ackData)
        {
            var handler = TryConnectNewClient(ackData);
            if (handler != null)
            {
                return new Session(handler);
            }

            return null;
        }
    }
}