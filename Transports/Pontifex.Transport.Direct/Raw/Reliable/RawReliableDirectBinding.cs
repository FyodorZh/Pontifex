using System;
using Pontifex.Factory.Registration;

namespace Pontifex.Raw.Reliable.Direct
{
    /// <summary>
    /// Factory binding for the in-memory reliable Direct transport.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The canonical scheme is <c>direct</c>, and the transport handshake
    /// name is the same contract name.</description></item>
    /// </list>
    /// </remarks>
    public static class RawReliableDirectBinding
    {
        public const string SchemeName = RawReliableDirectInfo.TransportName;

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawReliable,
                new TransportParameter[]
                {
                    new StringParameter("id", required: true, positional: true),
                    new IntegerParameter(
                        "handshake_max_byte_size",
                        required: false,
                        positional: false,
                        new IntegerType(1, int.MaxValue)),
                    new IntegerParameter(
                        "message_max_size",
                        required: false,
                        positional: false,
                        new IntegerType(1, int.MaxValue)),
                    new IntegerParameter(
                        "buffer_capacity_bytes",
                        required: false,
                        positional: false,
                        new IntegerType(1, long.MaxValue)),
                    new IntegerParameter(
                        "handshake_timeout",
                        required: false,
                        positional: false,
                        new IntegerType(1, int.MaxValue))
                },
                Create);
        }

        private static ITransport Create(ITransportBuildContext context)
        {
            if (!context.TryGetString("id", out var id))
            {
                throw new InvalidOperationException(
                    "The resolver guarantees the required 'id' parameter.");
            }

            int handshakeMaxByteSize = RawReliableDirectInfo.DefaultHandshakeMaxByteSize;
            if (context.TryGetInt64("handshake_max_byte_size", out var handshakeMax))
            {
                handshakeMaxByteSize = checked((int)handshakeMax);
            }

            int messageMaxSize = DirectInfo.DefaultMessageMaxSize;
            if (context.TryGetInt64("message_max_size", out var configuredMessageMax))
            {
                messageMaxSize = checked((int)configuredMessageMax);
            }

            long bufferCapacityBytes = DirectInfo.DefaultBufferCapacity;
            if (context.TryGetInt64("buffer_capacity_bytes", out var configuredBufferCapacity))
            {
                bufferCapacityBytes = configuredBufferCapacity;
            }

            TimeSpan handshakeTimeout = TimeSpan.FromSeconds(10);
            if (context.TryGetInt64("handshake_timeout", out var handshakeTimeoutSeconds))
            {
                handshakeTimeout = TimeSpan.FromSeconds(handshakeTimeoutSeconds);
            }

            if (handshakeMaxByteSize > messageMaxSize)
            {
                throw new ArgumentException(
                    "handshake_max_byte_size must not exceed message_max_size.", nameof(context));
            }

            return context.Role == TransportRole.Server
                ? new RawReliableDirectServerTransport(
                    id,
                    context.Logger,
                    context.Memory,
                    messageMaxSize: messageMaxSize,
                    bufferCapacityBytes: bufferCapacityBytes,
                    handshakeMaxByteSize: handshakeMaxByteSize,
                    handshakeTimeout: handshakeTimeout)
                : new RawReliableDirectClientTransport(
                    id,
                    context.Logger,
                    context.Memory,
                    messageMaxSize: messageMaxSize,
                    bufferCapacityBytes: bufferCapacityBytes,
                    handshakeMaxByteSize: handshakeMaxByteSize,
                    handshakeTimeout: handshakeTimeout);
        }
    }
}
