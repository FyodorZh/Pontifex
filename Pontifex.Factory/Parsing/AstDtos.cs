using System.Collections.Generic;
using Pontifex.Factory;

namespace Pontifex.Factory.Parsing;

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
    IReadOnlyList<AstExpr> Elements,
    SourceSpan Span) : AstValue(Span);

public sealed record AstStruct(
    IReadOnlyList<AstStructField> Fields,
    SourceSpan Span) : AstValue(Span);

public sealed record AstStructField(
    string Name,
    SourceSpan NameSpan,
    AstValue Value,
    SourceSpan Span);
