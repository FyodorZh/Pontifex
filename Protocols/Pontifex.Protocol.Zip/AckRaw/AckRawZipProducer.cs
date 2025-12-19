using Actuarius.Memory;
using Actuarius.PeriodicLogic;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Clients;
using Pontifex.Abstractions.Servers;
using Scriba;

namespace Pontifex.Protocols.Zip
{
    public abstract class AckRawZipProducer
    {
        public string Name => ZipInfo.TransportName;

        protected bool Parse(string @params, out int compressionLevel, out string nestedAddress)
        {
            if (@params.Length >= 3 && @params[1] == ':')
            {
                if (@params[0] >= '0' && @params[0] <= '9')
                {
                    compressionLevel = (@params[0] - '0');
                    nestedAddress = @params.Substring(2);
                    return true;
                }

                compressionLevel = 0;
                nestedAddress = "";
                return false;
            }
            compressionLevel = 9;
            nestedAddress = @params;
            return true;
        }
    }

    public class AckRawZipClientProducer : AckRawZipProducer, ITransportProducer
    {
        private class ZipClient : AckRawWrapperClient<AckRawZipClientLogic>, IAckReliableRawClient
        {
            public ZipClient(IAckRawClient transportToWrap, int compressionLevel)
                : base(ZipInfo.TransportName, transportToWrap, 
                    (logger, memoryRental) => new AckRawZipClientLogic(logger, memoryRental, compressionLevel))
            {
            }
        }

        public ITransport? Produce(string @params, ITransportFactory factory, ILogger? logger, IMemoryRental? memoryRental, IPeriodicLogicRunner? logicRunner)
        {
            if (Parse(@params, out var compressionLevel, out var nestedAddress))
            {
                if (factory.Construct(nestedAddress, logger, memoryRental, logicRunner) is IAckRawClient client)
                {
                    return new ZipClient(client, compressionLevel);
                }
            }
            return null;
        }
    }

    public class AckRawZipServerProducer : AckRawZipProducer, ITransportProducer
    {
        private class ZipServer : AckRawWrapperServer<AcknowledgerWrapper<HandlerWrapper<AckRawZipServerLogic>>>, IAckReliableRawServer
        {
            public ZipServer(IAckRawServer transportToWrap, int compressionLevel)
                : base(
                    ZipInfo.TransportName,
                    transportToWrap,
                    (logger, memory) => new AcknowledgerWrapper<HandlerWrapper<AckRawZipServerLogic>>(
                        () => new HandlerWrapper<AckRawZipServerLogic>(
                            () => new AckRawZipServerLogic(logger, memory, compressionLevel))))
            {
            }
        }

        public ITransport? Produce(string @params, ITransportFactory factory, ILogger? logger, IMemoryRental? memoryRental, IPeriodicLogicRunner? logicRunner)
        {
            if (Parse(@params, out var compressionLevel, out var nestedAddress))
            {
                if (factory.Construct(nestedAddress, logger, memoryRental, logicRunner) is IAckRawServer server)
                {
                    return new ZipServer(server, compressionLevel);
                }
            }

            return null;
        }
    }
}