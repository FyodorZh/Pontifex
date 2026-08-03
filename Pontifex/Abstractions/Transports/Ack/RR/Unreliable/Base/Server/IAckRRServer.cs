namespace Pontifex.Ack.RR.Unreliable
{
    public interface IAckRRServer : ITransport
    {
        bool Init(IRRServerAcknowledger<IAckRRServerHandler> acknowledger);
    }
}
