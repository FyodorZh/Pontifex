namespace Pontifex.Abstractions.Clients
{
    public interface IAckRawClient : ITransport
    {
        bool Init(Handlers.Client.IAckRawClientHandler handler);

        int MessageMaxByteSize { get; }
    }

    public interface IAckUnreliableRawClient : IAckRawClient, Flags.IUnreliable
    {
    }

    public interface IAckReliableRawClient : IAckRawClient, Flags.IReliable
    {
    }
}