using System;
using System.Collections.Generic;
using Actuarius.PeriodicLogic;
using Scriba;
using Transport.Abstractions;

namespace Transport
{
    public interface ITransportFactory
    {
        ITransport? Construct(string address, IPeriodicLogicRunner? logicRunner = null);
    }

    public interface ITransportFactoryCtl : ITransportFactory
    {
        bool Register(ITransportProducer producer);
    }

    public class TransportFactory : ITransportFactory, ITransportFactoryCtl
    {
        private const char Delimiter = '|';

        private readonly Dictionary<string, ITransportProducer> _producers = new ();

        public ITransport? Construct(string address, IPeriodicLogicRunner? logicRunner = null)
        {
            string typeName = TransportType(address);
            if (_producers.TryGetValue(typeName, out var producer))
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
                _producers.Add(producer.Name, producer);
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
            var index = address.IndexOf(Delimiter);
            if (index > 0)
            {
                return address.Substring(0, index);
            }
            return string.Empty;
        }

        private string TransportParams(string address)
        {
            var index = address.IndexOf(Delimiter);
            if (index++ >= 0 && index < address.Length)
            {
                return address.Substring(index);
            }
            return string.Empty;
        }
    }
}
