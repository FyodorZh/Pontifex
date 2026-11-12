using System;
using Pontifex.Factory.Registration;
using Pontifex.Raw.Unreliable.Direct;

namespace Pontifex.Raw.Unreliable.Ack.Direct
{
    /// <summary>
    /// Factory binding for the in-memory unreliable Ack transport.
    /// </summary>
    public static class RawUnreliableAckDirectBinding
    {
        public const string SchemeName = RawUnreliableAckDirectInfo.TransportName;

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawUnreliableAck,
                new TransportParameter[]
                {
                    new StringParameter("id", required: true, positional: true),
                    UnreliableDeliveryBinding.CreateParameter()
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

            if (context.Role == TransportRole.Server)
            {
                return new RawUnreliableAckDirectServer(id, context.Logger, context.Memory);
            }

            var transport = new RawUnreliableAckDirectClient(id, context.Logger, context.Memory);
            if (context.TryGetStruct("delivery", out var delivery))
            {
                transport.SetDeliverySystem(
                    UnreliableDeliveryBinding.Build(delivery, context.Memory),
                    UnreliableDeliveryBinding.Build(delivery, context.Memory));
            }

            return transport;
        }
    }
}
