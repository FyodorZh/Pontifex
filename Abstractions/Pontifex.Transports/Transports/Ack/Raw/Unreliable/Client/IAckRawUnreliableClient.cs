namespace Pontifex.Ack.Raw
{
    /// <summary>
    /// Marker interface for an unreliable (e.g. UDP) ACK raw client transport.
    /// Unreliable transports may drop, reorder, or duplicate messages without notification.
    /// </summary>
    public interface IAckRawUnreliableClient : IAckRawClient
    {
        /// <summary>
        /// Initializes the client transport with the user-provided handler.
        /// </summary>
        /// <param name="handler">The handler that processes transport events.</param>
        /// <returns>True if initialization was successful.</returns>
        bool Init(IAckRawUnreliableClientHandler handler);
    }
}