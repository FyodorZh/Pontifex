using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Validates incoming client connections, creates per-client session handlers, and builds the ACK
    /// response returned to the client.
    /// Implemented by business logic.
    /// </summary>
    public interface IRawReliableAckServerAcknowledger<THandler>
        where THandler : IRawReliableAckServerHandler
    {
        /// <summary>
        /// Identifies an incoming client and creates a new session for interaction.
        /// !!! It is guaranteed that calls to TryAck() never overlap concurrently.
        /// This call receives an additional reference to <paramref name="ackData"/> and MUST release
        /// that reference exactly once after validation, including on an exceptional path. The caller
        /// retains its own reference, and mutations are visible to the caller.
        /// </summary>
        /// <param name="remote">The remote endpoint of the connecting client.</param>
        /// <param name="ackData">Client identification data.</param>
        /// <returns><see langword="null"/> if the client is not recognized; otherwise a fresh session
        /// handler and an owned ACK response. The transport owns the response after the call, so the
        /// acknowledger MUST NOT retain or release it after returning. A failure while constructing the
        /// response is a <c>TryAck</c> failure.</returns>
        (THandler handler, UnionDataList ackResponse)? TryAck(
            IEndPoint remote, UnionDataList ackData);
    }
}
