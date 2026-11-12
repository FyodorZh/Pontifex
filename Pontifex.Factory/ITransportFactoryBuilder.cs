using Pontifex.Factory.Registration;
using Pontifex.TransportFormula.Registry;

namespace Pontifex.Factory
{
    public interface ITransportFactoryBuilder
    {
        IRegistryBuilder Registry { get; }

        /// <summary>
        /// Registers a core transport once. The factory derives both the formula
        /// metadata and the runtime constructor entry from the same registration.
        /// </summary>
        void RegisterTransport(ITransportRegistration registration);

        ITransportFactory Build();
    }
}
