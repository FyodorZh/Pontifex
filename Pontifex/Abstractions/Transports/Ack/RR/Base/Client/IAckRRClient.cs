namespace Pontifex.Ack.RR
{
    public interface IAckRRClient : ITransport
    {
        bool Init(IAckRRClientHandler handler);
    }
}