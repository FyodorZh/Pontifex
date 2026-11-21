using Pontifex.Factory.Registration;
using Pontifex.NetSockets;

namespace Pontifex.Raw.Unreliable.Udp
{
    /// <summary>
    /// Factory binding for the UDP unreliable transport.
    /// </summary>
    public static class RawUnreliableUdpBinding
    {
        public const string SchemeName = RawUnreliableUdpInfo.TransportName;

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawUnreliable,
                new TransportParameter[]
                {
                    NetBindingSupport.HostParameter(),
                    NetBindingSupport.PortParameter()
                },
                Create);
        }

        private static ITransport Create(ITransportBuildContext context)
        {
            NetBindingSupport.ResolveAddress(context, out var ipAddress, out var port);
            return context.Role == TransportRole.Server
                ? new RawUnreliableUdpServer(ipAddress, port, context.Logger, context.Memory)
                : new RawUnreliableUdpClient(ipAddress, port, context.Logger, context.Memory);
        }
    }
}
