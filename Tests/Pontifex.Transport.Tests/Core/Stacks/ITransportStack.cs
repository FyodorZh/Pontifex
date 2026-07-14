namespace Pontifex.Tests
{
    public interface ITransportStack
    {
        string Id { get; }
        TransportFactory GetTransportFactory(bool failIfError = true);

        (int size, int concurrency) GetSmallTestSize();
        (int size, int concurrency) GetBigTestSize();

    }
}