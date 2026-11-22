using System;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Scriba;

namespace Pontifex.Converters
{
    public class RawUnreliableToRawReliableConverter : ITransportConverter
    {
        public TransportType From => TransportType.RawUnreliable;
        public TransportType To => TransportType.RawReliable;

        public Func<ITransport> Convert(Func<ITransport> innerTransportCtor, IMemoryRental? memoryOverride = null, ILogger? loggerOverride = null)
        {
            return () =>
            {
                var transport = innerTransportCtor();

                if (transport is IRawUnreliableClient client)
                {
                    return new RawUnreliableToRawReliableClient(
                        client,
                        memoryOverride,
                        loggerOverride);
                }

                if (transport is IRawUnreliableServer server)
                {
                    return new RawUnreliableToRawReliableServer(
                        server,
                        memoryOverride,
                        loggerOverride);
                }

                throw new ArgumentException(
                    $"Transport must implement {nameof(IRawUnreliableClient)} or {nameof(IRawUnreliableServer)}",
                    nameof(transport));
            };
        }
    }
}
