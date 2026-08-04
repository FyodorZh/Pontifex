namespace Pontifex.Raw.Unreliable
{
    public interface IRawUnreliableHandler : IRawHandler
    {
        void OnStarted(IRawUnreliableEndpoint endpoint);
    }
}