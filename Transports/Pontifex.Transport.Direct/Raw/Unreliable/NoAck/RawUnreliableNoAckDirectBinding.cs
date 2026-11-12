using System;
using Pontifex.Factory.Registration;
using Pontifex.Raw.Unreliable.Direct;

namespace Pontifex.Raw.Unreliable.NoAck.Direct
{
    /// <summary>
    /// Factory binding for the in-memory unreliable NoAck transport.
    /// </summary>
    public static class RawUnreliableNoAckDirectBinding
    {
        public const string SchemeName = RawUnreliableNoAckDirectInfo.TransportName;

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawUnreliableNoAck,
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
                return new RawUnreliableNoAckDirectServer(id, context.Logger, context.Memory);
            }

            var transport = new RawUnreliableNoAckDirectClient(id, context.Logger, context.Memory);
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
