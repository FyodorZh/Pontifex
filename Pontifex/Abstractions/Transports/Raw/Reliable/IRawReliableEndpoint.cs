using Pontifex.Utils;

namespace Pontifex.Raw.Reliable
{
    public interface IRawReliableEndpoint : IRawEndpoint
    {
        /// <summary>
        /// Gets whether the endpoint is currently connected.
        /// May return true before OnConnected() is invoked (the endpoint reference
        /// is not available to the caller before OnConnected()).
        /// Guaranteed to return false during and after OnDisconnected() — the
        /// IsConnected transition to false is synchronized with the OnDisconnected() call.
        /// Safe to read concurrently.
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// Sends a message to the remote peer.
        /// Thread-safe. Concurrent successful sends on one endpoint are ordered by
        /// the transport's linearization order for those calls. Send does not wait
        /// for network delivery or peer handling; it returns after validation and
        /// outbound admission.
        /// Ownership of <paramref name="bufferToSend"/> transfers to the transport
        /// unconditionally, regardless of the returned SendResult. The caller MUST
        /// NOT read, mutate, retain, release, or retry using that buffer afterward.
        /// Synchronous failures (e.g., buffer full, message too big, not connected)
        /// are returned as a non-Ok SendResult and do NOT affect the connection.
        /// If a failure occurs asynchronously after the method returns Ok,
        /// the transport will be destroyed and OnDisconnected will be raised.
        /// </summary>
        /// <param name="bufferToSend">The data to send.</param>
        /// <returns>SendResult.Ok on success; other values indicate a synchronous failure.</returns>
        SendResult Send(UnionDataList bufferToSend);
        
        /// <summary>
        /// Initiates a logical disconnection of this endpoint with the given reason.
        /// </summary>
        /// <param name="reason">The reason for the disconnection.</param>
        /// <returns>True if the disconnect was initiated successfully.</returns>
        bool Disconnect(StopReason reason);
    }
}