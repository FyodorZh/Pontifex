using System;
using Pontifex.Factory.Registration;

namespace Pontifex.Raw.Unreliable.Direct
{
    /// <summary>
    /// Factory binding for the in-memory unreliable transport.
    /// </summary>
    public static class RawUnreliableDirectBinding
    {
        public const string SchemeName = RawUnreliableDirectInfo.TransportName;

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawUnreliable,
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
                if (context.TryGetStruct("delivery", out _))
                {
                    throw new ArgumentException(
                        "The 'delivery' parameter is valid only for a client transport.",
                        nameof(context));
                }

                return new RawUnreliableDirectServerTransport(id, context.Logger, context.Memory);
            }

            var transport = new RawUnreliableDirectClientTransport(id, context.Logger, context.Memory);
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
