using System.Collections.Generic;
using Pontifex;
using Pontifex.Factory;
using Pontifex.Factory.Registry;

namespace Pontifex.Factory.Semantics
{
    public sealed record FormulaResolveResult(
        ResolvedFormula? Formula,
        FactoryError? Error);

    public sealed record ResolvedFormula(
        ResolvedExpr Root);

    public abstract record ResolvedExpr(SourceSpan Span)
    {
        public abstract TransportType Type { get; }
    }

    public sealed record ResolvedTransport(
        TransportConstructorInfo Constructor,
        IReadOnlyList<ParamBinding> Provided,
        SourceSpan Span) : ResolvedExpr(Span)
    {
        public override TransportType Type => Constructor.Type;
    }

    public sealed record ResolvedCast(
        ResolvedTransport Inner,
        TransportType Target,
        SourceSpan Span) : ResolvedExpr(Span)
    {
        public override TransportType Type => Target;
    }

    public sealed record ParamBinding(
        Param Param,
        ResolvedValue Value);

    public abstract record ResolvedValue;

    public sealed record ScalarValue(
        string Text) : ResolvedValue;

    public sealed record TransportValue(
        ResolvedExpr Expr) : ResolvedValue;

    public sealed record TransportListValue(
        IReadOnlyList<ResolvedExpr> Elements) : ResolvedValue;

    public sealed record StructValue(
        IReadOnlyList<StructFieldBinding> Fields) : ResolvedValue;

    public sealed record StructFieldBinding(
        StructField Field,
        ResolvedStructFieldValue Value);

    public abstract record ResolvedStructFieldValue;

    public sealed record StringFieldValue(
        string Text) : ResolvedStructFieldValue;

    public sealed record NestedStructFieldValue(
        IReadOnlyList<StructFieldBinding> Fields) : ResolvedStructFieldValue;
}
