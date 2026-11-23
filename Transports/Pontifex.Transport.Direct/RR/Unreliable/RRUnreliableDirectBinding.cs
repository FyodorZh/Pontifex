using System;
using Pontifex.Endpoints;
using Pontifex.Factory.Registration;
using Pontifex.Raw.Unreliable.Direct;
using Pontifex.RR.Direct;
using Pontifex.VirtualDelivery;

namespace Pontifex.RR.Unreliable.Direct
{
    /// <summary>
    /// Factory binding for the in-process RR unreliable transport.
    /// </summary>
    public static class RRUnreliableDirectBinding
    {
        public const string SchemeName = RRDirectInfo.UnreliableTransportName;

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RRUnreliable,
                new TransportParameter[]
                {
                    new StringParameter("id", required: true, positional: true),
                    new IntegerParameter(
                        "request_ttl",
                        required: false,
                        positional: false,
                        new IntegerType(1, int.MaxValue)),
                    new IntegerParameter(
                        "response_ttl",
                        required: false,
                        positional: false,
                        new IntegerType(1, int.MaxValue)),
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
                if (context.TryGetInt64("request_ttl", out _))
                {
                    throw new ArgumentException(
                        "The 'request_ttl' parameter is valid only for a client transport.",
                        nameof(context));
                }

                if (context.TryGetStruct("delivery", out _))
                {
                    throw new ArgumentException(
                        "The 'delivery' parameter is valid only for a client transport.",
                        nameof(context));
                }

                TimeSpan responseTtl = context.TryGetInt64("response_ttl", out long responseSeconds)
                    ? TimeSpan.FromSeconds(responseSeconds)
                    : RRDirectInfo.DefaultResponseTtl;

                return new RRUnreliableDirectServer(
                    id,
                    responseTtl,
                    context.Logger,
                    context.Memory);
            }

            if (context.TryGetInt64("response_ttl", out _))
            {
                throw new InvalidOperationException(
                    "The 'response_ttl' parameter is valid only for a server transport.");
            }

            TimeSpan requestTtl = context.TryGetInt64("request_ttl", out long requestSeconds)
                ? TimeSpan.FromSeconds(requestSeconds)
                : RRDirectInfo.DefaultRequestTtl;

            Func<IDeliverySystem>? clientToServerFactory = null;
            Func<IDeliverySystem>? serverToClientFactory = null;

            if (context.TryGetStruct("delivery", out ITransportStructValue delivery))
            {
                UnreliableDeliveryBinding.Validate(delivery);
                clientToServerFactory = () => UnreliableDeliveryBinding.Build(delivery, context.Memory);
                serverToClientFactory = () => UnreliableDeliveryBinding.Build(delivery, context.Memory);
            }

            return new RRUnreliableDirectClient(
                new StringEndPoint(id),
                requestTtl,
                context.Logger,
                context.Memory,
                clientToServerFactory,
                serverToClientFactory);
        }
    }
}
