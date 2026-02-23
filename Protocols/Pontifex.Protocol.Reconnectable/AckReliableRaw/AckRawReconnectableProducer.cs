using System;
using Actuarius.Memory;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Clients;
using Pontifex.Abstractions.Servers;
using Scriba;

namespace Pontifex.Protocols.Reconnectable.AckReliableRaw
{
    public abstract class BaseAckRawReconnectableProducer
    {
        public string Name => ReconnectableInfo.TransportName;

        protected static bool Parse(string @params, out TimeSpan disconnectTimeout, out string otherParams)
        {
            try
            {
                int pos = @params.IndexOf(':');
                disconnectTimeout = TimeSpan.FromSeconds(int.Parse(@params.Substring(0, pos)));
                otherParams = @params.Substring(pos + 1);
                return disconnectTimeout.TotalMilliseconds > 0;
            }
            catch
            {
                disconnectTimeout = TimeSpan.Zero;
                otherParams = "";
                return false;
            }
        }
    }

    public class AckRawReconnectableClientProducer : BaseAckRawReconnectableProducer, ITransportProducer
    {
        public ITransport? Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental)
        {
            if (Parse(@params, out var disconnectTimeout, out var otherParams))
            {
                if (factory.Construct(otherParams, logger, memoryRental) is IAckReliableRawClient)
                {
                    return new AckRawReconnectableClient(
                        () => factory.Construct(otherParams, logger, memoryRental) as IAckReliableRawClient, 
                        disconnectTimeout, 
                        logger,
                        memoryRental);
                }
            }
            return null;
        }
    }

    public class AckRawReconnectableServerProducer : BaseAckRawReconnectableProducer, ITransportProducer
    {
        public ITransport? Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental)
        {
            if (Parse(@params, out var disconnectTimeout, out var otherParams))
            {
                if (factory.Construct(otherParams, logger, memoryRental) is IAckReliableRawServer innerTransport)
                {
                    return new AckRawReconnectableServer(innerTransport, disconnectTimeout, logger, memoryRental);
                }
            }
            return null;
        }
    }
}