using System;
using Transport.Abstractions;

namespace Transport
{
    public class TransportFactoryRegistrator
    {
        private readonly ITransportFactoryCtl mFactory;

        public event Action<ITransportProducer> Successed;
        public event Action<ITransportProducer> Failed;

        public TransportFactoryRegistrator(ITransportFactoryCtl factory)
        {
            mFactory = factory;
        }

        public bool Register<TProducer>()
            where TProducer : ITransportProducer, new()
        {
            TProducer producer = new TProducer();
            return Result(mFactory.Register(producer), producer);
        }

        private bool Result(bool isOk, ITransportProducer producer)
        {
            if (isOk)
            {
                if (Successed != null)
                {
                    Successed(producer);
                }
            }
            else
            {
                if (Failed != null)
                {
                    Failed(producer);
                }
            }
            return isOk;
        }
    }
}
