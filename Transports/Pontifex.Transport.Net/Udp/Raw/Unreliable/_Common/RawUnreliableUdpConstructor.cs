using System;
using System.Collections.Generic;
using System.Net;
using Pontifex.Factory;
using Pontifex.NetSockets;

namespace Pontifex.Raw.Unreliable.Udp
{
    /// <summary>
    /// Base class for the RawUnreliable UDP transport constructors. Owns the
    /// host/port description parsing and URI parsing shared by the Ack and
    /// NoAck contract variants.
    /// </summary>
    public abstract class RawUnreliableUdpConstructor : ITransportConstructor
    {
        public abstract TransportType Type { get; }

        public abstract string Name { get; }

        protected abstract IRawUnreliableClient CreateClient(ITransportBuilder builder, IPAddress ipAddress, int port);

        protected abstract IRawUnreliableTransport CreateServer(ITransportBuilder builder, IPAddress ipAddress, int port);

        public ITransport ConstructServer(ITransportBuilder builder, IDescription description)
        {
            if (!TryParse(description, out var ip, out var port) || ip == null)
                throw new ArgumentException("Invalid UDP server description");

            return CreateServer(builder, ip, port);
        }

        public ITransport ConstructClient(ITransportBuilder builder, IDescription description)
        {
            if (!TryParse(description, out var ip, out var port) || ip == null)
                throw new ArgumentException("Invalid UDP client description");

            return CreateClient(builder, ip, port);
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
            yield return (Name, (uriBody, factory) =>
            {
                if (!UrlStringParser.TryParseAddress(uriBody, out var ip, out var port))
                    return null;

                var desc = new Description();
                desc.Add("host", new StringElement(ip.ToString()));
                desc.Add("port", new LongElement(port));
                desc.Add("type", new StringElement(Type.ToString()));
                return desc;
            });
        }
    }
}
