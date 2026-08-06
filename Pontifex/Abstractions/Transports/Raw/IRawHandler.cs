using Pontifex.Utils;

namespace Pontifex.Raw
{
    public interface IRawHandler : IHandler
    {
        /// <summary>
        /// Called when data arrives from the remote peer.
        /// </summary>
        void OnReceived(UnionDataList receivedBuffer);
    }
}