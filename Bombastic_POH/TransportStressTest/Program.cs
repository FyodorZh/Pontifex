using System.Threading;
using LogConsumers;
using Microsoft.Extensions.Configuration;
using NewProtocol;
using Shared;
using Shared.Utils;
using Transport;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Controls;
using Transport.Abstractions.Servers;
using Transport.Protocols.Monitoring.AckRaw;
using Transport.Protocols.Reconnectable.AckReliableRaw;
using Transport.Protocols.Reliable.AckRaw;
using Transport.Protocols.Zip.AckRaw;
using Transport.Transports.Direct;
using Transport.Transports.RUdp;
using Transport.Transports.Tcp;
using Transport.Transports.Udp;


namespace TransportStressTest
{
    public class Params
    {
        public string Server { get; set; }
        public string Client { get; set; }

        public int Clients { get; set; } = 1;

        public int BlockSize { get; set; } = 300;

        public int Period { get; set; } = 50;
    }

    class Program
    {
        static void Main(string[] args)
        {
            Log.AddConsumer(new ConsoleConsumer(), true);
            Log.AddConsumer(new StudioDebugConsumer(), true);

            var configBuilder = new ConfigurationBuilder().AddCommandLine(args);
            IConfigurationRoot configuration = configBuilder.Build();

            Params p = new Params();
            configuration.Bind(p);


            if (p.Server != null && p.Client == null)
            {
                RunAsServer(p);
            }
            else if (p.Server == null && p.Client != null)
            {
                RunAsClient(p);
            }
            else
            {
                Log.i("Self test mode using direct transport");

                p.Client = "direct|stress";
                p.Client = "rudp2|127.0.0.1:10001/10";
                p.Server = p.Client;

                RunAsServer(p);
                Thread.Sleep(500);
                RunAsClient(p);
            }


            while (true)
            {
                Thread.Sleep(1);
            }
        }

        private static void RunAsServer(Params @params)
        {
            var period = DeltaTime.FromMiliseconds(1);
            WorkStoryProcessor processor = new WorkStoryProcessor(new PeriodicLogicThreadedRunner(Log.VoidLogger), 3, period,
                (story, state) => { Log.i("{0} -> {1}", story, state); },
                (story, thread) => { Log.e("Hung"); });

            new PeriodicLogicThreadedDriver(DeltaTime.FromMiliseconds(1)).Start(processor, Log.VoidLogger);

            TransportFactory factory  = new TransportFactory();

            factory.Register(new AckRawDirectServerProducer());
            factory.Register(new AckRawLoggerServerProducer());
            factory.Register(new AckRawReconnectableServerProducer());
            factory.Register(new AckRawZipServerProducer());
            factory.Register(new AckRawTcpServerProducer());
            factory.Register(new AckRawReliableServerProducerNew());
            factory.Register(new NoAckUnreliableRawUdpServerProducer());
            factory.Register(new AckReliableRawUdpProducer());

            var runner = new PeriodicLogicWorkStoryRunner(processor, DeltaTime.FromSeconds(1000), Log.VoidLogger);

            IAckRawServer server = (IAckRawServer)factory.Construct(@params.Server, runner);

            var protoFactory = new StressTestProtocolFactory(new VoidHashDB(), server);

            if (!protoFactory.Start(Log.StaticLogger, DeltaTime.FromMiliseconds(10), 1))
            {
                Log.e("Failed to start server " + @params.Server);
            }
            else
            {
                Log.i("Started server on " + @params.Server);
            }

            runner.Run(new ServerMonitor(server), DeltaTime.FromMiliseconds(2000));
        }

        class ServerMonitor : IPeriodicLogic
        {
            private readonly IAckRawServer mServer;

            private ICCUController mCCUCollector;
            private ITrafficCollector mTrafficCollector;
            private IDeliveryController mDeliveryController;

            public ServerMonitor(IAckRawServer server)
            {
                mServer = server;
            }

            public bool LogicStarted(ILogicDriverCtl driver)
            {
                mCCUCollector = mServer.TryGetControl<ICCUController>();
                mTrafficCollector = mServer.TryGetControl<ITrafficCollector>();
                mDeliveryController = mServer.TryGetControl<IDeliveryController>();
                return true;
            }

            public void LogicTick()
            {
                if (mCCUCollector != null && mTrafficCollector != null && mDeliveryController != null)
                {
                    Log.i("CCU={0}, InPackets={1} OutPackets={2} | InTraffic={3}KB OutTraffic={4}KB Delivered/Attempts={5}/{6}",
                        mCCUCollector.CCU,
                        mTrafficCollector.InPacketsSpeed, mTrafficCollector.OutPacketsSpeed,
                        mTrafficCollector.InSpeed / 1024, mTrafficCollector.OutSpeed / 1024,
                        mDeliveryController.DeliveredPS, mDeliveryController.AttemptsPS);
                }
            }

            public void LogicStopped()
            {
            }
        }

        private static void RunAsClient(Params @params)
        {
            var period = DeltaTime.FromMiliseconds(1);
            WorkStoryProcessor processor = new WorkStoryProcessor(
                new PeriodicLogicThreadedRunner(Log.VoidLogger),
                (@params.Clients + 99) / 100,
                period,
                (story, state) => { Log.i("{0} -> {1}", story, state); },
                (story, thread) => { Log.e("Hung"); });

            new PeriodicLogicThreadedDriver(DeltaTime.FromMiliseconds(1)).Start(processor, Log.VoidLogger);

            TransportFactory factory  = new TransportFactory();

            factory.Register(new AckRawDirectClientProducer());
            factory.Register(new AckRawLoggerClientProducer());
            factory.Register(new AckRawReconnectableClientProducer());
            factory.Register(new AckRawZipClientProducer());
            factory.Register(new AckRawTcpClientProducer());
            factory.Register(new AckRawReliableClientProducerNew());
            factory.Register(new NoAckUnreliableRawUdpClientProducer());
            factory.Register(new AckReliableRawUdpProducer());

            var runner = new PeriodicLogicWorkStoryRunner(processor, DeltaTime.FromSeconds(20), Log.VoidLogger);

            Log.i("Connecting {0} clients to {1}", @params.Clients, @params.Client);
            for (int i = 0; i < @params.Clients; ++i)
            {
                IAckRawClient client = (IAckRawClient)factory.Construct(@params.Client, runner);

                ClientSideStressTest clientProtocol = new ClientSideStressTest(client, @params.BlockSize, @params.Period);

                clientProtocol.Connected +=() => {  };
                clientProtocol.Stopped += () => {  };

                runner.Run(clientProtocol, DeltaTime.FromMiliseconds(10));
            }
            Log.i("OK");
        }
    }
}