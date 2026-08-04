using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Validates incoming client connections and creates per-client session handlers.
    /// Implemented by business logic.
    /// </summary>
    public interface IRawReliableAckServerAcknowledger<out THandler>
        where THandler : IRawReliableAckServerHandler
    {
        /// <summary>
        /// Identifies an incoming client and creates a new session for interaction.
        /// !!! It is guaranteed that calls to TryAck() never overlap concurrently.
        /// </summary>
        /// <param name="ackData">Client identification data.</param>
        /// <returns>Null if the client is not recognized; otherwise a client session handler.</returns>
        THandler? TryAck(UnionDataList ackData);
    }
}
