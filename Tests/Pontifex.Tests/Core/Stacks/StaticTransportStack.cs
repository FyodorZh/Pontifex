using Pontifex.Factory;

namespace Pontifex.Tests
{
    public class StaticTransportStack : ITransportStack
    {
        private readonly IDescription _transportDesc;

        public string Id { get; }

        public StaticTransportStack(string id, string transportUri)
        {
            Id = id;
            _transportDesc = TransportRegistry.DescriptionFactory.FromUri(transportUri);
        }

        public TransportFactory GetTransportFactory(bool failIfError = true) => new(_transportDesc, _transportDesc, failIfError);
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
