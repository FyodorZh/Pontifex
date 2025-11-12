namespace Transport
{
    public interface IReconnectableClientEndPointHandler : IClientEndPointHandler
    {
        void OnReconnected(IClientEndPoint endPoint);
    }

    public interface IReconnectableAcknowledger : IAcknowledger<IReconnectableClientEndPointHandler>
    {
    }

    public interface IReconnectableServer : IServer<IReconnectableClientEndPointHandler>
    {
    }
}