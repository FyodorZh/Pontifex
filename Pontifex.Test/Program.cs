using Scriba;
using Scriba.Consumers;
using Terminal.Gui;
using Terminal.UI;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Servers;
using Transport.Protocols.Monitoring.AckRaw;
using Transport.Protocols.Zip.AckRaw;
using Transport.Transports.Direct;
using TransportAnalyzer.TestLogic;
using Attribute = System.Attribute;

namespace Pontifex.Test
{
    class Program
    {
        private static readonly Transport.TransportFactory mClientFactory = new Transport.TransportFactory();
        private static readonly Transport.TransportFactory mServerFactory = new Transport.TransportFactory();
        
        static void Main(string[] args)
        {
            Colors.ColorSchemes["Toplevel"] = Colors.ColorSchemes["Base"] = new ColorScheme(
                new Terminal.Gui.Attribute(Color.Gray, Color.Blue), 
                new Terminal.Gui.Attribute(Color.BrightYellow, Color.Blue), 
                new Terminal.Gui.Attribute(Color.Gray, Color.Blue),
                new Terminal.Gui.Attribute(Color.BrightGreen, Color.Blue), 
                new Terminal.Gui.Attribute(Color.Green, Color.BrightGreen));
            Colors.ColorSchemes["Menu"] = new ColorScheme(
                new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan), 
                new Terminal.Gui.Attribute(Color.White, Color.Black), 
                new Terminal.Gui.Attribute(Color.BrightYellow, Color.BrightCyan), 
                new Terminal.Gui.Attribute(Color.DarkGray, Color.Gray), 
                new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black));
            UISystem.Run(new SessionView());

            return;
            
            
            
            
            
            
            Log.AddConsumer(new ConsoleConsumer(), true);
            
            Init();
            Console.WriteLine("Hello, World!");

            bool work = true;

            string url = "zip|9:log|direct|dir123";
            
            var server = mServerFactory.Construct(url);
            ((IAckRawServer)server).Init(new AckRawServerLogic());
            server.Start(r =>
            {
                Console.WriteLine("Server stopped " + r);
                work = false;
            }, StaticLogger.Instance);
            
            var client = mClientFactory.Construct(url);
            ((IAckRawClient)client).Init(new AckRawClientLogic(1, 1000));
            client.Start(r =>
            {
                Console.WriteLine("Client stopped " + r);
                work = false;
            }, StaticLogger.Instance);
            
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
    }
}