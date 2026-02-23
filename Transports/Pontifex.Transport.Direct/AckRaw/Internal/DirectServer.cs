using System;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Abstractions;
using Pontifex.Utils;

namespace Pontifex.Transports.Direct
{
    internal class DirectServer
    {
        private readonly IEndPoint _serverEp;
        private readonly Func<UnionDataList, IServerDirectCtl?> _onConnecting;

        private readonly IConcurrentMap<IEndPoint, DirectTransport> _connectedClients = new SynchronizedConcurrentDictionary<IEndPoint, DirectTransport>();
        
        private readonly IMemoryRental _memoryRental;

        public DirectServer(IEndPoint serverEp, Func<UnionDataList, IServerDirectCtl?> onConnecting, IMemoryRental memoryRental)
        {
            _serverEp = serverEp;
            _onConnecting = onConnecting;
            _memoryRental = memoryRental;
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

            UnionDataList ackData = _memoryRental.CollectablePool.Acquire<UnionDataList>();
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