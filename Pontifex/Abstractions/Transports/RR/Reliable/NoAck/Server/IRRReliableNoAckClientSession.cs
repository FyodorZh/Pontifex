using Pontifex.Utils;

namespace Pontifex.RR.Reliable.NoAck
{
    public interface IRRReliableNoAckClientSession
    {
        event System.Action<string> OnClosed;
        void OnRequested(UnionDataList data, IRRReliableNoAckCallbackOnServer callback);
        void Close(string reason);
    }
}
