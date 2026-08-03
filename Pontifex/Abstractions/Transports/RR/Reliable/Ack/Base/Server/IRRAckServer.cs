namespace Pontifex.RR.Reliable.Ack
{
    public interface IRRAckServer : ITransport
    {
        bool Init(IRRServerAcknowledger<IRRAckServerHandler> acknowledger);
    }
}
