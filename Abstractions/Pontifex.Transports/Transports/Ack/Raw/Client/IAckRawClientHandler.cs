using Pontifex.Utils;

namespace Pontifex.Ack.Raw
{
    /// <summary>
    /// Client-side handler for the ACK raw transport.
    /// Controls a single client-server connection.
    /// </summary>
    public interface IAckRawClientHandler : IAckRawBaseHandler
    {
        /// <summary>
        /// Prepares acknowledgement data to be sent to the server as part of the ACK handshake.
        /// </summary>
        /// <param name="ackData">The acknowledgement data to modify.</param>
        void FillAckData(UnionDataList ackData);
        
        /// <summary>
        /// Logical connection. Informs the business logic that the transport is fully configured
        /// and ready for use. Indicates successful client connection to the server
        /// (a session is created on the server side).
        /// The expected lifecycle after a successful connection is:
        /// OnConnected -> OnDisconnected -> OnStopped.
        /// </summary>
        /// <param name="endPoint">The endpoint to the remote agent.</param>
        /// <param name="ackResponse">The server's response to the client's AckData.</param>
        void OnConnected(IAckRawClientSideEndpoint endPoint, UnionDataList ackResponse);

        /// <summary>
        /// Called when the client-server connection is finally destroyed.
        /// If OnConnected() was previously triggered, the call sequence will be:
        /// OnDisconnected() followed by OnStopped().
        /// If OnConnected() was never triggered, only OnStopped() is called.
        /// </summary>
        void OnStopped(StopReason reason);
    }
}
