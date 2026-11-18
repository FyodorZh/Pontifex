namespace Pontifex.Raw.Reliable
{
    public interface IRawReliableServerHandler : IRawReliableHandler
    {
        /// <summary>
        /// Logical connect. Informs business logic that the transport is fully configured
        /// and ready for use. Indicates successful client connection to the server
        /// (a session is created on the server side).
        /// The expected lifecycle after a successful connection is:
        /// OnConnected -> OnDisconnected -> OnStopped.
        /// </summary>
        /// <param name="endPoint">The endpoint to the remote agent.</param>
        void OnConnected(IRawReliableEndpoint endPoint);
    }
}
