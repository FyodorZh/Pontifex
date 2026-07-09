using System;
using System.Collections.Generic;
using Pontifex.Ack.Raw;
using Pontifex.Factory;

namespace Pontifex.Protocols.Reconnectable.AckReliableRaw
{
    public class AckRawReconnectableConstructor : ITransportConstructor
    {
        public TransportType Type => TransportType.AckRawReliable;
        public string Name => ReconnectableInfo.TransportName;

        public ITransport ConstructClient(ITransportBuilder builder, IDescription description)
        {
            if (!description.Get("disconnect_timeout").EvaluateAsLong(out var timeoutLong))
                throw new ArgumentException("Missing 'disconnect_timeout' in reconnectable description");

            var disconnectTimeout = TimeSpan.FromSeconds(timeoutLong);

            if (!description.Get("nested").EvaluateAsDescription(out var nestedDesc))
                throw new ArgumentException("Missing 'nested' in reconnectable description");

            var innerDesc = nestedDesc;

            return new AckRawReconnectableClient(
                () => builder.Build<IAckRawReliableClient>(innerDesc),
                disconnectTimeout,
                builder.Logger,
                builder.MemoryRental);
        }

        public ITransport ConstructServer(ITransportBuilder builder, IDescription description)
        {
            if (!description.Get("disconnect_timeout").EvaluateAsLong(out var timeoutLong))
                throw new ArgumentException("Missing 'disconnect_timeout' in reconnectable description");

            var disconnectTimeout = TimeSpan.FromSeconds(timeoutLong);

            if (!description.Get("nested").EvaluateAsDescription(out var nestedDesc))
                throw new ArgumentException("Missing 'nested' in reconnectable description");

            return new AckRawReconnectableServer(builder.Build<IAckRawReliableServer>(nestedDesc), disconnectTimeout, builder.Logger, builder.MemoryRental);
        }

        public IEnumerable<(string name, Func<string, IDescriptionUriFactory, Description?> uriParser)> GetUriParsers()
        {
            yield return (ReconnectableInfo.TransportName, (uriBody, factory) =>
            {
                int pos = uriBody.IndexOf(':');
                if (pos < 0)
                    return null;

                if (!int.TryParse(uriBody.Substring(0, pos), out var timeoutSeconds) || timeoutSeconds <= 0)
                    return null;

                var nestedUri = uriBody.Substring(pos + 1);
                var nested = factory.ParseTransport(nestedUri);

                var desc = new Description();
                desc.Add("disconnect_timeout", new LongElement(timeoutSeconds));
                desc.Add("nested", new DescriptionElement(nested));
                desc.Add("type", new StringElement("AckRawReliable"));
                return desc;
            });
        }
    }
}
