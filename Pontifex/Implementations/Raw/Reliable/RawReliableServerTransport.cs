using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Base class for all RawReliable server transports. The generic parameter
    /// is the variant session-admission mechanism: Ack servers supply an
    /// acknowledger, NoAck servers a source handler factory. A server route
    /// delivers to an existing session; a new source is admitted by the variant
    /// <see cref="AdmitSession"/>.
    /// </summary>
    public abstract class RawReliableServerTransport<TFactory> : RawReliableTransport
        where TFactory : class
    {
        protected RawReliableServerTransport(string typeName, ILogger logger, IMemoryRental memory, RawReliableTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        protected bool TryInitializeServer(TFactory factory)
        {
            return TryInitialize(null, factory);
        }

        protected override IEndPoint? ClientRemoteEndPoint => null;

        protected override void ProcessServerInbound(IEndPoint source, UnionDataList message)
        {
            if (_stopping || !IsStarted)
            {
                message.Release();
                return;
            }

            if (_routes.TryGetValue(source, out var existing))
            {
                DeliverToEndpoint(existing, message);
                return;
            }

            AdmitSession(source, message);
        }

        /// <summary>
        /// Variant hook invoked on the dispatcher thread for the first inbound
        /// message from a new source route. The message ownership transfers to
        /// this method; it must be released or delivered exactly once.
        /// </summary>
        protected abstract void AdmitSession(IEndPoint source, UnionDataList message);
    }
}
