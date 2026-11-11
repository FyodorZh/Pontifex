namespace Pontifex.Factory
{
    // The only public creation seam for the Transport Formula pipeline. Concrete
    // implementations stay internal; callers receive interfaces only.
    public static class TransportFactoryBootstrap
    {
        public static ITransportFactoryBuilder CreateBuilder()
        {
            return new Bootstrap.TransportFactoryBuilder();
        }
    }
}
