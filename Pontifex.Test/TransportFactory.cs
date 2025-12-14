using Actuarius.Memory;
using Scriba;
using Pontifex.Abstractions.Clients;
using Pontifex.Abstractions.Servers;
using Pontifex.Protocols.Monitoring.AckRaw;
using Pontifex.Protocols.Zip.AckRaw;
using Pontifex.Transports.Direct;

namespace Pontifex.Test
{
    public class TransportFactory
    {
        private static readonly Pontifex.TransportFactory mClientFactory = new Pontifex.TransportFactory();
        private static readonly Pontifex.TransportFactory mServerFactory = new Pontifex.TransportFactory();

        public TransportFactory()
        {
            mClientFactory.Register(new AckRawDirectClientProducer());
            mServerFactory.Register(new AckRawDirectServerProducer());

            //mClientFactory.Register(new AckRawMonitoringClientProducer());
            //mServerFactory.Register(new AckRawMonitoringServerProducer());
            //
            mClientFactory.Register(new AckRawZipClientProducer());
            mServerFactory.Register(new AckRawZipServerProducer());
            //
            // mClientFactory.Register(new AckRawTcpClientProducer());
            // mServerFactory.Register(new AckRawTcpServerProducer());
            //
            // mClientFactory.Register(new NoAckRRUdpClientProducer());
            // mServerFactory.Register(new NoAckRRUdpServerProducer());
            //
            // mClientFactory.Register(new NoAckUnreliableRawUdpClientProducer());
            // mServerFactory.Register(new NoAckUnreliableRawUdpServerProducer());
            //
            // mClientFactory.Register(new AckRawRUdpClientProducer());
            // mServerFactory.Register(new AckRawRUdpServerProducer());
            //
            // mClientFactory.Register(new AckReliableRawUdpProducer());
            // mServerFactory.Register(new AckReliableRawUdpProducer());
            //
            // //mClientFactory.Register(new AckRawReliableClientProducerNew());
            // //mServerFactory.Register(new AckRawReliableServerProducerNew());
            //
            // mClientFactory.Register(new AckRawReconnectableClientProducer());
            // mServerFactory.Register(new AckRawReconnectableServerProducer());
            
            mClientFactory.Register(new AckRawLoggerClientProducer());
            mServerFactory.Register(new AckRawLoggerServerProducer());
        }

        public IAckRawServer? ConstructServer(string url, ILogger logger, IMemoryRental memoryRental)
        {
            return mServerFactory.Construct(url, logger, memoryRental) as IAckRawServer;
        }

        public IAckRawClient? ConstructClient(string url, ILogger logger, IMemoryRental memoryRental)
        {
            return mClientFactory.Construct(url, logger, memoryRental) as IAckRawClient;
        }
    }
}