using Shared.Utils;
using Transport.Abstractions;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Servers;

namespace Transport.Protocols.Reconnectable.AckReliableRaw
{
    public abstract class BaseAckRawReconnectableProducer
    {
        public string Name
        {
            get { return ReconnectableInfo.TransportName; }
        }

        protected static bool Parse(string @params, out System.TimeSpan disconnectTimeout, out string otherParams)
        {
            try
            {
                int pos = @params.IndexOf(':');
                disconnectTimeout = System.TimeSpan.FromSeconds(int.Parse(@params.Substring(0, pos)));
                otherParams = @params.Substring(pos + 1);
                return disconnectTimeout.TotalMilliseconds > 0;
            }
            catch
            {
                disconnectTimeout = new System.TimeSpan();
                otherParams = "";
                return false;
            }
        }
    }

    public class AckRawReconnectableClientProducer : BaseAckRawReconnectableProducer, ITransportProducer
    {
        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            System.TimeSpan disconnectTimeout;
            string otherParams;
            if (Parse(@params, out disconnectTimeout, out otherParams))
            {
                IAckReliableRawClient innerTransport = factory.Construct(otherParams, logicRunner) as IAckReliableRawClient;
                if (innerTransport != null)
                {
                    // forget innerTransport instance :)
                    return new AckRawReconnectableClient(() => factory.Construct(otherParams, logicRunner) as IAckReliableRawClient, disconnectTimeout, logicRunner);
                }
            }
            return null;
        }
    }

    public class AckRawReconnectableServerProducer : BaseAckRawReconnectableProducer, ITransportProducer
    {
        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            System.TimeSpan disconnectTimeout;
            string otherParams;
            if (Parse(@params, out disconnectTimeout, out otherParams))
            {
                IAckReliableRawServer innerTransport = factory.Construct(otherParams) as IAckReliableRawServer;
                if (innerTransport != null)
                {
                    return new AckRawReconnectableServer(innerTransport, disconnectTimeout);
                }
            }
            return null;
        }
    }
}