using System;
using Actuarius.Memory;
using Scriba;
using Pontifex.Ack.Raw;
using Pontifex.Ack.Raw.Reliable.Direct;
using Pontifex.Converters;
using Pontifex.Factory;
using Pontifex.Protocols.Monitoring.AckRaw;
using Pontifex.Protocols.Reconnectable.AckReliableRaw;
using Pontifex.Protocols.Zip;
using Pontifex.Transports.Tcp;

namespace Pontifex.Test
{
    public class TransportFactory
    {
        private static readonly TransportBuilder mBuilder = new TransportBuilder(ConvertersGraph.Default);

        public TransportFactory()
        {
            mBuilder.RegisterTransport(new AckRawDirectConstructor());
            mBuilder.RegisterTransport(new AckRawTcpConstructor());
            mBuilder.RegisterTransport(new AckRawZipConstructor());
            mBuilder.RegisterTransport(new AckRawReconnectableConstructor());
            mBuilder.RegisterTransport(new AckRawLoggerConstructor());
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
