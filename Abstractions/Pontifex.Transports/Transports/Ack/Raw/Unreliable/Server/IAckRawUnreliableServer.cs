namespace Pontifex.Ack.Raw
{
    /// <summary>
    /// Marker interface for an unreliable (e.g. UDP) ACK raw server transport.
    /// </summary>
    public interface IAckRawUnreliableServer : IAckRawServer
    {
        /// <summary>
        /// Initializes the server transport with the given acknowledger.
        /// The acknowledger validates incoming client connections and creates
        /// per-client session handlers.
        /// </summary>
        /// <param name="acknowledger">The acknowledger that validates and creates client sessions.</param>
        /// <returns>True if initialization was successful.</returns>
        bool Init(IRawServerAcknowledger<IAckRawUnreliableServerHandler> acknowledger);
    }
}