using System;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Utils;
using Shared;
using Transport.Abstractions;
using Transport.Endpoints;

namespace Transport.Transports.Direct
{
    internal class DirectServer
    {
        private readonly IEndPoint mServerEp;
        private readonly Func<UnionDataList, IServerDirectCtl> mOnConnecting;

        private readonly IConcurrentMap<IEndPoint, DirectTransport> mConnectedClients = new SynchronizedConcurrentDictionary<IEndPoint, DirectTransport>();

        public DirectServer(IEndPoint serverEp, Func<UnionDataList, IServerDirectCtl> onConnecting)
        {
            mServerEp = serverEp;
            mOnConnecting = onConnecting;
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public DirectTransport NewTransport(
            IEndPoint clientAddress,
            IClientDirectCtl clientCtl)
        {
            DirectTransport transport;
            if (!mConnectedClients.TryGetValue(clientAddress, out transport))
            {
                UnionDataList ackData = new();
                clientCtl.GetAckData(ackData);
                IServerDirectCtl serverCtl = mOnConnecting(ackData);
                if (serverCtl != null)
                {
                    transport = new DirectTransport(mServerEp, clientAddress, serverCtl, clientCtl, (clientEp) =>
                    {
                        mConnectedClients.Remove(clientEp.ClientEp);
                    });
                    serverCtl.Init(transport);

                    mConnectedClients.Add(clientAddress, transport);

                    return transport;
                }
            }

            return null;
        }
    }
}