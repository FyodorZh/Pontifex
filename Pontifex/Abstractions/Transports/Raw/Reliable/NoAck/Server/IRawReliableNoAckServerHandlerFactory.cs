namespace Pontifex.Raw.Reliable.NoAck
{
    /// <summary>
    /// Validates incoming client connections and creates per-client session handlers.
    /// Implemented by business logic.
    /// </summary>
    public interface IRawReliableNoAckServerHandlerFactory<out THandler>
        where THandler : IRawReliableNoAckServerHandler
    {
        /// <summary>
        /// Identifies an incoming client and creates a new session for interaction.
        /// !!! It is guaranteed that calls to TryCreate() never overlap concurrently.
        /// The returned handler owns no transport-supplied buffers: this call has no
        /// acknowledgment payload, so there is nothing to populate or release.
        /// </summary>
        /// <param name="remote">The remote endpoint of the connecting client.</param>
        /// <returns>Null if the client is not recognized, which rejects the connection;
        /// otherwise a fresh client session handler.</returns>
        THandler? TryCreate(IEndPoint remote);
    }
}
