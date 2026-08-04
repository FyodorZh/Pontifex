namespace Pontifex.RR.Unreliable.NoAck
{
    public interface IRRNoAckServer : ITransport
    {
        int MessageMaxByteSize { get; }
    }
}
