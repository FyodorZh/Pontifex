using System;
using Pontifex.Factory.Registration;
using Pontifex.NetSockets;

namespace Pontifex.Raw.Reliable.NoAck.Tcp
{
    /// <summary>
    /// Factory binding for the TCP reliable no-acknowledgement transport.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The canonical scheme is <c>tcp-noack</c>. The acknowledgement
    /// <c>tcp</c> scheme is never registered as an alias for this transport, and the reserved
    /// <c>direct-noack</c> scheme is not registered here.</description></item>
    /// </list>
    /// </remarks>
    public static class RawReliableNoAckTcpBinding
    {
        public const string SchemeName = TcpNoAckInfo.TransportName;

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawReliableNoAck,
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

            var disconnectTimeout = TcpNoAckInfo.DefaultDisconnectTimeout;
            if (context.TryGetInt64("disconnection_timeout", out var timeoutSeconds))
            {
                disconnectTimeout = TimeSpan.FromSeconds(timeoutSeconds);
            }

            return context.Role == TransportRole.Server
                ? new RawReliableNoAckTcpServerTransport(
                    ipAddress,
                    port,
                    TcpNoAckInfo.ServerConnectionsLimit,
                    disconnectTimeout,
                    null,
                    context.Logger,
                    context.Memory)
                : new RawReliableNoAckTcpClientTransport(
                    ipAddress,
                    port,
                    disconnectTimeout,
                    null,
                    context.Logger,
                    context.Memory);
        }
    }
}
