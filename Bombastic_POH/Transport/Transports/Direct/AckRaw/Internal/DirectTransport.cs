using System;
using Transport.Abstractions;

namespace Transport.Transports.Direct
{
    internal class DirectTransport
    {
        private readonly IServerDirectCtl mServerCtl;
        private readonly IClientDirectCtl mClientCtl;

        private Action<DirectTransport> mOnDisconnected;
        public TransportEndPoint ClientSide { get; private set; }

        public TransportEndPoint ServerSide { get; private set; }

        public IEndPoint ClientEp
        {
            get { return ClientSide.LocalEndPoint; }
        }

        public DirectTransport(IEndPoint serverEp, IEndPoint clientEp,
            IServerDirectCtl serverCtl,
            IClientDirectCtl clientCtl,
            Action<DirectTransport> onDisconnected)
        {
            mOnDisconnected = onDisconnected;

            mServerCtl = serverCtl;
            mClientCtl = clientCtl;

            ServerSide = new TransportEndPoint(this, serverEp, mServerCtl);
            ClientSide = new TransportEndPoint(this, clientEp, mClientCtl);

            ServerSide.SetOther(ClientSide);
            ClientSide.SetOther(ServerSide);
        }

        public void FinishConnection()
        {
            mServerCtl.OnClientPrepared();
        }

        public bool Disconnect(StopReason reason)
        {
            var onDisconnected = System.Threading.Interlocked.Exchange(ref mOnDisconnected, null);
            if (onDisconnected != null)
            {
                ServerSide.Disconnect(reason);
                ClientSide.Disconnect(reason);

                onDisconnected(this);
                return true;
            }

            return false;
        }
    }
}