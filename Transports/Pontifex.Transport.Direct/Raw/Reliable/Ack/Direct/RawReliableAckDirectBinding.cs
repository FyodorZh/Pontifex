using System;
using Pontifex.Factory.Registration;

namespace Pontifex.Raw.Reliable.Ack.Direct
{
    /// <summary>
    /// Factory binding for the in-memory reliable Ack transport.
    /// </summary>
    public static class RawReliableAckDirectBinding
    {
        public const string SchemeName = DirectInfo.TransportName;

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawReliableAck,
                new TransportParameter[]
                {
                    new StringParameter("id", required: true, positional: true)
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

            return context.Role == TransportRole.Server
                ? new RawReliableAckDirectServer(id, context.Logger, context.Memory)
                : new RawReliableAckDirectClient(id, context.Logger, context.Memory);
        }
    }
}
