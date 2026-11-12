using Pontifex.Factory.Registration;
using Pontifex.NetSockets;

namespace Pontifex.RR.Unreliable.NoAck.Udp
{
    /// <summary>
    /// Factory binding for the RR UDP unreliable NoAck transport.
    /// </summary>
    public static class RRUnreliableNoAckUdpBinding
    {
        public const string SchemeName = RRUdpInfo.TransportName + "_rr";
        public const string LegacyAlias = RRUdpInfo.TransportName;

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RRUnreliableNoAck,
                new TransportParameter[]
                {
                    NetBindingSupport.HostParameter(),
                    NetBindingSupport.PortParameter()
                },
                Create,
                new[] { LegacyAlias });
        }

        private static ITransport Create(ITransportBuildContext context)
        {
            NetBindingSupport.ResolveAddress(context, out var ipAddress, out var port);
            return context.Role == TransportRole.Server
                ? new RRUnreliableNoAckUdpServer(ipAddress, port, context.Logger, context.Memory)
                : new RRUnreliableNoAckUdpClient(ipAddress, port, context.Logger, context.Memory);
        }
    }
}
