using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Ack.Raw.Reliable;
using Pontifex.Factory;
using Pontifex.Ack.Raw.Reliable.Protocols;
using Scriba;

namespace Pontifex.Ack.Raw.Reliable.Zip
{
    public class AckRawReliableZipConstructor : ITransportConstructor
    {
        public TransportType Type => TransportType.AckRawReliable;
        public string Name => ZipInfo.TransportName;

        private class ZipClient : AckRawReliableWrapperClient<AckRawReliableZipClientLogic>, IAckRawReliableClient
        {
            public ZipClient(IAckRawReliableClient transportToWrap, int compressionLevel)
                : base(ZipInfo.TransportName, transportToWrap,
                    (logger, memoryRental) => new AckRawReliableZipClientLogic(logger, memoryRental, compressionLevel))
            {
            }
        }

        private class ZipServer : AckRawReliableWrapperServer<AcknowledgerWrapper<HandlerWrapper<AckRawReliableZipServerLogic>>>, IAckRawReliableServer
        {
            public ZipServer(IAckRawReliableServer transportToWrap, int compressionLevel)
                : base(
                    ZipInfo.TransportName,
                    transportToWrap,
                    (logger, memory) => new AcknowledgerWrapper<HandlerWrapper<AckRawReliableZipServerLogic>>(
                        () => new HandlerWrapper<AckRawReliableZipServerLogic>(
                            () => new AckRawReliableZipServerLogic(logger, memory, compressionLevel))))
            {
            }
        }

        public ITransport ConstructClient(ITransportBuilder builder, IDescription description)
        {
            int compressionLevel = 9;
            if (description.Get("compression_level").EvaluateAsLong(out var level))
                compressionLevel = (int)level;

            if (!description.Get("nested").EvaluateAsDescription(out var nestedDesc))
                throw new ArgumentException("Missing 'nested' in zip description");

            return new ZipClient(builder.Build<IAckRawReliableClient>(nestedDesc), compressionLevel);
        }

        public ITransport ConstructServer(ITransportBuilder builder, IDescription description)
        {
            int compressionLevel = 9;
            if (description.Get("compression_level").EvaluateAsLong(out var level))
                compressionLevel = (int)level;

            if (!description.Get("nested").EvaluateAsDescription(out var nestedDesc))
                throw new ArgumentException("Missing 'nested' in zip description");

            return new ZipServer(builder.Build<IAckRawReliableServer>(nestedDesc), compressionLevel);
        }

        public IEnumerable<(string name, Func<string, IDescriptionUriFactory, Description?> uriParser)> GetUriParsers()
        {
            yield return (ZipInfo.TransportName, (uriBody, factory) =>
            {
                int compressionLevel = 9;
                string nestedUri = uriBody;

                if (uriBody.Length >= 3 && uriBody[1] == ':')
                {
                    if (uriBody[0] >= '0' && uriBody[0] <= '9')
                    {
                        compressionLevel = uriBody[0] - '0';
                        nestedUri = uriBody.Substring(2);
                    }
                }

                var nested = factory.ParseTransport(nestedUri);
                var desc = new Description();
                desc.Add("compression_level", new LongElement(compressionLevel));
                desc.Add("nested", new DescriptionElement(nested));
                desc.Add("type", new StringElement("AckRawReliable"));
                return desc;
            });
        }
    }
}
