namespace Pontifex.Tests
{
    public class StaticTransportStack : ITransportStack
    {
        private readonly string _transportFormula;

        public string Id { get; }

        public StaticTransportStack(string id, string transportFormula)
        {
            Id = id;
            _transportFormula = transportFormula;
        }

        public TransportFactory GetTransportFactory(bool failIfError = true) => new(_transportFormula, _transportFormula, failIfError);
        public (int size, int concurrency) GetSmallTestSize()
        {
            return (100, 10);
        }

        public (int size, int concurrency) GetBigTestSize()
        {
            return (1000, 100);
        }
    }
}
