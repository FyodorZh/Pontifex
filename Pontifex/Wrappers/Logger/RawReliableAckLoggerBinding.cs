using System;
using Pontifex.Factory.Registration;
using Pontifex.Raw.Reliable.Ack;

namespace Pontifex.Raw.Reliable.Ack.Logger
{
    /// <summary>
    /// Factory binding for the logging reliable Ack wrapper.
    /// </summary>
    public static class RawReliableAckLoggerBinding
    {
        public const string SchemeName = "log";

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawReliableAck,
                new TransportParameter[]
                {
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
            if (!context.TryGetTransport("nested", out var nested))
            {
                throw new InvalidOperationException(
                    "The resolver guarantees the required 'nested' parameter.");
            }

            return context.Role == TransportRole.Server
                ? new RawReliableAckServerLogger((IRawReliableAckServer)nested())
                : new RawReliableAckClientLogger((IRawReliableAckClient)nested());
        }
    }
}
