using Actuarius.Memory;
using Scriba;

namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Base class for the unreliable raw transports of the endpoint-owned architecture.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The class is a thin marker facade over
    /// <see cref="RawTransport"/>. It adds the unreliable transport contract and a typed view of the
    /// unreliable transport conformance control; lifecycle, carrier, handling, and routing behaviour
    /// belong to its client and server children.</description></item>
    /// <item><description><b>Difference from the legacy hierarchy</b>: the legacy base deliberately did
    /// not expose <see cref="IRawUnreliableTransport"/>; this class does, mirroring
    /// <c>RawReliableTransport</c>.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawUnreliableTransport : RawTransport, IRawUnreliableTransport
    {
        /// <summary>
        /// Gets the unreliable transport conformance control through its public contract.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The control is also published through
        /// <see cref="AnyTransport.GetControls"/>. It carries the relocated
        /// <c>BeforeHandlerFactoryGate</c> and <c>BeforeHandlerStartedGate</c> and forwards
        /// <c>TryMakeReliable</c> to the carrier.</description></item>
        /// </list>
        /// </remarks>
        protected new RawUnreliableTransportConformanceControl Conformance
            => (RawUnreliableTransportConformanceControl)base.Conformance;

        /// <summary>
        /// Initializes a new unreliable transport.
        /// </summary>
        /// <param name="typeName">The name of the concrete transport.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used by the transport.</param>
        /// <param name="conformanceControl">The conformance control shared with the transport's
        /// endpoints; a new control is created when <see langword="null"/>.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The control is retained, not disposed, by the
        /// transport.</description></item>
        /// </list>
        /// </remarks>
        protected RawUnreliableTransport(string typeName, ILogger logger, IMemoryRental memory,
            RawUnreliableTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl ?? new RawUnreliableTransportConformanceControl())
        {
        }
    }
}
