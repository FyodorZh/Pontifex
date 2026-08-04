namespace Pontifex.RR.Reliable.NoAck
{
    public interface IRRNoAckServer : ITransport
    {
        int MessageMaxByteSize { get; }
    }
}
