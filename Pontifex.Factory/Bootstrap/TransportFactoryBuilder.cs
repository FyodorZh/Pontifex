using System;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Registry.Internal;

namespace Pontifex.Factory.Bootstrap
{
    // Composition root. The registry builder is exposed through IRegistryBuilder;
    // Build freezes it and wires the pipeline once. Build is idempotent and returns
    // the same factory on every call. See implementation-plan.md §12.2.
    internal sealed class TransportFactoryBuilder : ITransportFactoryBuilder
    {
        private readonly TransportRegistryBuilder _registry = new();
        private ITransportFactory? _built;

        public IRegistryBuilder Registry => _registry;

        public ITransportFactory Build()
        {
            if (_built is not null)
            {
                return _built;
            }

            var registry = _registry.Build();
            var components = FactoryComponentFactory.Create(registry);
            _built = new TransportFactory(
                components.Parser,
                components.Resolver,
                components.Canonicalizer);

            return _built;
        }
    }
}
