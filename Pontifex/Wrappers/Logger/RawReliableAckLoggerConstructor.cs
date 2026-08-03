using System;
using System.Collections.Generic;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Factory;

namespace Pontifex.Raw.Reliable.Ack.Logger
{
    public class RawReliableAckLoggerConstructor : ITransportConstructor
    {
        public TransportType Type => TransportType.RawReliableAck;
        public string Name => "log";

        public ITransport ConstructClient(ITransportBuilder builder, IDescription description)
        {
            if (!description.Get("nested").EvaluateAsDescription(out var nestedDesc))
                throw new ArgumentException("Missing 'nested' in logger description");

            return new RawReliableAckClientLogger(builder.Build<IRawReliableAckClient>(nestedDesc));
        }

        public ITransport ConstructServer(ITransportBuilder builder, IDescription description)
        {
            if (!description.Get("nested").EvaluateAsDescription(out var nestedDesc))
                throw new ArgumentException("Missing 'nested' in logger description");

            return new RawReliableAckServerLogger(builder.Build<IRawReliableAckServer>(nestedDesc));
        }

        public IEnumerable<(string name, Func<string, IDescriptionUriFactory, Description?> uriParser)> GetUriParsers()
        {
            yield return ("log", (uriBody, factory) =>
            {
                var nested = factory.ParseTransport(uriBody);
                var desc = new Description();
                desc.Add("nested", new DescriptionElement(nested));
                desc.Add("type", new StringElement("RawReliableAck"));
                return desc;
            });
        }
    }
}
