using Pontifex.Factory;

namespace Pontifex.Tests
{
    public class DynamicTransportStack : ITransportStack
    {
        private readonly Func<string> _uriProvider;
        
        public string Id { get; private set; }
        
        public DynamicTransportStack(string id, Func<string> uriProvider)
        {
            _uriProvider = uriProvider;
            Id = id;
        }

        public TransportFactory GetTransportFactory(bool failIfError = true)
        {
            var desc = TransportRegistry.DescriptionFactory.FromUri(_uriProvider());
            return new(desc, desc, failIfError);
        }
    }
}