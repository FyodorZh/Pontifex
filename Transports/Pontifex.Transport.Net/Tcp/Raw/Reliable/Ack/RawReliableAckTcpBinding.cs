using System;
using Pontifex.Factory.Registration;
using Pontifex.NetSockets;

namespace Pontifex.Raw.Reliable.Ack.Tcp
{
    /// <summary>
    /// Factory binding for the TCP reliable Ack transport.
    /// </summary>
    public static class RawReliableAckTcpBinding
    {
        public const string SchemeName = TcpInfo.TransportName;

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawReliableAck,
                new TransportParameter[]
                {
                    NetBindingSupport.HostParameter(),
                    NetBindingSupport.PortParameter(),
                    new IntegerParameter(
                        "disconnection_timeout",
                        required: false,
                        positional: false,
                        new IntegerType(0, long.MaxValue))
                },
                Create);
        }

        private static ITransport Create(ITransportBuildContext context)
        {
            NetBindingSupport.ResolveAddress(context, out var ipAddress, out var port);

            var disconnectTimeout = TcpInfo.DefaultDisconnectTimeout;
            if (context.TryGetInt64("disconnection_timeout", out var timeoutSeconds))
            {
                disconnectTimeout = TimeSpan.FromSeconds(timeoutSeconds);
            }

            return context.Role == TransportRole.Server
                ? new RawReliableAckTcpServerTransport(
                    ipAddress,
                    port,
                    TcpInfo.ServerConnectionsLimit,
                    disconnectTimeout,
                    null,
                    context.Logger,
                    context.Memory)
                : new RawReliableAckTcpClientTransport(
                    ipAddress,
                    port,
                    disconnectTimeout,
                    null,
                    context.Logger,
                    context.Memory);
        }
    }
}
