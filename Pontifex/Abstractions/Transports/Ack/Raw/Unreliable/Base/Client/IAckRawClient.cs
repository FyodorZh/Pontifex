namespace Pontifex.Ack.Raw.Unreliable
{
    /// <summary>
    /// Client-side transport for an ACK-based raw connection.
    /// </summary>
    public interface IAckRawClient : ITransport
    {
        /// <summary>
        /// Gets the maximum single-message size in bytes supported by the transport.
        /// </summary>
        int MessageMaxByteSize { get; }
    }
}