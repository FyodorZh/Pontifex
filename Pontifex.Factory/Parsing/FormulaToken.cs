using Pontifex.Factory;

namespace Pontifex.Factory.Parsing;

public enum FormulaTokenKind
{
    Word,
    QuotedString,
    LParen,
    RParen,
    LBracket,
    RBracket,
    LBrace,
    RBrace,
    Comma,
    Equals,
    EndOfInput
}

public sealed record FormulaToken(
    FormulaTokenKind Kind,
    string Text,
    string RawText,
    SourceSpan Span);
