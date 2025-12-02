using LogConsumers;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Servers;
using Transport.Protocols.Monitoring.AckRaw;
using Transport.Transports.Direct;
using TransportAnalyzer.TestLogic;

namespace Pontifex.Test
{
    class Program
    {
        private static readonly Transport.TransportFactory mClientFactory = new Transport.TransportFactory();
        private static readonly Transport.TransportFactory mServerFactory = new Transport.TransportFactory();
        
        static void Main(string[] args)
        {
            Log.AddConsumer(new ConsoleConsumer(), true);
            
            Init();
            Console.WriteLine("Hello, World!");

            bool work = true;

            string url = "log|direct|dir123";
            
            var server = mServerFactory.Construct(url);
            ((IAckRawServer)server).Init(new AckRawServerLogic());
            server.Start(r =>
            {
                Console.WriteLine("Server stopped " + r);
                work = false;
            }, Log.StaticLogger);
            
            var client = mClientFactory.Construct(url);
            ((IAckRawClient)client).Init(new AckRawClientLogic(10, 1000));
            client.Start(r =>
            {
                Console.WriteLine("Client stopped " + r);
                work = false;
            }, Log.StaticLogger);
            
            while (work)
            {
                Thread.Sleep(100);
            }
            
        }
        
        
        private static void Init()
        {
            mClientFactory.Register(new AckRawDirectClientProducer());
            mServerFactory.Register(new AckRawDirectServerProducer());

            // mClientFactory.Register(new AckRawMonitoringClientProducer());
            // mServerFactory.Register(new AckRawMonitoringServerProducer());
            //
            // mClientFactory.Register(new AckRawZipClientProducer());
            // mServerFactory.Register(new AckRawZipServerProducer());
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
    }
}