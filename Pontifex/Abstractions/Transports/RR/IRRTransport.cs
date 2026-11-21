namespace Pontifex.RR
{
    /// <summary>
    /// Common contract of the RR transport family: every RR transport exchanges requests and responses.
    /// </summary>
    /// <remarks>
    /// The reliability guarantees and the exact request/response model are defined by the concrete
    /// family, <see cref="Reliable.IRRReliableTransport"/> or
    /// <see cref="Unreliable.IRRUnreliableTransport"/>.
    /// </remarks>
    public interface IRRTransport : ITransport
    {
        /// <summary>
        /// Inclusive maximum payload size of one message, in bytes. Transport framing is not included.
        /// </summary>
        int MessageMaxByteSize { get; }
    }
}
