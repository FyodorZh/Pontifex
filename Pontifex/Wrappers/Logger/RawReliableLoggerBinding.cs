using System;
using Pontifex.Factory.Registration;

namespace Pontifex.Raw.Reliable.Logger
{
    /// <summary>
    /// Factory binding for the logging reliable wrapper.
    /// </summary>
    /// <remarks>
    /// The scheme is <c>log-noack</c> until the final cleanup: the registry rejects a second canonical
    /// scheme name, so the reliable wrapper mirrors the <c>tcp</c> / <c>tcp-noack</c>
    /// convention.
    /// </remarks>
    public static class RawReliableLoggerBinding
    {
        /// <summary>
        /// The canonical scheme name of the reliable logging wrapper.
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
