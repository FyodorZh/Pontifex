namespace Transport.Abstractions.Clients
{
    public interface IAckRRClient : ITransport
    {
        bool Init(Handlers.Client.IAckRRClientHandler handler);
        // TODO: NotImplemented
    }

    public interface IAckUnreliableRRClient : IAckRRClient, Flags.IUnreliable
    {
        // TODO: NotImplemented
    }

    public interface IAckReliableRRClient : IAckRRClient, Flags.IReliable
    {
        // TODO: NotImplemented
    }
}