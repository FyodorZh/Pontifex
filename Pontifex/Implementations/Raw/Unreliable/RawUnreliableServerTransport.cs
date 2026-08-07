using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Base class for all RawUnreliable server transports. The generic parameter
    /// is the variant handler-factory delegate type: Ack servers supply a
    /// <see cref="Func{T1, T2, TResult}"/> receiving the triggering message,
    /// NoAck servers a <see cref="Func{T, TResult}"/> receiving only the source
    /// route. The type-safe factory invocation is the only variant difference.
    /// </summary>
    public abstract class RawUnreliableServerTransport<TFactory> : RawUnreliableTransport
        where TFactory : Delegate
    {
        protected RawUnreliableServerTransport(string typeName, ILogger logger, IMemoryRental memory,
            RawUnreliableTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        protected bool TryInitializeServer(TFactory factory)
        {
            return TryInitialize(null, factory);
        }

        protected sealed override IRawUnreliableHandler? InvokeHandlerFactory(IEndPoint source, UnionDataList triggeringMessage)
            => InvokeFactory((TFactory)HandlerFactory!, source, triggeringMessage);

        /// <summary>
        /// Type-safe variant factory invocation. NoAck invokes
        /// <c>factory(source)</c>; Ack invokes <c>factory(source, message)</c>.
        /// </summary>
        protected abstract IRawUnreliableHandler? InvokeFactory(TFactory factory, IEndPoint source, UnionDataList triggeringMessage);

        protected override IEndPoint? ClientRemoteEndPoint => null;
    }
}
