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

        public ITransport Convert(ITransport transport, IMemoryRental? memoryOverride = null, ILogger? loggerOverride = null)
        {
            if (transport is IAckRawReliableClient client)
            {
                var log = loggerOverride ?? client.Log;
                var memory = memoryOverride ?? client.Memory;
                return new AckRawReliableToNoAckRawReliableClient(
                    () => client,
                    client.Name,
                    log,
                    memory);
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
        }
    }
}
