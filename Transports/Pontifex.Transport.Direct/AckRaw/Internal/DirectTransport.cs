using System;
using Pontifex.Abstractions;

namespace Pontifex.Transports.Direct
{
    internal class DirectTransport
    {
        private readonly IServerDirectCtl _serverCtl;
        private readonly IClientDirectCtl _clientCtl;

        private Action<DirectTransport>? _onDisconnected;
        public TransportEndPoint ClientSide { get; private set; }

        public TransportEndPoint ServerSide { get; private set; }

        public IEndPoint ClientEp => ClientSide.LocalEndPoint;

        public DirectTransport(IEndPoint serverEp, IEndPoint clientEp,
            IServerDirectCtl serverCtl,
            IClientDirectCtl clientCtl,
            Action<DirectTransport> onDisconnected)
        {
            _onDisconnected = onDisconnected;

            _serverCtl = serverCtl;
            _clientCtl = clientCtl;

            ServerSide = new TransportEndPoint(this, serverEp, _serverCtl);
            ClientSide = new TransportEndPoint(this, clientEp, _clientCtl);

            ServerSide.SetOther(ClientSide);
            ClientSide.SetOther(ServerSide);
        }

        public void FinishConnection()
        {
            _serverCtl.OnClientPrepared();
        }

        public bool Disconnect(StopReason reason)
        {
            var onDisconnected = System.Threading.Interlocked.Exchange(ref _onDisconnected, null);
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