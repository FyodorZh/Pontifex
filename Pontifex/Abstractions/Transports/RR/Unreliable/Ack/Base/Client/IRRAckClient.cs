namespace Pontifex.RR.Unreliable.Ack
{
    public interface IRRAckClient : ITransport
    {
        bool Init(IRRAckClientHandler handler);
    }
}