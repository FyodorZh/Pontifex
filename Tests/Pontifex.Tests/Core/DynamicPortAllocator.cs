using System.Net;
using System.Net.Sockets;

namespace Pontifex.Tests;

public static class DynamicPortAllocator
{
    public static int GetRandomPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            return port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
