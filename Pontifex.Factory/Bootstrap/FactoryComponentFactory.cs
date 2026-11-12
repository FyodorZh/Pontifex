using System;
using Pontifex.TransportFormula.Canonicalization;
using Pontifex.TransportFormula.Canonicalization.Internal;
using Pontifex.TransportFormula.Parsing;
using Pontifex.TransportFormula.Parsing.Internal;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Semantics;
using Pontifex.TransportFormula.Semantics.Internal;

namespace Pontifex.Factory.Bootstrap
{
    // Internal wiring helper. Component instances are created once from a frozen
    // registry and never exposed publicly. See implementation-plan.md §12.2.
    internal static class FactoryComponentFactory
    {
        public static FactoryComponents Create(IRegistry registry)
        {
            if (registry is null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            return new FactoryComponents(
                new FormulaParser(),
                new FormulaResolver(registry),
                new FormulaCanonicalizer(registry.Types));
        }
    }

    internal sealed class FactoryComponents
    {
        public FactoryComponents(
            IParser parser,
            IResolver resolver,
            ICanonicalizer canonicalizer)
        {
            Parser = parser ?? throw new ArgumentNullException(nameof(parser));
            Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            Canonicalizer = canonicalizer ?? throw new ArgumentNullException(nameof(canonicalizer));
        }

        public IParser Parser { get; }

        public IResolver Resolver { get; }

        public ICanonicalizer Canonicalizer { get; }
    }
}
