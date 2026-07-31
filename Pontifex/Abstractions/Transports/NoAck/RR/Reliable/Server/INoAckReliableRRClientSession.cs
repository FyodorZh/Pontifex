using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Reliable
{
    public interface INoAckReliableRRClientSession
    {
        event System.Action<string> OnClosed;
        void OnRequested(UnionDataList data, INoAckReliableRRCallbackOnServer callback);
        void Close(string reason);
    }
}
