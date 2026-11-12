using System.Collections.Generic;
using Pontifex;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Registry;

namespace Pontifex.TransportFormula.Semantics
{
    public sealed record FormulaResolveResult(
        ResolvedFormula? Formula,
        FactoryError? Error);

    public sealed record ResolvedFormula(
        ResolvedExpr Root);

    // Resolved values form two branches. DataValue is pure data (strings,
    // integers, decimals, structs, and arrays). ResolvedExpr is the special
    // expression-value branch used where a transport expression is a value.
    public abstract record ResolvedValue;

    public abstract record DataValue : ResolvedValue;

    public abstract record ScalarValue : DataValue;

    public sealed record StringValue(
        string Text) : ScalarValue;

    public sealed record IntegerValue(
        long Value) : ScalarValue;

    public sealed record DecimalValue(
        decimal Value) : ScalarValue;

    public sealed record StructValue(
        IReadOnlyList<StructFieldBinding> Fields) : DataValue;

    public sealed record ArrayValue(
        IReadOnlyList<DataValue> Elements) : DataValue;

    public abstract record ResolvedExpr(SourceSpan Span) : ResolvedValue
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

    public sealed record TransportListValue(
        IReadOnlyList<ResolvedExpr> Elements) : ResolvedValue;

    public sealed record StructFieldBinding(
        StructField Field,
        DataValue Value);
}
