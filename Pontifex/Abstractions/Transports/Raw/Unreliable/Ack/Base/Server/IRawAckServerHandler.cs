using Pontifex.Utils;

namespace Pontifex.Raw.Unreliable.Ack
{
    public interface IRawAckServerHandler : IRawAckBaseHandler
    {
        /// <summary>
        /// Called during the ACK handshake to produce the server's response
        /// to the connecting client's acknowledgement data.
        /// </summary>
        /// <param name="ackData">The outgoing ACK response data to be sent to the client.</param>
        void FillAckResponse(UnionDataList ackData);
    }
}