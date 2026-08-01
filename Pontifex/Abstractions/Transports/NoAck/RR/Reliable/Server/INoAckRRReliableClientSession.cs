using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Reliable
{
    public interface INoAckRRReliableClientSession
    {
        event System.Action<string> OnClosed;
        void OnRequested(UnionDataList data, INoAckRRReliableCallbackOnServer callback);
        void Close(string reason);
    }
}
