using System;
using Actuarius.Memory;
using Pontifex.Ack.Raw;
using Pontifex.NoAck.Raw;
using Scriba;

namespace Pontifex.Converters
{
    public class NoAckRawUnreliableToAckRawReliableConverter : ITransportConverter
    {
        public TransportType From => TransportType.NoAckRawUnreliable;
        public TransportType To => TransportType.AckRawReliable;

        public Func<ITransport> Convert(Func<ITransport> innerTransportCtor, IMemoryRental? memoryOverride = null, ILogger? loggerOverride = null)
        {
            return () =>
            {
                var transport = innerTransportCtor();

                if (transport is INoAckRawUnreliableClient client)
                {
                    return new NoAckRawUnreliableToAckRawReliableClient(
                        client,
                        memoryOverride,
                        loggerOverride);
                }

                if (transport is INoAckRawUnreliableServer server)
                {
                    return new NoAckRawUnreliableToAckRawReliableServer(
                        server,
                        memoryOverride,
                        loggerOverride);
                }

                throw new ArgumentException(
                    $"Transport must implement {nameof(INoAckRawUnreliableClient)} or {nameof(INoAckRawUnreliableServer)}",
                    nameof(transport));
            };
        }
    }
}
