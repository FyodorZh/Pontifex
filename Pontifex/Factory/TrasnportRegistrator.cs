using System;
using Pontifex.Abstractions;

namespace Pontifex
{
    public class TransportFactoryRegistrator
    {
        private readonly ITransportFactoryCtl _factory;

        public event Action<ITransportProducer>? Succeed;
        public event Action<ITransportProducer>? Failed;

        public TransportFactoryRegistrator(ITransportFactoryCtl factory)
        {
            _factory = factory;
        }

        public bool Register<TProducer>()
            where TProducer : ITransportProducer, new()
        {
            TProducer producer = new TProducer();
            return Result(_factory.Register(producer), producer);
        }

        private bool Result(bool isOk, ITransportProducer producer)
        {
            if (isOk)
            {
                Succeed?.Invoke(producer);
            }
            else
            {
                Failed?.Invoke(producer);
            }
            return isOk;
        }
    }
}
