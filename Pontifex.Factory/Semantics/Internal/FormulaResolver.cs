using System;
using Pontifex;
using Pontifex.Factory.Parsing;
using Pontifex.Factory.Registry;

namespace Pontifex.Factory.Semantics.Internal
{
    // Implements IResolver: the only place where syntax and metadata meet. Holds the
    // registry and orchestrates candidate selection, argument binding, parameter and
    // struct validation, and cast resolution following parsing.md §7. User input
    // never throws; the first semantic error becomes FormulaResolveResult.Error.
    internal sealed class FormulaResolver : IResolver
    {
        private static readonly TransportTypeSet AnySet =
            new(TransportTypeSetKind.Any, Array.Empty<TransportType>());

        private readonly IRegistry _registry;
        private readonly ConstructorSelector _selector;
        private readonly ArgumentBinder _binder;
        private readonly ParameterValidator _validator;

        public FormulaResolver(IRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _selector = new ConstructorSelector(_registry);
            _binder = new ArgumentBinder();
            _validator = new ParameterValidator(_registry);
        }

        public FormulaResolveResult Resolve(AstFormula formula, TransportTypeSet expected)
        {
            if (formula is null)
            {
                throw new ArgumentNullException(nameof(formula));
            }

            try
            {
                var root = ResolveExpression(formula.Root, expected);
                return new FormulaResolveResult(new ResolvedFormula(root), null);
            }
            catch (SemanticAbort abort)
            {
                return new FormulaResolveResult(null, abort.Error);
            }
        }

        // Internal so ParameterValidator can resolve nested expressions and so the
        // component tests can reuse the exact resolution path.
        internal ResolvedExpr ResolveExpression(
            AstExpr expression,
            TransportTypeSet expected)
        {
            if (expression.Cast is null)
            {
                return ResolveTransport(expression.Call, expected);
            }

            var cast = expression.Cast;
            if (!_registry.Types.TryResolve(cast.TypeName, out var typeInfo) || typeInfo is null)
            {
                throw new SemanticAbort(SemanticErrorFactory.UnknownTransportType(
                    cast.TypeName,
                    cast.TypeSpan.Start,
                    _registry.Types));
            }

            var target = typeInfo.Type;
            if (!expected.Contains(target))
            {
                throw new SemanticAbort(SemanticErrorFactory.CastTypeMismatch(
                    target,
                    expected,
                    cast.TypeSpan.Start,
                    _registry.Types));
            }

            var inner = ResolveTransport(expression.Call, AnySet);

            // Identity casts collapse, retaining the full cast expression span.
            if (inner.Type == target)
            {
                return inner with { Span = expression.Span };
            }

            return new ResolvedCast(inner, target, expression.Span);
        }

        private ResolvedTransport ResolveTransport(
            AstTransportCall call,
            TransportTypeSet expected)
        {
            var constructor = _selector.Select(call.Scheme, call.SchemeSpan, expected);
            var bound = _binder.Bind(call, constructor);
            var provided = _validator.Validate(constructor, bound, ResolveExpression);
            return new ResolvedTransport(constructor, provided, call.Span);
        }
    }
}
