namespace Pontifex.Tests
{
    public class DynamicTransportStack : ITransportStack
    {
        private readonly int _smallTestSize;
        private readonly int _smallConcurrency;
        private readonly int _bigTestSize;
        private readonly int _bigConcurrency;
        private readonly Func<string> _uriProvider;

        public string Id { get; }
        
        public DynamicTransportStack(string id, 
            (int smallTestSize, int smallConcurrency) small, (int bigTestSize, int bigConcurrency) big, 
            Func<string> uriProvider)
        {
            (_smallTestSize, _smallConcurrency) = small;
            (_bigTestSize, _bigConcurrency) = big;
            _uriProvider = uriProvider;
            Id = id;
        }

        public TransportFactory GetTransportFactory(bool failIfError = true)
        {
            var desc = TransportRegistry.DescriptionFactory.FromUri(_uriProvider());
            return new(desc, desc, failIfError);
        }
        
        public (int size, int concurrency) GetSmallTestSize()
        {
            return (_smallTestSize, _smallConcurrency);
        }

        public (int size, int concurrency) GetBigTestSize()
        {
            return (_bigTestSize, _bigConcurrency);
        }

        public override string ToString()
        {
            return Id;
        }
    }
}