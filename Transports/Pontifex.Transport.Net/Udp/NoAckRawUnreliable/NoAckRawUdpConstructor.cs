using System;
using System.Collections.Generic;
using System.Net;
using Pontifex.Factory;
using Pontifex.NetSockets;

namespace Pontifex.NoAck.Raw.Unreliable.Udp
{
    public class NoAckRawUdpConstructor : ITransportConstructor
    {
        public TransportType Type => TransportType.NoAckRawUnreliable;
        public string Name => RawUdpInfo.TransportName;

        public ITransport ConstructServer(ITransportBuilder builder, IDescription description)
        {
            if (!TryParse(description, out var ip, out var port) || ip == null)
                throw new ArgumentException("Invalid UDP server description");

            return new NoAckRawUdpServer(ip, port, builder.Logger, builder.MemoryRental);
        }

        public ITransport ConstructClient(ITransportBuilder builder, IDescription description)
        {
            if (!TryParse(description, out var ip, out var port) || ip == null)
                throw new ArgumentException("Invalid UDP client description");

            return new NoAckRawUdpClient(ip, port, builder.Logger, builder.MemoryRental);
        }

        private static bool TryParse(IDescription description, out IPAddress? ip, out int port)
        {
            try
            {
                if (!description.Get("host").EvaluateAsString(out var host))
                {
                    ip = null;
                    port = -1;
                    return false;
                }

                if (!description.Get("port").EvaluateAsLong(out var portLong))
                {
                    ip = null;
                    port = -1;
                    return false;
                }

                ip = IPAddress.Parse(host);
                port = (int)portLong;
                return true;
            }
            catch
            {
                ip = null;
                port = -1;
                return false;
            }
        }

        public IEnumerable<(string name, Func<string, IDescriptionUriFactory, Description?> uriParser)> GetUriParsers()
        {
            yield return (RawUdpInfo.TransportName, (uriBody, factory) =>
            {
                if (!UrlStringParser.TryParseAddress(uriBody, out var ip, out var port))
                    return null;

                var desc = new Description();
                desc.Add("host", new StringElement(ip.ToString()));
                desc.Add("port", new LongElement(port));
                desc.Add("type", new StringElement("NoAckRawUnreliable"));
                return desc;
            });
        }
    }
}
