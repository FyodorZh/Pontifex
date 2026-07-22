using System;
using System.Collections.Generic;
using System.Text.Json;
using Pontifex.Factory;
using Pontifex.VirtualDelivery;

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
            if (!description.Get("delivery").EvaluateAsDescription(out var descriptionInfo))
                throw new ArgumentException("Missing 'delivery' in description");

            var transport = new NoAckRawUnreliableDirectClient(id, builder.Logger, builder.MemoryRental);
            var pool = builder.MemoryRental.CollectablePool;
            var bytesPool = builder.MemoryRental.ByteArraysPool;
            transport.SetDeliverySystem(
                DeliverySystemFactory.Build(descriptionInfo, pool, bytesPool),
                DeliverySystemFactory.Build(descriptionInfo, pool, bytesPool));
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
                desc.Add("type", new StringElement("NoAckRawUnreliable"));
                desc.Add("delivery", new DescriptionElement(deliveryDescription));
                return desc;
            });
        }
    }
}
