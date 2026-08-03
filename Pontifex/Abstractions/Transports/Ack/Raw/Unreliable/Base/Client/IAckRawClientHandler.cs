using Pontifex.Utils;

namespace Pontifex.Ack.Raw.Unreliable
{
    public interface IAckRawClientHandler : IAckRawBaseHandler
    {
        /// <summary>
        /// Prepares acknowledgement data to be sent to the server as part of the ACK handshake.
        /// </summary>
        /// <param name="ackData">The acknowledgement data to modify.</param>
        void FillAckData(UnionDataList ackData);

        /// <summary>
        /// Called when the client-server connection is finally destroyed.
        /// If OnConnected() was previously triggered, the call sequence will be:
        /// OnDisconnected() followed by OnStopped().
        /// If OnConnected() was never triggered, only OnStopped() is called.
        /// </summary>
        void OnStopped(StopReason reason);
    }
}
