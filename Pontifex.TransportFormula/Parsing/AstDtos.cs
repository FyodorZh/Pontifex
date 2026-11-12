using System.Collections.Generic;
using Pontifex.TransportFormula;

namespace Pontifex.TransportFormula.Parsing
{
    public sealed record AstFormula(
        AstExpr Root,
        SourceSpan Span);

    public abstract record AstValue(SourceSpan Span);

    public sealed record AstExpr(
        AstTransportCall Call,
        AstCast? Cast,
        SourceSpan Span) : AstValue(Span);

    public sealed record AstTransportCall(
        string Scheme,
        SourceSpan SchemeSpan,
        IReadOnlyList<AstArgument> Arguments,
        int CloseParenOffset,
        SourceSpan Span);

    public sealed record AstArgument(
        string? Name,
        SourceSpan? NameSpan,
        AstValue Value,
        SourceSpan Span);

    public sealed record AstCast(
        string TypeName,
        SourceSpan TypeSpan,
        SourceSpan Span);

    public sealed record AstScalar(
        string Text,
        bool WasQuoted,
        SourceSpan Span) : AstValue(Span);

    public sealed record AstList(
        IReadOnlyList<AstValue> Elements,
        int OpenOffset,
        int CloseOffset,
        SourceSpan Span) : AstValue(Span);

    public sealed record AstStruct(
        IReadOnlyList<AstStructField> Fields,
        int OpenOffset,
        int CloseOffset,
        SourceSpan Span) : AstValue(Span);

    public sealed record AstStructField(
        string Name,
        SourceSpan NameSpan,
        AstValue Value,
        SourceSpan Span);
}
