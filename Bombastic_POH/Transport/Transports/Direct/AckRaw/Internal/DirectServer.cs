using System;
using Shared;
using Transport.Abstractions;
using Transport.Endpoints;

namespace Transport.Transports.Direct
{
    internal class DirectServer
    {
        private readonly IEndPoint mServerEp;
        private readonly Func<ByteArraySegment, IServerDirectCtl> mOnConnecting;

        private readonly IConcurrentMap<IEndPoint, DirectTransport> mConnectedClients = new TrivialConcurrentDictionary<IEndPoint, DirectTransport>();

        public DirectServer(IEndPoint serverEp, Func<ByteArraySegment, IServerDirectCtl> onConnecting)
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
                IServerDirectCtl serverCtl = mOnConnecting(clientCtl.GetAckData());
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