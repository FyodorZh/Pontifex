using System;
using System.Collections.Generic;
using Pontifex.Factory;

namespace Pontifex.NoAck.Raw.Unreliable.Direct
{
    public class NoAckRawUnreliableDirectConstructor : ITransportConstructor
    {
        public TransportType Type => TransportType.NoAckRawUnreliable;
        public string Name => "direct-noack-raw-unreliable";

        public ITransport ConstructServer(ITransportBuilder builder, IDescription description)
        {
            if (!description.Get("id").EvaluateAsString(out var id))
                throw new ArgumentException("Missing 'id' in description");

            return new NoAckRawUnreliableDirectServer(id, builder.Logger, builder.MemoryRental);
        }

        public ITransport ConstructClient(ITransportBuilder builder, IDescription description)
        {
            if (!description.Get("id").EvaluateAsString(out var id))
                throw new ArgumentException("Missing 'id' in description");

            return new NoAckRawUnreliableDirectClient(id, builder.Logger, builder.MemoryRental);
        }

        public IEnumerable<(string name, Func<string, IDescriptionUriFactory, Description?> uriParser)> GetUriParsers()
        {
            yield return (Name, (uriBody, factory) =>
            {
                var desc = new Description();
                desc.Add("id", new StringElement(uriBody));
                desc.Add("type", new StringElement("NoAckRawUnreliable"));
                return desc;
            });
        }
    }
}
