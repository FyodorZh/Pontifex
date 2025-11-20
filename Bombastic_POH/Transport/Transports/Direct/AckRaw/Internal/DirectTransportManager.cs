using System;
using Actuarius.Collections;
using Actuarius.Memory;
using Shared;
using Transport.Abstractions;
using Transport.Endpoints;

namespace Transport.Transports.Direct
{
    internal class DirectTransportManager
    {
        public static readonly DirectTransportManager Instance = new DirectTransportManager();

        private readonly IConcurrentMap<IEndPoint, DirectServer> mServers = new TrivialConcurrentDictionary<IEndPoint, DirectServer>();

        public DirectServer StartServer(IEndPoint serverEp, Func<UnionDataList, IServerDirectCtl> onConnecting)
        {
            DirectServer server = new DirectServer(serverEp, onConnecting);

            if (mServers.Add(serverEp, server))
            {
                return server;
            }

            return null;
        }

        public DirectTransport NewTransport(
            IEndPoint serverEp,
            IEndPoint clientEp,
            IClientDirectCtl clientCtl)
        {
            DirectServer server;
            if (mServers.TryGetValue(serverEp, out server))
            {
                return server.NewTransport(clientEp, clientCtl);
            }
            return null;
        }
    }
}