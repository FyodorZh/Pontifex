using System;
using System.Collections.Generic;
using System.Text.Json;
using Pontifex.Factory;
using Pontifex.VirtualDelivery;

namespace Pontifex.Raw.Unreliable.Direct
{
    /// <summary>
    /// Base class for the RawUnreliable Direct transport constructors. Owns the
    /// description parsing, client delivery-system wiring, and URI parsing shared
    /// by the Ack and NoAck contract variants.
    /// </summary>
    public abstract class RawUnreliableDirectConstructor : ITransportConstructor
    {
        public abstract TransportType Type { get; }

        public abstract string Name { get; }

        protected abstract RawUnreliableDirectClientTransport CreateClient(ITransportBuilder builder, string id);

        protected abstract IRawUnreliableTransport CreateServer(ITransportBuilder builder, string id);

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
            if (!description.Get("delivery").EvaluateAsDescription(out var descriptionInfo))
                throw new ArgumentException("Missing 'delivery' in description");

            var transport = CreateClient(builder, id);
            var pool = builder.MemoryRental.CollectablePool;
            var bytesPool = builder.MemoryRental.ByteArraysPool;
            transport.SetDeliverySystem(
                DeliverySystemFactory.Build(descriptionInfo, pool, bytesPool) ?? new PerfectDeliverySystem(),
                DeliverySystemFactory.Build(descriptionInfo, pool, bytesPool) ?? new PerfectDeliverySystem());
            return transport;
        }

        public IEnumerable<(string name, Func<string, IDescriptionUriFactory, Description?> uriParser)> GetUriParsers()
        {
            yield return (Name, (uriBody, factory) =>
            {
                var pos = uriBody.IndexOf(':');
                var serverName = uriBody.Substring(0, pos);
                var paramsJson = uriBody.Substring(pos);
                var deliveryDescription = factory.FromJson(JsonElement.Parse(paramsJson));

                var desc = new Description();
                desc.Add("id", new StringElement(serverName));
                desc.Add("type", new StringElement(Type.ToString()));
                desc.Add("delivery", new DescriptionElement(deliveryDescription));
                return desc;
            });
        }
    }
}
