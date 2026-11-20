using System;
using Pontifex.Factory.Registration;
using Pontifex.NetSockets;

namespace Pontifex.Raw.Reliable.Tcp
{
    /// <summary>
    /// Factory binding for the TCP reliable transport.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The canonical scheme is <c>tcp-noack</c>. The acknowledgement
    /// <c>tcp</c> scheme is never registered as an alias for this transport, and the reserved
    /// <c>direct-noack</c> scheme is not registered here.</description></item>
    /// </list>
    /// </remarks>
    public static class RawReliableTcpBinding
    {
        public const string SchemeName = RawReliableTcpInfo.TransportName;

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawReliable,
                new TransportParameter[]
                {
                    NetBindingSupport.HostParameter(),
                    NetBindingSupport.PortParameter(),
                    new IntegerParameter(
                        "disconnection_timeout",
                        required: false,
                        positional: false,
                        new IntegerType(0, long.MaxValue)),
                    new IntegerParameter(
                        "handshake_max_byte_size",
                        required: false,
                        positional: false,
                        new IntegerType(1, long.MaxValue)),
                    new IntegerParameter(
                        "handshake_timeout",
                        required: false,
                        positional: false,
                        new IntegerType(1, long.MaxValue)),
                    new IntegerParameter(
                        "reject_linger",
                        required: false,
                        positional: false,
                        new IntegerType(0, long.MaxValue))
                },
                Create);
        }

        private static ITransport Create(ITransportBuildContext context)
        {
            NetBindingSupport.ResolveAddress(context, out var ipAddress, out var port);

            var disconnectTimeout = RawReliableTcpInfo.DefaultDisconnectTimeout;
            if (context.TryGetInt64("disconnection_timeout", out var timeoutSeconds))
            {
                disconnectTimeout = TimeSpan.FromSeconds(timeoutSeconds);
            }

            int handshakeMaxByteSize = RawReliableHandshake.DefaultMaxByteSize;
            if (context.TryGetInt64("handshake_max_byte_size", out var handshakeMax))
            {
                handshakeMaxByteSize = checked((int)handshakeMax);
            }

            var handshakeTimeout = TimeSpan.FromSeconds(10);
            if (context.TryGetInt64("handshake_timeout", out var handshakeTimeoutSeconds))
            {
                handshakeTimeout = TimeSpan.FromSeconds(handshakeTimeoutSeconds);
            }

            var rejectLinger = TimeSpan.FromMilliseconds(2000);
            if (context.TryGetInt64("reject_linger", out var rejectLingerMs))
            {
                rejectLinger = TimeSpan.FromMilliseconds(rejectLingerMs);
            }

            return context.Role == TransportRole.Server
                ? new RawReliableTcpServerTransport(
                    ipAddress,
                    port,
                    RawReliableTcpInfo.ServerConnectionsLimit,
                    disconnectTimeout,
                    null,
                    context.Logger,
                    context.Memory,
                    handshakeName: RawReliableTcpInfo.HandshakeName,
                    handshakeMaxByteSize: handshakeMaxByteSize,
                    handshakeTimeout: handshakeTimeout,
                    rejectLinger: rejectLinger)
                : new RawReliableTcpClientTransport(
                    ipAddress,
                    port,
                    disconnectTimeout,
                    null,
                    context.Logger,
                    context.Memory,
                    handshakeName: RawReliableTcpInfo.HandshakeName,
                    handshakeMaxByteSize: handshakeMaxByteSize,
                    handshakeTimeout: handshakeTimeout);
        }
    }
}
