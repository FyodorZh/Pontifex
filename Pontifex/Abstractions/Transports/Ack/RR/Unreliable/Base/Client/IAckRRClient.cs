namespace Pontifex.Ack.RR.Unreliable
{
    public interface IAckRRClient : ITransport
    {
        bool Init(IAckRRClientHandler handler);
    }
}