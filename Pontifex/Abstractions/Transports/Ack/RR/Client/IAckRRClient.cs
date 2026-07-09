namespace Pontifex.Ack.RR
{
    public interface IAckRRClient : ITransport
    {
        bool Init(IAckRRClientHandler handler);
        // TODO: NotImplemented
    }

    public interface IAckUnreliableRRClient : IAckRRClient
    {
        // TODO: NotImplemented
    }

    public interface IAckReliableRRClient : IAckRRClient
    {
        // TODO: NotImplemented
    }
}