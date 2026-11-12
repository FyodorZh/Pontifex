using System;
using Pontifex.Converters;
using Pontifex.Factory.Registration;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Registry.Internal;

namespace Pontifex.Factory.Bootstrap
{
    // Composition root. The formula registry builder is exposed through
    // IRegistryBuilder; RegisterTransport is the single registration path for
    // transports that will be compiled. Build freezes the formula registry, seals
    // the runtime constructor registry, and wires the pipeline once. Build is
    // idempotent and returns the same factory on every call. See parsing.md §9.
    internal sealed class TransportFactoryBuilder : ITransportFactoryBuilder
    {
        private readonly TransportRegistryBuilder _registry = new();
        private readonly TransportConstructorRegistry _constructors = new();
        private readonly IConvertersGraph _converters;
        private ITransportFactory? _built;

        public TransportFactoryBuilder(IConvertersGraph converters)
        {
            _converters = converters ?? throw new ArgumentNullException(nameof(converters));
        }

        public IRegistryBuilder Registry => _registry;

        public void RegisterTransport(ITransportRegistration registration)
        {
            if (registration is null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            // The formula registry validates and normalizes first, so a rejected
            // registration never reaches the runtime registry. The core object is
            // stored unchanged under the same canonical key the compiler uses.
            var formulaRegistration = CoreRegistrationAdapter.Adapt(registration);
            _registry.RegisterTransport(formulaRegistration);
            _constructors.Register(registration);
        }

        public ITransportFactory Build()
        {
            if (_built is not null)
            {
                return _built;
            }

            var registry = _registry.Build();
            var components = FactoryComponentFactory.Create(
                registry,
                _constructors,
                _converters);
            _constructors.Seal();

            // Assign last: a failure above leaves the builder unbuilt instead of
            // publishing a partially wired factory.
            _built = new TransportFactory(
                components.Parser,
                components.Resolver,
                components.Canonicalizer,
                components.Constructors,
                components.Converters);

            return _built;
        }
    }
}
