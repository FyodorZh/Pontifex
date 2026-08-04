using System;
using System.Collections.Generic;
using Pontifex.Factory;

namespace Pontifex.Raw.Reliable.Ack.Direct
{
    public class RawReliableAckDirectConstructor : ITransportConstructor
    {
        public TransportType Type => TransportType.RawReliableAck;
        public string Name => DirectInfo.TransportName;

        public ITransport ConstructServer(ITransportBuilder builder, IDescription description)
        {
            if (!description.Get("id").EvaluateAsString(out var id))
                throw new ArgumentException("Missing 'id' in description");

            return new RawReliableAckDirectServer(id, builder.Logger, builder.MemoryRental);
        }

        public ITransport ConstructClient(ITransportBuilder builder, IDescription description)
        {
            if (!description.Get("id").EvaluateAsString(out var id))
                throw new ArgumentException("Missing 'id' in description");

            return new RawReliableAckDirectClient(id, builder.Logger, builder.MemoryRental);
        }

        public IEnumerable<(string name, Func<string, IDescriptionUriFactory, Description?> uriParser)> GetUriParsers()
        {
            yield return (DirectInfo.TransportName, (uriBody, factory) =>
            {
                var desc = new Description();
                desc.Add("id", new StringElement(uriBody));
                desc.Add("type", new StringElement("RawReliableAck"));
                return desc;
            });
        }
    }
}
