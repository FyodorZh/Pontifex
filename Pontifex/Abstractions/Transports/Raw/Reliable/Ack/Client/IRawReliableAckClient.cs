namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Marker interface for a reliable (e.g. TCP) ACK raw client transport.
    /// Reliable transport guarantees in-order, lossless message delivery.
    /// </summary>
    public interface IRawReliableAckClient : IRawReliableTransport
    {        
        /// <summary>
        /// Initializes the client transport with the user-provided handler.
        /// </summary>
        /// <param name="handler">The handler that processes transport events.</param>
        /// <returns>True if initialization was successful.</returns>
        bool Init(IRawReliableAckClientHandler handler);
    }
}