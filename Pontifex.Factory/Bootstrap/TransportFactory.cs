using System;
using Actuarius.Memory;
using Pontifex.Converters;
using Pontifex.Factory.Compilation;
using Pontifex.Factory.Registration;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Canonicalization;
using Pontifex.TransportFormula.Compilation;
using Pontifex.TransportFormula.Parsing;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Semantics;
using Scriba;
using TransportTypeSet = Pontifex.TransportFormula.Registry.TransportTypeSet;
using TransportTypeSetKind = Pontifex.TransportFormula.Registry.TransportTypeSetKind;

namespace Pontifex.Factory.Bootstrap
{
    // Public-facing facade over the composed pipeline. Resolve never throws for
    // user input; Canonicalize and Compile throw a FactoryException carrying the
    // stage's FactoryError. CreateCompiler binds the frozen runtime registry and
    // converter graph. See implementation-plan.md §12.2-12.3.
    internal sealed class TransportFactory : ITransportFactory
    {
        private static readonly TransportTypeSet AnySet =
            new(TransportTypeSetKind.Any, Array.Empty<TransportType>());

        private readonly IParser _parser;
        private readonly IResolver _resolver;
        private readonly ICanonicalizer _canonicalizer;
        private readonly ITransportConstructorRegistry _constructors;
        private readonly IConvertersGraph _converters;

        // The parser keeps a cursor as instance state, so the read-only pipeline is
        // serialized. Caller-supplied compilers run outside this gate.
        private readonly object _sync = new object();

        public TransportFactory(
            IParser parser,
            IResolver resolver,
            ICanonicalizer canonicalizer,
            ITransportConstructorRegistry constructors,
            IConvertersGraph converters)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _canonicalizer = canonicalizer ?? throw new ArgumentNullException(nameof(canonicalizer));
            _constructors = constructors ?? throw new ArgumentNullException(nameof(constructors));
            _converters = converters ?? throw new ArgumentNullException(nameof(converters));
        }

        public FormulaResolveResult Resolve(
            string formula,
            TransportTypeSet? expected = null)
        {
            lock (_sync)
            {
                return ResolveCore(formula, expected);
            }
        }

        public string Canonicalize(string formula)
        {
            lock (_sync)
            {
                var resolved = ResolveOrThrowCore(formula, null);
                return _canonicalizer.Canonicalize(resolved);
            }
        }

        public ITransportCompiler CreateCompiler(
            TransportRole role,
            ILogger logger,
            IMemoryRental memory)
        {
            // The facade guards the build environment before handing it to the
            // compiler. TransportCompiler applies the same guards, so direct
            // construction retains its contract.
            if (logger is null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (memory is null)
            {
                throw new ArgumentNullException(nameof(memory));
            }

            return new TransportCompiler(
                _constructors,
                _converters,
                logger,
                memory,
                role);
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

        private FormulaResolveResult ResolveCore(
            string formula,
            TransportTypeSet? expected)
        {
            var parsed = _parser.Parse(formula);
            if (parsed.Error is not null)
            {
                return new FormulaResolveResult(null, parsed.Error);
            }

            return _resolver.Resolve(parsed.Formula!, expected ?? AnySet);
        }

        private ResolvedFormula ResolveOrThrow(
            string formula,
            TransportTypeSet? expected)
        {
            lock (_sync)
            {
                return ResolveOrThrowCore(formula, expected);
            }
        }

        private ResolvedFormula ResolveOrThrowCore(
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
