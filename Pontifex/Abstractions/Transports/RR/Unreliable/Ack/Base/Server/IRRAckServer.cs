namespace Pontifex.RR.Unreliable.Ack
{
    public interface IRRAckServer : ITransport
    {
        bool Init(IRRServerAcknowledger<IRRAckServerHandler> acknowledger);
    }
}
