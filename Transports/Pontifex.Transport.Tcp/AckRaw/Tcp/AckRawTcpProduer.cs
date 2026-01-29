using System;
using System.Diagnostics.CodeAnalysis;
using Actuarius.Memory;
using Pontifex.Abstractions;
using Scriba;

namespace Pontifex.Transports.Tcp
{
    public class BaseAckRawTcpProducer
    {
        public string Name => TcpInfo.TransportName;

        // host:ip/timeout
        protected static bool Parse(string @params, out TimeSpan disconnectionTimeout, [MaybeNullWhen(false)] out System.Net.IPAddress ip, out int port)
        {
            try
            {
                string[] list = @params.Split('/');
                disconnectionTimeout = list.Length == 1 ? TcpInfo.DefaultDisconnectTimeout : TimeSpan.FromSeconds(int.Parse(list[1]));

                return Utils.UrlStringParser.TryParseAddress(list[0], out ip, out port);
            }
            catch
            {
                disconnectionTimeout = TimeSpan.Zero;
                ip = null;
                port = -1;
                return false;
            }
        }
    }

    public class AckRawTcpClientProducer : BaseAckRawTcpProducer, ITransportProducer
    {
        public ITransport? Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental)
        {
            if (Parse(@params, out var disconnectionTimeout, out var ip, out var port))
            {
                return new AckRawTcpClient(ip, port, disconnectionTimeout, logger, memoryRental);
            }
            return null;
        }
    }

    public class AckRawTcpServerProducer : BaseAckRawTcpProducer, ITransportProducer
    {
        public ITransport? Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental)
        {
            if (Parse(@params, out var disconnectionTimeout, out var ip, out var port))
            {
                return new AckRawTcpServer(ip, port, TcpInfo.ServerConnectionsLimit, disconnectionTimeout, logger, memoryRental);
            }
            return null;
        }
    }
}
