using System;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable.NoAck;
using Scriba;

namespace Pontifex.Converters
{
    public class RawUnreliableNoAckToRawReliableAckConverter : ITransportConverter
    {
        public TransportType From => TransportType.RawUnreliableNoAck;
        public TransportType To => TransportType.RawReliableAck;

        public Func<ITransport> Convert(Func<ITransport> innerTransportCtor, IMemoryRental? memoryOverride = null, ILogger? loggerOverride = null)
        {
            return () =>
            {
                var transport = innerTransportCtor();

                if (transport is IRawUnreliableNoAckClient client)
                {
                    return new RawUnreliableNoAckToRawReliableAckClient(
                        client,
                        memoryOverride,
                        loggerOverride);
                }

                if (transport is IRawUnreliableNoAckServer server)
                {
                    return new RawUnreliableNoAckToRawReliableAckServer(
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
