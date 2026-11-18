namespace Pontifex.Raw.Reliable.Ack
{
    public interface IRawReliableAckServerHandler : IRawReliableServerHandler
    {
        /// <summary>
        /// Logical connect. Informs business logic that the transport is fully configured
        /// and ready for use. Indicates successful client connection to the server
        /// (a session is created on the server side).
        /// </summary>
        /// <param name="endPoint">The endpoint to the remote agent.</param>
        void OnConnected(IRawReliableEndpoint endPoint);
    }
}