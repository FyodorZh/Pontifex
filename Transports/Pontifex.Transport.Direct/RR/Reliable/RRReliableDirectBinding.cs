using System;
using Pontifex.Endpoints;
using Pontifex.Factory.Registration;
using Pontifex.RR.Direct;

namespace Pontifex.RR.Reliable.Direct
{
    /// <summary>
    /// Factory binding for the in-process RR reliable transport.
    /// </summary>
    public static class RRReliableDirectBinding
    {
        public const string SchemeName = RRDirectInfo.ReliableTransportName;

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RRReliable,
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
                        new IntegerType(1, int.MaxValue))
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

                TimeSpan responseTtl = context.TryGetInt64("response_ttl", out long responseSeconds)
                    ? TimeSpan.FromSeconds(responseSeconds)
                    : RRDirectInfo.DefaultResponseTtl;

                return new RRReliableDirectServer(
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

            return new RRReliableDirectClient(
                new StringEndPoint(id),
                requestTtl,
                context.Logger,
                context.Memory);
        }
    }
}
