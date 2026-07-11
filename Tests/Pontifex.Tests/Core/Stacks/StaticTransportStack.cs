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
    }
}
