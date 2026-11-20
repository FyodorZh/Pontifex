using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Protocols
{
    /// <summary>
    /// Convenience base for <see cref="IRawReliableWrapperLogic"/> with shared logging and memory ownership.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Negotiation defaults</b>: the client appends no data, the server accepts every
    /// structure-valid request, the server response is empty, and the accepted hook is a no-op. Layers
    /// override only the hooks they actually use.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawReliableWrapperLogic : IRawReliableWrapperLogic
    {
        /// <summary>
        /// Gets the logging system used by the wrapper logic.
        /// </summary>
        protected ILogger Log { get; }

        /// <summary>
        /// Gets the memory rental system used to build negotiation response tails.
        /// </summary>
        protected IMemoryRental Memory { get; }

        /// <summary>
        /// Initializes the logic base.
        /// </summary>
        /// <param name="logger">The logging system. Must be non-null.</param>
        /// <param name="memoryRental">The memory rental system. Must be non-null.</param>
        protected RawReliableWrapperLogic(ILogger logger, IMemoryRental memoryRental)
        {
            Log = logger ?? throw new ArgumentNullException(nameof(logger));
            Memory = memoryRental ?? throw new ArgumentNullException(nameof(memoryRental));
        }

        /// <inheritdoc/>
        public abstract void OnConnected();

        /// <inheritdoc/>
        public abstract void OnDisconnected();

        /// <inheritdoc/>
        public abstract bool ProcessReceivedData(UnionDataList receivedData);

        /// <inheritdoc/>
        public abstract bool ProcessSentData(UnionDataList sentData);

        /// <inheritdoc/>
        public abstract void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null);

        /// <inheritdoc/>
        public virtual void FillNegotiationData(UnionDataList customData)
        {
            // The default client request has no custom elements.
        }

        /// <inheritdoc/>
        public virtual void OnNegotiationAccepted(UnionDataList customData)
        {
            // The default response tail carries no custom elements.
        }

        /// <inheritdoc/>
        public virtual UnionDataList CreateNegotiationResponse()
        {
            return Memory.CollectablePool.Acquire<UnionDataList>();
        }

        /// <inheritdoc/>
        public virtual bool CheckNegotiationData(UnionDataList customData)
        {
            return true;
        }
    }
}
