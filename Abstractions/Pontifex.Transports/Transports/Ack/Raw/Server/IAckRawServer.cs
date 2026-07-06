namespace Pontifex.Ack.Raw
{
    /// <summary>
    /// Server-side transport for an ACK-based raw connection.
    /// Initializes the server with an acknowledger that validates and creates sessions
    /// for incoming clients.
    /// Implemented by the transport system.
    /// </summary>
    public interface IAckRawServer : ITransport
    {
        /// <summary>
        /// Initializes the server transport with the given acknowledger.
        /// The acknowledger validates incoming client connections and creates
        /// per-client session handlers.
        /// </summary>
        /// <param name="acknowledger">The acknowledger that validates and creates client sessions.</param>
        /// <returns>True if initialization was successful.</returns>
        bool Init(IRawServerAcknowledger<IAckRawServerHandler> acknowledger);

        /// <summary>
        /// Gets the maximum message size in bytes supported by the transport.
        /// </summary>
        int MessageMaxByteSize { get; }
    }

    /// <summary>
    /// Marker interface for an unreliable (e.g. UDP) ACK raw server transport.
    /// </summary>
    public interface IAckUnreliableRawServer : IAckRawServer
    {
    }

    /// <summary>
    /// Marker interface for a reliable (e.g. TCP) ACK raw server transport.
    /// Reliable transport guarantees in-order, lossless message delivery.
    /// </summary>
    public interface IAckReliableRawServer : IAckRawServer
    {
    }
}