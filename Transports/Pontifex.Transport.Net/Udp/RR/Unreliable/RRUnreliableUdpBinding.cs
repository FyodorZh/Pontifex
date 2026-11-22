using System;
using System.Net;
using Pontifex.Factory.Registration;
using Pontifex.NetSockets;

namespace Pontifex.RR.Unreliable.Udp
{
    /// <summary>
    /// Factory binding for the RR UDP unreliable transport.
    /// </summary>
    public static class RRUnreliableUdpBinding
    {
        public const string SchemeName = RRUdpInfo.TransportName + "_rr";

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RRUnreliable,
                new TransportParameter[]
                {
                    NetBindingSupport.HostParameter(),
                    NetBindingSupport.PortParameter(),
                    new IntegerParameter(
                        "request_ttl",
                        required: false,
                        positional: false,
                        new IntegerType(1, int.MaxValue)),
                    new IntegerParameter(
                        "response_ttl",
                        required: false,
                        positional: false,
                        new IntegerType(1, int.MaxValue))
                },
                Create);
        }

        private static ITransport Create(ITransportBuildContext context)
        {
            NetBindingSupport.ResolveAddress(context, out var ipAddress, out var port);

            if (port == 0)
            {
                throw new InvalidOperationException(
                    "An RR unreliable UDP transport requires a non-zero port.");
            }

            if (context.Role == TransportRole.Server)
            {
                TimeSpan responseTtl = ResolveServerResponseTtl(context);
                return new RRUnreliableUdpServer(ipAddress, port, responseTtl, context.Logger, context.Memory);
            }

            TimeSpan requestTtl = ResolveClientRequestTtl(context);
            IEndPoint serverEndPoint = new IpEndPoint(new IPEndPoint(ipAddress, port));
            return new RRUnreliableUdpClient(serverEndPoint, requestTtl, context.Logger, context.Memory);
        }

        private static TimeSpan ResolveClientRequestTtl(ITransportBuildContext context)
        {
            if (context.TryGetInt64("response_ttl", out _))
            {
                throw new InvalidOperationException(
                    "The 'response_ttl' parameter is valid only for a server transport.");
            }

            return context.TryGetInt64("request_ttl", out long seconds)
                ? TimeSpan.FromSeconds(seconds)
                : RRUdpInfo.DefaultClientRequestTtl;
        }

        private static TimeSpan ResolveServerResponseTtl(ITransportBuildContext context)
        {
            if (context.TryGetInt64("request_ttl", out _))
            {
                throw new InvalidOperationException(
                    "The 'request_ttl' parameter is valid only for a client transport.");
            }

            return context.TryGetInt64("response_ttl", out long seconds)
                ? TimeSpan.FromSeconds(seconds)
                : RRUdpInfo.DefaultServerResponseTtl;
        }
    }
}
