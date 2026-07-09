using Pontifex.Utils;

namespace Pontifex.NoAck.RR
{
    public interface INoAckRRServerHandler : IHandler
    {
        void Started();
        void Stopped();
    }
    
    public interface INoAckUnreliableRRServerHandler : INoAckRRServerHandler
    {
        void OnRequest(IEndPoint client, UnionDataList message);
    }

    public interface INoAckReliableRRClientSession
    {
        event System.Action<string> OnClosed;
        void OnRequested(UnionDataList data, INoAckReliableRRCallbackOnServer callback);
        void Close(string reason);
    }

    public interface INoAckReliableRRServerHandler : IHandler
    {
        INoAckReliableRRClientSession OpenSession(IEndPoint client);
    }
}