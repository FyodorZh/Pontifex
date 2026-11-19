using System;
using Pontifex.Factory.Registration;
using Pontifex.Raw.Reliable.NoAck;

namespace Pontifex.Raw.Reliable.NoAck.Logger
{
    /// <summary>
    /// Factory binding for the logging reliable no-acknowledgement wrapper.
    /// </summary>
    /// <remarks>
    /// The scheme is <c>log-noack</c>, not <c>log</c>: the registry rejects a second canonical
    /// scheme name, so the no-acknowledgement wrapper mirrors the <c>tcp</c> / <c>tcp-noack</c>
    /// convention.
    /// </remarks>
    public static class RawReliableNoAckLoggerBinding
    {
        /// <summary>
        /// The canonical scheme name of the no-acknowledgement logging wrapper.
        /// </summary>
        public const string SchemeName = "log-noack";

        /// <summary>
        /// Creates the registry entry that makes the <c>log-noack</c> scheme available.
        /// </summary>
        /// <returns>The transport registration.</returns>
        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawReliableNoAck,
                new TransportParameter[]
                {
                    new NestedTransportParameter(
                        "nested",
                        required: true,
                        positional: true,
                        TransportTypeSet.Exactly(TransportType.RawReliableNoAck))
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
                ? new RawReliableNoAckServerLogger((IRawReliableNoAckServer)nested())
                : new RawReliableNoAckClientLogger((IRawReliableNoAckClient)nested());
        }
    }
}
