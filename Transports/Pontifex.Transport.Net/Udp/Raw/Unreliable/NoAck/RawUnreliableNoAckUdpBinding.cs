using Pontifex.Factory.Registration;
using Pontifex.NetSockets;

namespace Pontifex.Raw.Unreliable.NoAck.Udp
{
    /// <summary>
    /// Factory binding for the UDP unreliable NoAck transport.
    /// </summary>
    public static class RawUnreliableNoAckUdpBinding
    {
        public const string SchemeName = RawUnreliableNoAckUdpInfo.TransportName;

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawUnreliableNoAck,
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
                ? new RawUnreliableNoAckUdpServer(ipAddress, port, context.Logger, context.Memory)
                : new RawUnreliableNoAckUdpClient(ipAddress, port, context.Logger, context.Memory);
        }
    }
}
