namespace Pontifex.Ack.RR
{
    public interface IAckRRServer : ITransport
    {
        bool Init(IRRServerAcknowledger<IAckRRServerHandler> acknowledger);
    }
}
