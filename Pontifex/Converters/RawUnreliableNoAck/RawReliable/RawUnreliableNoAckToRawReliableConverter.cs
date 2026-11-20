using System;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable.NoAck;
using Scriba;

namespace Pontifex.Converters
{
    public class RawUnreliableNoAckToRawReliableConverter : ITransportConverter
    {
        public TransportType From => TransportType.RawUnreliableNoAck;
        public TransportType To => TransportType.RawReliable;

        public Func<ITransport> Convert(Func<ITransport> innerTransportCtor, IMemoryRental? memoryOverride = null, ILogger? loggerOverride = null)
        {
            return () =>
            {
                var transport = innerTransportCtor();

                if (transport is IRawUnreliableNoAckClient client)
                {
                    return new RawUnreliableNoAckToRawReliableClient(
                        client,
                        memoryOverride,
                        loggerOverride);
                }

                if (transport is IRawUnreliableNoAckServer server)
                {
                    return new RawUnreliableNoAckToRawReliableServer(
                        server,
                        memoryOverride,
                        loggerOverride);
                }

                throw new ArgumentException(
                    $"Transport must implement {nameof(IRawUnreliableNoAckClient)} or {nameof(IRawUnreliableNoAckServer)}",
                    nameof(transport));
            };
        }
    }
}
