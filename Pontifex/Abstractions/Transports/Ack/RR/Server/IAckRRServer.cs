namespace Pontifex.Ack.RR
{
    public interface IAckRRServer : ITransport
    {
        bool Init(IRRServerAcknowledger<IAckRRServerHandler> acknowledger);
    }

    public interface IAckUnreliableRRServer : IAckRRServer
    {
    }

    public interface IAckReliableRRServer : IAckRRServer
    { // TCP-response/request
    }
}
