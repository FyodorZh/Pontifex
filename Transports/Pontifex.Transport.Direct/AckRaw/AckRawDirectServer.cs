using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Ack.Raw.Reliable.Direct
{
    public class AckRawDirectServer : AckRawReliableServer, IAckRawReliableServer
    {
        private readonly StringEndPoint _localEp;
        private DirectServer? _server;

        public override int MessageMaxByteSize => DirectInfo.MessageMaxByteSize;

        public AckRawDirectServer(string serverName, ILogger logger, IMemoryRental memory)
            : base(DirectInfo.TransportName, logger, memory)
        {
            _localEp = new StringEndPoint(serverName);
        }

        protected override bool TryStart()
        {
            _server = DirectTransportManager.Instance.StartServer(_localEp, OnConnecting, Memory);
            return _server != null;
        }

        protected override void OnStopped(StopReason reason)
        {
            var server = _server;
            if (server != null)
            {
                _server = null;
                DirectTransportManager.Instance.StopServer(_localEp);
            }
        }

        private IServerDirectCtl? OnConnecting(UnionDataList ackData)
        {
            var handler = TryConnectNewClient(ackData);
            if (handler != null)
            {
                return new Session(handler, Memory);
            }

            return null;
        }
    }
}