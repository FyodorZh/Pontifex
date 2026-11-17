namespace Pontifex.Raw.Reliable.NoAck
{
    /// <summary>
    /// Client-side handler for a reliable no-acknowledgement raw transport.
    /// </summary>
    public interface IRawReliableNoAckClientHandler : IRawReliableClientHandler
    {
        /// <summary>
        /// Logical connection. Informs the business logic that the transport is fully configured
        /// and ready for use. Indicates successful client connection to the server
        /// (a session is created on the server side).
        /// The expected lifecycle after a successful connection is:
        /// OnConnected -> OnDisconnected -> OnStopped.
        /// </summary>
        /// <param name="endPoint">The endpoint to the remote agent.</param>
        void OnConnected(IRawReliableEndpoint endPoint);
    }
}
