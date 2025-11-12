namespace Transport
{
    public interface ISynchronizedClientEndPointHandler : IClientEndPointHandler
    {
    }

    public interface ISynchronizedAcknowledger : IAcknowledger<ISynchronizedClientEndPointHandler>
    {
    }

    public interface ISynchronizedServer : IServer<ISynchronizedClientEndPointHandler>
    {
    }
}