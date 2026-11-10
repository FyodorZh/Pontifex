using Pontifex.Factory.Registry;

namespace Pontifex.Factory;

public interface ITransportFactoryBuilder
{
    IRegistryBuilder Registry { get; }

    ITransportFactory Build();
}
