using Actuarius.Memory;
using Pontifex.Abstractions.Servers;
using Pontifex.Endpoints;
using Pontifex.Transports.Core;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Transports.Direct
{
    public class AckRawDirectServer : AckRawServer, IAckReliableRawServer
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
                server.Stop();
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