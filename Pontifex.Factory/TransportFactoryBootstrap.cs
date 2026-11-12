using Pontifex.Converters;

namespace Pontifex.Factory
{
    // The only public creation seam for the Transport Formula pipeline. Concrete
    // implementations stay internal; callers receive interfaces only.
    public static class TransportFactoryBootstrap
    {
        public static ITransportFactoryBuilder CreateBuilder(
            IConvertersGraph? converters = null)
        {
            return new Bootstrap.TransportFactoryBuilder(
                converters ?? ConvertersGraph.Default);
        }
    }
}
