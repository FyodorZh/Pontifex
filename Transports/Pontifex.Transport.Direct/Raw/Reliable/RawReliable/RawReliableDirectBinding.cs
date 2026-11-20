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
                        new IntegerType(1, long.MaxValue))
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

            return context.Role == TransportRole.Server
                ? new RawReliableDirectServerTransport(
                    id,
                    context.Logger,
                    context.Memory,
                    handshakeMaxByteSize: handshakeMaxByteSize)
                : new RawReliableDirectClientTransport(
                    id,
                    context.Logger,
                    context.Memory,
                    handshakeMaxByteSize: handshakeMaxByteSize);
        }
    }
}
