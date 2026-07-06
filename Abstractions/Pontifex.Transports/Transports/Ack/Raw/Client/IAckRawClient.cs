namespace Pontifex.Ack.Raw
{
    /// <summary>
    /// Client-side transport for an ACK-based raw connection.
    /// </summary>
    public interface IAckRawClient : ITransport
    {
        /// <summary>
        /// Initializes the client transport with the user-provided handler.
        /// </summary>
        /// <param name="handler">The handler that processes transport events.</param>
        /// <returns>True if initialization was successful.</returns>
        bool Init(IAckRawClientHandler handler);

        /// <summary>
        /// Gets the maximum single-message size in bytes supported by the transport.
        /// </summary>
        int MessageMaxByteSize { get; }
    }

    /// <summary>
    /// Marker interface for an unreliable (e.g. UDP) ACK raw client transport.
    /// Unreliable transports may drop, reorder, or duplicate messages without notification.
    /// </summary>
    public interface IAckUnreliableRawClient : IAckRawClient
    {
    }

    /// <summary>
    /// Marker interface for a reliable (e.g. TCP) ACK raw client transport.
    /// Reliable transport guarantees in-order, lossless message delivery.
    /// </summary>
    public interface IAckReliableRawClient : IAckRawClient
    {
    }
}