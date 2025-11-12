using Shared;
using Shared.Utils;
using Transport.Abstractions;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Servers;
using Transport.Transports.ProtocolWrapper.AckRaw;

namespace Transport.Protocols.Monitoring.AckRaw
{
    public class AckRawMonitoringClientProducer : ITransportProducer
    {
        public string Name
        {
            get { return MonitoringInfo.TransportName; }
        }

        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            IAckRawClient server = factory.Construct(@params) as IAckRawClient;
            if (server != null)
            {
                return new AckRawWrapperClient<AckRawMonitoringClientLogic>(MonitoringInfo.TransportName, server, DefaultConstructor<AckRawMonitoringClientLogic>.Instance);
            }
            return null;
        }
    }

    public class AckRawMonitoringServerProducer : ITransportProducer
    {
        public string Name
        {
            get { return MonitoringInfo.TransportName; }
        }

        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            IAckRawServer server = factory.Construct(@params) as IAckRawServer;
            if (server != null)
            {
                return new AckRawWrapperServer<AcknowledgerWrapper<HandlerWrapper<AckRawMonitoringServerLogic>>>(
                    MonitoringInfo.TransportName,
                    server,
                    new LambdaConstructor<AcknowledgerWrapper<HandlerWrapper<AckRawMonitoringServerLogic>>>(
                        () => new AcknowledgerWrapper<HandlerWrapper<AckRawMonitoringServerLogic>>(
                            new LambdaConstructor<HandlerWrapper<AckRawMonitoringServerLogic>>(() => new HandlerWrapper<AckRawMonitoringServerLogic>(
                                DefaultConstructor<AckRawMonitoringServerLogic>.Instance))))); // Прошу прощения, но так получилось
            }
            return null;
        }
    }
}