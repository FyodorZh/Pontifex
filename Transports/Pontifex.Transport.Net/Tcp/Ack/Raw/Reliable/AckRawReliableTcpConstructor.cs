using System;
using System.Collections.Generic;
using System.Net;
using Pontifex.Factory;
using Pontifex.NetSockets;

namespace Pontifex.Ack.Raw.Reliable.Tcp
{
    public class AckRawReliableTcpConstructor : ITransportConstructor
    {
        public TransportType Type => TransportType.AckRawReliable;
        public string Name => TcpInfo.TransportName;

        public ITransport ConstructServer(ITransportBuilder builder, IDescription description)
        {
            if (!TryParse(description, out var disconnectionTimeout, out var ip, out var port) || ip == null)
                throw new ArgumentException("Invalid TCP server description");

            return new AckRawReliableTcpServer(ip, port, TcpInfo.ServerConnectionsLimit, disconnectionTimeout, null, builder.Logger, builder.MemoryRental);
        }

        public ITransport ConstructClient(ITransportBuilder builder, IDescription description)
        {
            if (!TryParse(description, out var disconnectionTimeout, out var ip, out var port) || ip == null)
                throw new ArgumentException("Invalid TCP client description");

            return new AckRawReliableTcpClient(ip, port, disconnectionTimeout, null, builder.Logger, builder.MemoryRental);
        }

        private static bool TryParse(IDescription description, out TimeSpan disconnectionTimeout, out IPAddress? ip, out int port)
        {
            try
            {
                if (!description.Get("host").EvaluateAsString(out var host))
                {
                    disconnectionTimeout = TimeSpan.Zero;
                    ip = null;
                    port = -1;
                    return false;
                }

                if (!description.Get("port").EvaluateAsLong(out var portLong))
                {
                    disconnectionTimeout = TimeSpan.Zero;
                    ip = null;
                    port = -1;
                    return false;
                }

                disconnectionTimeout = TcpInfo.DefaultDisconnectTimeout;
                if (description.Get("disconnection_timeout").EvaluateAsLong(out var timeoutLong))
                {
                    disconnectionTimeout = TimeSpan.FromSeconds(timeoutLong);
                }

                ip = IPAddress.Parse(host);
                port = (int)portLong;
                return true;
            }
            catch
            {
                disconnectionTimeout = TimeSpan.Zero;
                ip = null;
                port = -1;
                return false;
            }
        }

        public IEnumerable<(string name, Func<string, IDescriptionUriFactory, Description?> uriParser)> GetUriParsers()
        {
            yield return (TcpInfo.TransportName, (uriBody, factory) =>
            {
                var parts = uriBody.Split('/');
                string address = parts[0];

                if (!UrlStringParser.TryParseAddress(address, out var ip, out var port))
                    return null;

                var desc = new Description();
                desc.Add("host", new StringElement(ip.ToString()));
                desc.Add("port", new LongElement(port));
                desc.Add("type", new StringElement("AckRawReliable"));

                if (parts.Length > 1 && int.TryParse(parts[1], out var timeout))
                {
                    desc.Add("disconnection_timeout", new LongElement(timeout));
                }

                return desc;
            });
        }
    }
}
