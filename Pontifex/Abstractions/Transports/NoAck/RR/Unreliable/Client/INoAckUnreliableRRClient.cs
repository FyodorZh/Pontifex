namespace Pontifex.NoAck.RR.Unreliable
{
    public interface INoAckUnreliableRRClient : INoAckRRClient
    {
        bool Init(INoAckUnreliableRRClientHandler handler);

        int MessageMaxByteSize { get; }
    }
}
