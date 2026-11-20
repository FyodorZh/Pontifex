using System;
using Pontifex.Factory.Registration;

namespace Pontifex.Raw.Reliable.Reconnectable
{
    /// <summary>
    /// Factory binding for the reconnectable reliable wrapper.
    /// </summary>
    public static class RawReliableReconnectableBinding
    {
        public const string SchemeName = ReconnectableInfo.TransportName;

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawReliable,
                new TransportParameter[]
                {
                    new IntegerParameter(
                        "disconnect_timeout",
                        required: true,
                        positional: true,
                        new IntegerType(1, long.MaxValue)),
                    new NestedTransportParameter(
                        "nested",
                        required: true,
                        positional: true,
                        TransportTypeSet.Exactly(TransportType.RawReliable)),
                    new IntegerParameter(
                        "handshake_max_byte_size",
                        required: false,
                        positional: false,
                        new IntegerType(1, int.MaxValue))
                },
                Create);
        }

        private static ITransport Create(ITransportBuildContext context)
        {
            if (!context.TryGetInt64("disconnect_timeout", out long timeoutSeconds))
            {
                throw new InvalidOperationException(
                    "The resolver guarantees the required 'disconnect_timeout' parameter.");
            }

            if (!context.TryGetTransport("nested", out NestedTransportFactory nested))
            {
                throw new InvalidOperationException(
                    "The resolver guarantees the required 'nested' parameter.");
            }

            int handshakeMaxByteSize = ReconnectableInfo.DefaultHandshakeMaxByteSize;
            if (context.TryGetInt64("handshake_max_byte_size", out long configuredMax))
            {
                handshakeMaxByteSize = (int)configuredMax;
            }

            TimeSpan disconnectTimeout = TimeSpan.FromSeconds(timeoutSeconds);

            return context.Role == TransportRole.Server
                ? new RawReliableReconnectableServer(
                    (IRawReliableServer)nested(),
                    disconnectTimeout,
                    context.Logger,
                    context.Memory,
                    ReconnectableInfo.NegotiationVersion,
                    null,
                    handshakeMaxByteSize,
                    null,
                    null)
                : new RawReliableReconnectableClient(
                    () => nested() as IRawReliableClient,
                    disconnectTimeout,
                    context.Logger,
                    context.Memory,
                    ReconnectableInfo.NegotiationVersion,
                    null,
                    handshakeMaxByteSize,
                    null,
                    null,
                    null);
        }
    }
}
