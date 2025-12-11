using Transport.Abstractions.Clients;
using Transport.Abstractions.Servers;
using Transport.Protocols.Monitoring.AckRaw;
using Transport.Protocols.Zip.AckRaw;
using Transport.Transports.Direct;

namespace Pontifex.Test
{
    public class TransportFactory
    {
        private static readonly Transport.TransportFactory mClientFactory = new Transport.TransportFactory();
        private static readonly Transport.TransportFactory mServerFactory = new Transport.TransportFactory();

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

        public IAckRawServer? ConstructServer(string url)
        {
            return mServerFactory.Construct(url) as IAckRawServer;
        }

        public IAckRawClient? ConstructClient(string url)
        {
            return mClientFactory.Construct(url) as IAckRawClient;
        }
    }
}