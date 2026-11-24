using System;
using Actuarius.Memory;
using Pontifex.Factory.Registration;
using Pontifex.VirtualDelivery;

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
                context => Create(context, UnreliableDeliveryBinding.CreateFactory));
        }

        private static ITransport Create(ITransportBuildContext context)
        {
            return Create(context, UnreliableDeliveryBinding.CreateFactory);
        }

        /// <summary>
        /// Creates the transport with an injectable delivery-factory builder.
        /// </summary>
        /// <param name="context">The build context.</param>
        /// <param name="factoryBuilder">A builder that validates the delivery value and returns a per-direction
        /// factory. Tests supply a counting or failing builder.</param>
        /// <returns>The constructed transport.</returns>
        internal static ITransport Create(ITransportBuildContext context,
            Func<ITransportStructValue, IMemoryRental, Func<IDeliverySystem>> factoryBuilder)
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
                transport.SetDeliverySystemFactory(
                    factoryBuilder(delivery, context.Memory),
                    factoryBuilder(delivery, context.Memory));
            }

            return transport;
        }
    }
}
