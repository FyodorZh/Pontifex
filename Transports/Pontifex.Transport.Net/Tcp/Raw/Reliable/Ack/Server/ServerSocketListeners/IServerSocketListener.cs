using System;
using System.Net.Sockets;

namespace Pontifex.Raw.Reliable.Ack.Tcp
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
