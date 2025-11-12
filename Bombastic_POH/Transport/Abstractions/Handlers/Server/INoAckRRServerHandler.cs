using Shared;
using Transport.Abstractions.Endpoints.Server;

namespace Transport.Abstractions.Handlers.Server
{
    public interface INoAckUnreliableRRServerHandler : IHandler
    {
        //void Started(); // todo add
        //void Stoped(); // todo add

        void OnRequest(IEndPoint client, Message message);
    }

    public interface INoAckReliableRRClientSession
    {
        event System.Action<string> OnClosed;
        void OnRequested(DeliveryId id, IMultiRefByteArray data, INoAckReliableRRCallback callback);
        void Close(string reason);
    }

    public interface INoAckReliableRRServerHandler : IHandler
    {
        INoAckReliableRRClientSession OpenSession(IEndPoint client);
    }
}