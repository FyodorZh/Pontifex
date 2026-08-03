using System;
using Actuarius.Memory;
using Scriba;
using Pontifex.Ack.Raw.Reliable;
using Pontifex.Ack.Raw.Reliable.Direct;
using Pontifex.Ack.Raw.Reliable.Logger;
using Pontifex.Ack.Raw.Reliable.Reconnectable;
using Pontifex.Ack.Raw.Reliable.Tcp;
using Pontifex.Ack.Raw.Reliable.Zip;
using Pontifex.Converters;
using Pontifex.Factory;

namespace Pontifex.Test
{
    public class TransportFactory
    {
        private static readonly TransportBuilder mBuilder = new TransportBuilder(ConvertersGraph.Default);

        public TransportFactory()
        {
            mBuilder.RegisterTransport(new AckRawReliableDirectConstructor());
            mBuilder.RegisterTransport(new AckRawReliableTcpConstructor());
            mBuilder.RegisterTransport(new AckRawReliableZipConstructor());
            mBuilder.RegisterTransport(new AckRawReliableReconnectableConstructor());
            mBuilder.RegisterTransport(new AckRawReliableLoggerConstructor());
        }

        public IAckRawReliableServer? ConstructServer(string url, ILogger logger, IMemoryRental memoryRental)
        {
            var description = mBuilder.DescriptionFactory.FromUri("transport://" + url);
            return mBuilder.BuildServer(description, memoryRental, logger) as IAckRawReliableServer;
        }

        public IAckRawReliableClient? ConstructClient(string url, ILogger logger, IMemoryRental memoryRental)
        {
            var description = mBuilder.DescriptionFactory.FromUri("transport://" + url);
            return mBuilder.BuildClient(description, memoryRental, logger) as IAckRawReliableClient;
        }
    }
}
