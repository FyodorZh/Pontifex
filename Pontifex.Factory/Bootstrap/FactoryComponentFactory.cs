using System;
using Pontifex.Converters;
using Pontifex.TransportFormula.Canonicalization;
using Pontifex.TransportFormula.Canonicalization.Internal;
using Pontifex.TransportFormula.Parsing;
using Pontifex.TransportFormula.Parsing.Internal;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Semantics;
using Pontifex.TransportFormula.Semantics.Internal;

namespace Pontifex.Factory.Bootstrap
{
    // Internal wiring helper. Formula and runtime component instances are created
    // once from a frozen registry and never exposed publicly. See
    // implementation-plan.md §12.2.
    internal static class FactoryComponentFactory
    {
        public static FactoryComponents Create(
            IRegistry registry,
            ITransportConstructorRegistry constructors,
            IConvertersGraph converters)
        {
            if (registry is null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (constructors is null)
            {
                throw new ArgumentNullException(nameof(constructors));
            }

            if (converters is null)
            {
                throw new ArgumentNullException(nameof(converters));
            }

            return new FactoryComponents(
                new FormulaParser(),
                new FormulaResolver(registry),
                new FormulaCanonicalizer(registry.Types),
                constructors,
                converters);
        }
    }

    internal sealed class FactoryComponents
    {
        public FactoryComponents(
            IParser parser,
            IResolver resolver,
            ICanonicalizer canonicalizer,
            ITransportConstructorRegistry constructors,
            IConvertersGraph converters)
        {
            Parser = parser ?? throw new ArgumentNullException(nameof(parser));
            Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            Canonicalizer = canonicalizer ?? throw new ArgumentNullException(nameof(canonicalizer));
            Constructors = constructors ?? throw new ArgumentNullException(nameof(constructors));
            Converters = converters ?? throw new ArgumentNullException(nameof(converters));
        }

        public IParser Parser { get; }

        public IResolver Resolver { get; }

        public ICanonicalizer Canonicalizer { get; }

        public ITransportConstructorRegistry Constructors { get; }

        public IConvertersGraph Converters { get; }
    }
}
