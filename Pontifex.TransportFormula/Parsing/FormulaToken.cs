using Pontifex.TransportFormula;

namespace Pontifex.TransportFormula.Parsing
{
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
}
