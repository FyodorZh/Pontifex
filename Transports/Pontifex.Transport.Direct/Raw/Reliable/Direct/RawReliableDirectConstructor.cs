using System;
using System.Collections.Generic;
using Pontifex.Factory;
using Pontifex.Raw.Reliable.Ack;

namespace Pontifex.Raw.Reliable.Direct
{
    /// <summary>
    /// Base class for the RawReliable Direct transport constructors. Owns the
    /// description and URI parsing shared by the contract variants. The Direct
    /// transport uses the <c>direct://id</c> URI form (no delivery parameter).
    /// </summary>
    public abstract class RawReliableDirectConstructor : ITransportConstructor
    {
        public abstract TransportType Type { get; }

        public abstract string Name { get; }

        protected abstract RawReliableAckClientTransport CreateClient(ITransportBuilder builder, string id);

        protected abstract RawReliableAckServerTransport CreateServer(ITransportBuilder builder, string id);

        public ITransport ConstructServer(ITransportBuilder builder, IDescription description)
        {
            if (!description.Get("id").EvaluateAsString(out var id))
                throw new ArgumentException("Missing 'id' in description");

            return CreateServer(builder, id);
        }

        public ITransport ConstructClient(ITransportBuilder builder, IDescription description)
        {
            if (!description.Get("id").EvaluateAsString(out var id))
                throw new ArgumentException("Missing 'id' in description");

            return CreateClient(builder, id);
        }

        public IEnumerable<(string name, Func<string, IDescriptionUriFactory, Description?> uriParser)> GetUriParsers()
        {
            yield return (Name, (uriBody, factory) =>
            {
                var desc = new Description();
                desc.Add("id", new StringElement(uriBody));
                desc.Add("type", new StringElement("RawReliableAck"));
                return desc;
            });
        }
    }
}
