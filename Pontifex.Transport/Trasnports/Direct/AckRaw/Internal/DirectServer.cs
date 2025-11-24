using System;
using Actuarius.Collections;
using Pontifex.Utils;
using Transport.Abstractions;

namespace Transport.Transports.Direct
{
    internal class DirectServer
    {
        private readonly IEndPoint _serverEp;
        private readonly Func<UnionDataList, IServerDirectCtl?> _onConnecting;

        private readonly IConcurrentMap<IEndPoint, DirectTransport> _connectedClients = new SynchronizedConcurrentDictionary<IEndPoint, DirectTransport>();

        public DirectServer(IEndPoint serverEp, Func<UnionDataList, IServerDirectCtl?> onConnecting)
        {
            _serverEp = serverEp;
            _onConnecting = onConnecting;
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public DirectTransport? NewTransport(IEndPoint clientAddress, IClientDirectCtl clientCtl)
        {
            if (_connectedClients.TryGetValue(clientAddress, out var transport))
            {
                return null;
            }
            
            
            UnionDataList ackData = new();
            clientCtl.GetAckData(ackData);
            IServerDirectCtl? serverCtl = _onConnecting(ackData);
            if (serverCtl != null)
            {
                transport = new DirectTransport(_serverEp, clientAddress, serverCtl, clientCtl, (clientEp) =>
                {
                    _connectedClients.Remove(clientEp.ClientEp);
                });
                serverCtl.Init(transport);

                _connectedClients.Add(clientAddress, transport);

                return transport;
            }

            return null;
        }
    }
}