using System;
using System.Collections.Generic;
using Pontifex.Factory;

namespace Pontifex.Ack.Raw.Reliable.Direct
{
    public class AckRawDirectConstructor : ITransportConstructor
    {
        public TransportType Type => TransportType.AckRawReliable;
        public string Name => DirectInfo.TransportName;

        public ITransport ConstructServer(ITransportBuilder builder, IDescription description)
        {
            if (!description.Get("id").EvaluateAsString(out var id))
                throw new ArgumentException("Missing 'id' in description");

            return new AckRawDirectServer(id, builder.Logger, builder.MemoryRental);
        }

        public ITransport ConstructClient(ITransportBuilder builder, IDescription description)
        {
            if (!description.Get("id").EvaluateAsString(out var id))
                throw new ArgumentException("Missing 'id' in description");

            return new AckRawDirectClient(id, builder.Logger, builder.MemoryRental);
        }

        public IEnumerable<(string name, Func<string, IDescriptionUriFactory, Description?> uriParser)> GetUriParsers()
        {
            yield return (DirectInfo.TransportName, (uriBody, factory) =>
            {
                var desc = new Description();
                desc.Add("id", new StringElement(uriBody));
                desc.Add("type", new StringElement("AckRawReliable"));
                return desc;
            });
        }
    }
}
