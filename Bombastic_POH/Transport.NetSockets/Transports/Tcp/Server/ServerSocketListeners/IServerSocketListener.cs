using System;
using System.Net.Sockets;

namespace Transport.Transports.Tcp
{
    interface IServerSocketListener
    {
        event Action<Socket> Connected;
        event Action Stopped;
        event Action<Exception> Failed;
        bool Start();
        void Stop();
    }
}
