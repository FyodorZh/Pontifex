using Pontifex.Factory.Registration;
using Pontifex.NetSockets;

namespace Pontifex.Raw.Unreliable.Ack.Udp
{
    /// <summary>
    /// Factory binding for the UDP unreliable Ack transport.
    /// </summary>
    public static class RawUnreliableAckUdpBinding
    {
        public const string SchemeName = RawUnreliableAckUdpInfo.TransportName;

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawUnreliableAck,
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
                ? new RawUnreliableAckUdpServer(ipAddress, port, context.Logger, context.Memory)
                : new RawUnreliableAckUdpClient(ipAddress, port, context.Logger, context.Memory);
        }
    }
}
