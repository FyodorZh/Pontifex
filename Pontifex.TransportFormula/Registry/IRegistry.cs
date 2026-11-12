using System.Collections.Generic;

namespace Pontifex.TransportFormula.Registry
{
    public interface IRegistry
    {
        ITransportTypeCatalog Types { get; }

        IReadOnlyList<TransportConstructorInfo> Constructors { get; }

        IReadOnlyList<string> AllSchemeNames { get; }

        TransportConstructorInfo? FindCanonical(string scheme);

        IReadOnlyList<TransportConstructorInfo> FindCandidates(string scheme);
    }

    public interface IRegistryBuilder
    {
        void RegisterType(TransportTypeInfo type);

        void RegisterTransport(TransportRegistration registration);

        IRegistry Build();
    }
}
