using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Client-side handler for the Reliable ACK raw transport.
    /// Controls a single client-server connection.
    /// </summary>
    public interface IRawReliableAckClientHandler : IRawAckClientHandler
    {
        /// <summary>
        /// Logical connection. Informs the business logic that the transport is fully configured
        /// and ready for use. Indicates successful client connection to the server
        /// (a session is created on the server side).
        /// The expected lifecycle after a successful connection is:
        /// OnConnected -> OnDisconnected -> OnStopped.
        /// </summary>
        /// <param name="endPoint">The endpoint to the remote agent.</param>
        /// <param name="ackResponse">The server's response to the client's AckData.</param>
        void OnConnected(IRawReliableAckClientSideEndpoint endPoint, UnionDataList ackResponse);
    }
}