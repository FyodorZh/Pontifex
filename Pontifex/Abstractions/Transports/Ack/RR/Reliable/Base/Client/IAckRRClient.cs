namespace Pontifex.Ack.RR.Reliable
{
    public interface IAckRRClient : ITransport
    {
        bool Init(IAckRRClientHandler handler);
    }
}