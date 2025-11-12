using Shared;
using Shared.Utils;
using Transport.Abstractions;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Servers;
using Transport.Transports.ProtocolWrapper.AckRaw;

namespace Transport.Protocols.Zip.AckRaw
{
    public abstract class AckRawZipProducer
    {
        public string Name
        {
            get { return ZipInfo.TransportName; }
        }

        protected bool Parse(string @params, out int compressionLevel, out string nestedAddress)
        {
            if (@params != null && @params.Length >= 3 && @params[1] == ':')
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
                : base(ZipInfo.TransportName, transportToWrap, new LambdaConstructor<AckRawZipClientLogic>(() => new AckRawZipClientLogic(compressionLevel)))
            {
            }
        }

        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            int compressionLevel;
            string nestedAddress;
            if (Parse(@params, out compressionLevel, out nestedAddress))
            {
                IAckRawClient client = factory.Construct(nestedAddress, logicRunner) as IAckRawClient;
                if (client != null)
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
                    new LambdaConstructor<AcknowledgerWrapper<HandlerWrapper<AckRawZipServerLogic>>>(
                        () => new AcknowledgerWrapper<HandlerWrapper<AckRawZipServerLogic>>(
                            new LambdaConstructor<HandlerWrapper<AckRawZipServerLogic>>(
                                () => new HandlerWrapper<AckRawZipServerLogic>(
                                    new LambdaConstructor<AckRawZipServerLogic>(
                                        () => new AckRawZipServerLogic(compressionLevel)))))))
            {
            }
        }

        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            int compressionLevel;
            string nestedAddress;
            if (Parse(@params, out compressionLevel, out nestedAddress))
            {
                IAckRawServer server = factory.Construct(nestedAddress, logicRunner) as IAckRawServer;
                if (server != null)
                {
                    return new ZipServer(server, compressionLevel);
                }
            }

            return null;
        }
    }
}