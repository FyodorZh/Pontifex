using System;
using Actuarius.Memory;
using Pontifex.Converters;
using Pontifex.Factory.Compilation.Internal;
using Pontifex.Factory.Registration;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Internal;
using Pontifex.TransportFormula.Semantics;
using Scriba;

namespace Pontifex.Factory.Compilation
{
    /// <summary>
    /// Direct transport compiler over a resolved formula tree. It is deterministic,
    /// holds no static state, and is safe to reuse. See parsing.md §9.3.
    /// </summary>
    public sealed class TransportCompiler : ITransportCompiler
    {
        private readonly ITransportConstructorRegistry _constructors;
        private readonly IConvertersGraph _converters;
        private readonly ILogger _logger;
        private readonly IMemoryRental _memory;

        public TransportCompiler(
            ITransportConstructorRegistry constructors,
            IConvertersGraph converters,
            ILogger logger,
            IMemoryRental memory,
            TransportRole role)
        {
            _constructors = constructors
                ?? throw new ArgumentNullException(nameof(constructors));
            _converters = converters
                ?? throw new ArgumentNullException(nameof(converters));
            _logger = logger
                ?? throw new ArgumentNullException(nameof(logger));
            _memory = memory
                ?? throw new ArgumentNullException(nameof(memory));
            Role = role;
        }

        public TransportRole Role { get; }

        public ITransport Compile(ResolvedFormula formula)
        {
            if (formula is null)
            {
                throw new ArgumentNullException(nameof(formula));
            }

            if (formula.Root is null)
            {
                throw InconsistentTree("unspecified invariant violation");
            }

            return CompileExpr(formula.Root);
        }

        internal ITransport CompileExpr(ResolvedExpr expr)
        {
            switch (expr)
            {
                case ResolvedTransport transport:
                    return CompileTransport(transport);
                case ResolvedCast cast:
                    return CompileCast(cast);
                default:
                    throw InconsistentTree("unspecified invariant violation");
            }
        }

        private ITransport CompileTransport(ResolvedTransport transport)
        {
            var scheme = NameNormalizer.Normalize(transport.Constructor.Name);
            var registration = _constructors.Find(scheme, transport.Constructor.Type);
            if (registration is null)
            {
                throw new BuildException(
                    "E_BLD_CONSTRUCTOR_NOT_FOUND",
                    $"transport constructor '{scheme}' is not registered");
            }

            var context = new TransportBuildContext(
                transport,
                this,
                Role,
                _logger,
                _memory);

            try
            {
                return registration.Create(context);
            }
            catch (Exception exception)
            {
                throw ConstructorFailed(scheme, exception);
            }
        }

        private ITransport CompileCast(ResolvedCast cast)
        {
            var inner = CompileExpr(cast.Inner);
            if (inner.Type == cast.Target)
            {
                return inner;
            }

            var convert = _converters.TryConvert(
                inner.Type,
                cast.Target,
                () => inner,
                _memory,
                _logger);

            if (convert is null)
            {
                throw new BuildException(
                    "E_BLD_NO_CONVERSION_PATH",
                    $"no conversion path from {inner.Type} to {cast.Target}");
            }

            try
            {
                return convert();
            }
            catch (Exception exception)
            {
                throw ConstructorFailed(
                    NameNormalizer.Normalize(cast.Inner.Constructor.Name),
                    exception);
            }
        }

        private static CompilerException InconsistentTree(string detail)
        {
            return new CompilerException(
                "E_CMP_INCONSISTENT_TREE",
                $"compiler received an inconsistent resolved tree: {detail}");
        }

        private static BuildException ConstructorFailed(
            string scheme,
            Exception innerException)
        {
            return new BuildException(
                new FactoryError(
                    "E_BLD_CONSTRUCTOR_FAILED",
                    $"transport constructor '{scheme}' failed",
                    FactoryPhase.Building,
                    null),
                innerException);
        }
    }
}
