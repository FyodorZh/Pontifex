using System;
using System.Collections.Generic;
using Shared.Utils;
using Transport.Abstractions;

namespace Transport
{
    public interface ITransportFactory
    {
        ITransport Construct(string address, IPeriodicLogicRunner logicRunner = null);
    }

    public interface ITransportFactoryCtl : ITransportFactory
    {
        bool Register(ITransportProducer producer);
    }

    public class TransportFactory : ITransportFactory, ITransportFactoryCtl
    {
        private const char Delimiter = '|';

        private readonly Dictionary<string, ITransportProducer> mProducers = new Dictionary<string, ITransportProducer>();

        public ITransport Construct(string address, IPeriodicLogicRunner logicRunner = null)
        {
            string typeName = TransportType(address);
            ITransportProducer producer;
            if (mProducers.TryGetValue(typeName, out producer))
            {
                string @params = TransportParams(address);
                return producer.Produce(@params, this, logicRunner);
            }

            return null;
        }

        public bool Register(ITransportProducer producer)
        {
            try
            {
                mProducers.Add(producer.Name, producer);
            }
            catch (Exception e)
            {
                Log.wtf(e);
                return false;
            }

            return true;
        }

        private string TransportType(string address)
        {
            if (null != address)
            {
                var index = address.IndexOf(Delimiter);
                if (index > 0)
                {
                    return address.Substring(0, index);
                }
            }
            return string.Empty;
        }

        private string TransportParams(string address)
        {
            if (null != address)
            {
                var index = address.IndexOf(Delimiter);
                if (index++ >= 0 && index < address.Length)
                {
                    return address.Substring(index);
                }
            }
            return string.Empty;
        }
    }
}
