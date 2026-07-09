using Pontifex.Utils;

namespace Pontifex.NoAck.Raw_old
{
    public interface INoAckRawClientSideEndpoint : INoAckRawEndpoint
    {
        IEndPoint ServerAddress { get; }
        SendResult Send(UnionDataList message);
        
        /// <summary>
        /// Initiates a logical disconnection of this endpoint with the given reason.
        /// </summary>
        /// <param name="reason">The reason for the disconnection.</param>
        /// <returns>True if the disconnect was initiated successfully.</returns>
        bool Stop(StopReason reason);
    }
}