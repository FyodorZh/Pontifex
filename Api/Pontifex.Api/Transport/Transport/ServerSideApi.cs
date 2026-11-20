using System;
using Pontifex.Raw.Reliable;

namespace Pontifex.Api
{
    /// <summary>
    /// Server-side Api factory over a reliable raw transport.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Admission</b>: the transport-level admission decides how many sessions exist; the
    /// Api negotiation (<c>"api"</c>, version <c>1</c>) performed by each
    /// <see cref="ServerSideApiInstance{TApi}"/> decides whether that session may run the Api.</description></item>
    /// <item><description><b>Factory contract</b>: every invocation creates a fresh
    /// <see cref="ServerSideApiInstance{TApi}"/>, so the Api state is never shared between sessions.</description></item>
    /// </list>
    /// </remarks>
    public class ServerSideApiFactory<TApi> : IRawReliableServerHandlerFactory<IRawReliableServerHandler>
        where TApi : IApiRoot
    {
        private readonly Func<ServerSideApiInstance<TApi>> _instanceFactory;

        public ServerSideApiFactory(Func<ServerSideApiInstance<TApi>> instanceFactory)
        {
            _instanceFactory = instanceFactory;
        }

        public IRawReliableServerHandler? TryCreate(IEndPoint remote)
        {
            return _instanceFactory();
        }
    }
}
