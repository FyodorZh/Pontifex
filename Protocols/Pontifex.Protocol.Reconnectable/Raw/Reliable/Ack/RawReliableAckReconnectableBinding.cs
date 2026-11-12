using System;
using Pontifex.Factory.Registration;
using Pontifex.Raw.Reliable.Ack;

namespace Pontifex.Raw.Reliable.Ack.Reconnectable
{
    /// <summary>
    /// Factory binding for the reconnectable reliable Ack wrapper.
    /// </summary>
    public static class RawReliableAckReconnectableBinding
    {
        public const string SchemeName = ReconnectableInfo.TransportName;

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawReliableAck,
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
                        TransportTypeSet.Exactly(TransportType.RawReliableAck))
                },
                Create);
        }

        private static ITransport Create(ITransportBuildContext context)
        {
            if (!context.TryGetInt64("disconnect_timeout", out var timeoutSeconds))
            {
                throw new InvalidOperationException(
                    "The resolver guarantees the required 'disconnect_timeout' parameter.");
            }

            if (!context.TryGetTransport("nested", out var nested))
            {
                throw new InvalidOperationException(
                    "The resolver guarantees the required 'nested' parameter.");
            }

            var disconnectTimeout = TimeSpan.FromSeconds(timeoutSeconds);

            return context.Role == TransportRole.Server
                ? new RawReliableAckReconnectableServer(
                    (IRawReliableAckServer)nested(),
                    disconnectTimeout,
                    context.Logger,
                    context.Memory)
                : new RawReliableAckReconnectableClient(
                    () => (IRawReliableAckClient)nested(),
                    disconnectTimeout,
                    context.Logger,
                    context.Memory);
        }
    }
}
