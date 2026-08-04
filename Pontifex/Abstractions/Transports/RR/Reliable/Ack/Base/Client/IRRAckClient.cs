namespace Pontifex.RR.Reliable.Ack
{
    public interface IRRAckClient : ITransport
    {
        bool Init(IRRAckClientHandler handler);
    }
}