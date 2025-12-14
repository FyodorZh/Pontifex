namespace Pontifex.Abstractions.Servers
{
    public interface IAckRRServer : ITransport
    {
        bool Init(Acknowledgers.IRRServerAcknowledger<Handlers.Server.IAckRRServerHandler> acknowledger);
    }

    public interface IAckUnreliableRRServer : IAckRRServer, Flags.IUnreliable
    {
    }

    public interface IAckReliableRRServer : IAckRRServer, Flags.IReliable
    { // TCP-response/request
    }
}
