namespace Pontifex.Ack.RR.Reliable
{
    public interface IAckRRServer : ITransport
    {
        bool Init(IRRServerAcknowledger<IAckRRServerHandler> acknowledger);
    }
}
