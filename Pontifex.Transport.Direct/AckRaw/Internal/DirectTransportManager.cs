using System;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Utils;
using Transport.Abstractions;

namespace Transport.Transports.Direct
{
    internal class DirectTransportManager
    {
        public static readonly DirectTransportManager Instance = new DirectTransportManager();

        private readonly IConcurrentMap<IEndPoint, DirectServer> _servers = new SynchronizedConcurrentDictionary<IEndPoint, DirectServer>();

        public DirectServer? StartServer(IEndPoint serverEp, Func<UnionDataList, IServerDirectCtl?> onConnecting, IMemoryRental memoryRental)
        {
            DirectServer server = new DirectServer(serverEp, onConnecting, memoryRental);

            if (_servers.Add(serverEp, server))
            {
                return server;
            }

            return null;
        }

        public DirectTransport? NewTransport(IEndPoint serverEp, IEndPoint clientEp, IClientDirectCtl clientCtl)
        {
            if (_servers.TryGetValue(serverEp, out var server))
            {
                return server.NewTransport(clientEp, clientCtl);
            }
            return null;
        }
    }
}