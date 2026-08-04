using System;
using Actuarius.Memory;
using Scriba;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Raw.Reliable.Ack.Direct;
using Pontifex.Raw.Reliable.Ack.Logger;
using Pontifex.Raw.Reliable.Ack.Reconnectable;
using Pontifex.Raw.Reliable.Ack.Tcp;
using Pontifex.Raw.Reliable.Ack.Zip;
using Pontifex.Converters;
using Pontifex.Factory;

namespace Pontifex.Test
{
    public class TransportFactory
    {
        private static readonly TransportBuilder mBuilder = new TransportBuilder(ConvertersGraph.Default);

        public TransportFactory()
        {
            mBuilder.RegisterTransport(new RawReliableAckDirectConstructor());
            mBuilder.RegisterTransport(new RawReliableAckTcpConstructor());
            mBuilder.RegisterTransport(new RawReliableAckZipConstructor());
            mBuilder.RegisterTransport(new RawReliableAckReconnectableConstructor());
            mBuilder.RegisterTransport(new RawReliableAckLoggerConstructor());
        }

        public IRawReliableAckServer? ConstructServer(string url, ILogger logger, IMemoryRental memoryRental)
        {
            var description = mBuilder.DescriptionFactory.FromUri("transport://" + url);
            return mBuilder.BuildServer(description, memoryRental, logger) as IRawReliableAckServer;
        }

        public IRawReliableAckClient? ConstructClient(string url, ILogger logger, IMemoryRental memoryRental)
        {
            var description = mBuilder.DescriptionFactory.FromUri("transport://" + url);
            return mBuilder.BuildClient(description, memoryRental, logger) as IRawReliableAckClient;
        }
    }
}
