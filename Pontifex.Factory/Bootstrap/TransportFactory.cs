using System;
using Pontifex;
using Pontifex.Factory.Canonicalization;
using Pontifex.Factory.Compilation;
using Pontifex.Factory.Parsing;
using Pontifex.Factory.Registry;
using Pontifex.Factory.Semantics;

namespace Pontifex.Factory.Bootstrap
{
    // Public-facing facade over the composed pipeline. Resolve never throws for
    // user input; Canonicalize and Compile throw a FactoryException carrying the
    // stage's FactoryError. See implementation-plan.md §12.2-12.3.
    internal sealed class TransportFactory : ITransportFactory
    {
        private static readonly TransportTypeSet AnySet =
            new(TransportTypeSetKind.Any, Array.Empty<TransportType>());

        private readonly IParser _parser;
        private readonly IResolver _resolver;
        private readonly ICanonicalizer _canonicalizer;

        public TransportFactory(
            IParser parser,
            IResolver resolver,
            ICanonicalizer canonicalizer)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _canonicalizer = canonicalizer ?? throw new ArgumentNullException(nameof(canonicalizer));
        }

        public FormulaResolveResult Resolve(
            string formula,
            TransportTypeSet? expected = null)
        {
            var parsed = _parser.Parse(formula);
            if (parsed.Error is not null)
            {
                return new FormulaResolveResult(null, parsed.Error);
            }

            return _resolver.Resolve(parsed.Formula!, expected ?? AnySet);
        }

        public string Canonicalize(string formula)
        {
            var resolved = ResolveOrThrow(formula, null);
            return _canonicalizer.Canonicalize(resolved);
        }

        public TOutput Compile<TOutput>(
            string formula,
            ICompiler<TOutput> compiler,
            TransportTypeSet? expected = null)
        {
            if (compiler is null)
            {
                throw new ArgumentNullException(nameof(compiler));
            }

            var resolved = ResolveOrThrow(formula, expected);

            // ICompiler<T> is opaque and caller-supplied: exceptions it raises are
            // part of the caller's contract and propagate unchanged.
            return compiler.Compile(resolved);
        }

        private ResolvedFormula ResolveOrThrow(
            string formula,
            TransportTypeSet? expected)
        {
            var parsed = _parser.Parse(formula);
            if (parsed.Error is not null)
            {
                throw new FactoryException(parsed.Error);
            }

            var resolved = _resolver.Resolve(parsed.Formula!, expected ?? AnySet);
            if (resolved.Error is not null)
            {
                throw new FactoryException(resolved.Error);
            }

            return resolved.Formula!;
        }
    }
}
