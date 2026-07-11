namespace Pontifex.Tests
{
    public interface ITransportStack
    {
        string Id { get; }
        TransportFactory GetTransportFactory(bool failIfError = true);
    }
}