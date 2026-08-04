namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Marker interface for a reliable (e.g. TCP) ACK raw server transport.
    /// Reliable transport guarantees in-order, lossless message delivery.
    /// </summary>
    public interface IRawReliableAckServer : IRawReliableTransport
    {        
        /// <summary>
        /// Initializes the server transport with the given acknowledger.
        /// The acknowledger validates incoming client connections and creates
        /// per-client session handlers.
        /// </summary>
        /// <param name="acknowledger">The acknowledger that validates and creates client sessions.</param>
        /// <returns>True if initialization was successful.</returns>
        bool Init(IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler> acknowledger);
    }
}