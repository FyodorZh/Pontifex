using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Ack
{
    public interface IRawReliableAckClientHandler : IRawReliableClientHandler
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
        void OnConnected(IRawReliableEndpoint endPoint, UnionDataList ackResponse);
        
        /// <summary>
        /// Prepares acknowledgement data to be sent to the server as part of the ACK handshake.
        /// </summary>
        /// <param name="ackData">The acknowledgement data to modify.</param>
        void FillAckData(UnionDataList ackData);
    }
}
