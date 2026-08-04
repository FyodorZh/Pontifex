namespace Pontifex.Raw.Unreliable.Ack
{
    /// <summary>
    /// Client-side transport for an ACK-based raw connection.
    /// </summary>
    public interface IRawAckClient : ITransport
    {
        /// <summary>
        /// Gets the maximum single-message size in bytes supported by the transport.
        /// </summary>
        int MessageMaxByteSize { get; }
    }
}