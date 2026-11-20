using System;
using Pontifex.Factory.Registration;

namespace Pontifex.Raw.Reliable.Logger
{
    /// <summary>
    /// Factory binding for the logging reliable wrapper.
    /// </summary>
    /// <remarks>
    /// The scheme is <c>log</c>. The wrapper is transparent: it adds no negotiation, no name/version, and
    /// no extra round trip.
    /// </remarks>
    public static class RawReliableLoggerBinding
    {
        /// <summary>
        /// The canonical scheme name of the reliable logging wrapper.
        /// </summary>
        public const string SchemeName = "log";

        /// <summary>
        /// Creates the registry entry that makes the <c>log</c> scheme available.
        /// </summary>
        /// <returns>The transport registration.</returns>
        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawReliable,
                new TransportParameter[]
                {
                    new NestedTransportParameter(
                        "nested",
                        required: true,
                        positional: true,
                        TransportTypeSet.Exactly(TransportType.RawReliable))
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
                ? new RawReliableServerLogger((IRawReliableServer)nested())
                : new RawReliableClientLogger((IRawReliableClient)nested());
        }
    }
}
