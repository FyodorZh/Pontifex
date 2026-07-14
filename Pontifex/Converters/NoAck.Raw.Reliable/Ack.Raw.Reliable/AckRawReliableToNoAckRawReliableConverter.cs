using System;
using Actuarius.Memory;
using Pontifex.Ack.Raw;
using Pontifex.NoAck.Raw;
using Scriba;

namespace Pontifex.Converters
{
    public class AckRawReliableToNoAckRawReliableConverter : ITransportConverter
    {
        public TransportType From => TransportType.AckRawReliable;
        public TransportType To => TransportType.NoAckRawReliable;

        public Func<ITransport> Convert(Func<ITransport> innerTransportCtor, IMemoryRental? memoryOverride = null, ILogger? loggerOverride = null)
        {
            return () =>
            {
                var transport = innerTransportCtor();

                if (transport is IAckRawReliableClient client)
                {
                    return new AckRawReliableToNoAckRawReliableClient(
                        () => (IAckRawReliableClient?)innerTransportCtor(),
                        client.Name,
                        loggerOverride ?? client.Log,
                        memoryOverride ?? client.Memory);
                }

                if (transport is IAckRawReliableServer server)
                {
                    return new AckRawReliableToNoAckRawReliableServer(
                        server,
                        loggerOverride,
                        memoryOverride);
                }

                throw new ArgumentException(
                    $"Transport must implement {nameof(IAckRawReliableClient)} or {nameof(IAckRawReliableServer)}",
                    nameof(transport));
            };
        }
    }
}
