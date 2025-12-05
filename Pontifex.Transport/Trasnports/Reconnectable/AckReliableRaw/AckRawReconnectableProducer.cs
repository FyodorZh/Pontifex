using System;
using Actuarius.PeriodicLogic;
using Transport.Abstractions;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Servers;

namespace Transport.Protocols.Reconnectable.AckReliableRaw
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
        public ITransport? Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner? logicRunner)
        {
            if (Parse(@params, out var disconnectTimeout, out var otherParams))
            {
                if (factory.Construct(otherParams, logicRunner) is IAckReliableRawClient)
                {
                    return new AckRawReconnectableClient(
                        () => (IAckReliableRawClient)factory.Construct(otherParams, logicRunner)!, 
                        disconnectTimeout, 
                        logicRunner);
                }
            }
            return null;
        }
    }

    public class AckRawReconnectableServerProducer : BaseAckRawReconnectableProducer, ITransportProducer
    {
        public ITransport? Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner? logicRunner)
        {
            if (Parse(@params, out var disconnectTimeout, out var otherParams))
            {
                if (factory.Construct(otherParams) is IAckReliableRawServer innerTransport)
                {
                    return new AckRawReconnectableServer(innerTransport, disconnectTimeout);
                }
            }
            return null;
        }
    }
}