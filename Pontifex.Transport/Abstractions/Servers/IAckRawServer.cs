using Transport.Abstractions.Acknowledgers;
using Transport.Abstractions.Handlers.Server;

namespace Transport.Abstractions.Servers
{
    public interface IAckRawServer : ITransport
    {
        bool Init(IRawServerAcknowledger<IAckRawServerHandler> acknowledger);
        
        int MessageMaxByteSize { get; }
    }

    public interface IAckUnreliableRawServer : IAckRawServer, Flags.IUnreliable
    {
    }

    public interface IAckReliableRawServer : IAckRawServer, Flags.IReliable
    { // TCP
    }
}