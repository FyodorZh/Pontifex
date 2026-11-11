using Pontifex.Factory.Internal;
using Pontifex.Factory.Parsing;

namespace Pontifex.Factory.Parsing.Internal;

internal static class ParsingErrorFactory
{
    public static FactoryError LexUnterminatedString(int openingQuoteOffset)
    {
        return FormulaErrorFactory.Create(
            FactoryPhase.Lexing,
            "E_LEX_UNTERMINATED_STRING",
            "unterminated quoted string",
            openingQuoteOffset);
    }

    public static FactoryError LexInvalidEscape(int backslashOffset, string sequence)
    {
        return FormulaErrorFactory.Create(
            FactoryPhase.Lexing,
            "E_LEX_INVALID_ESCAPE",
            "invalid escape sequence '" + TokenDisplay.Escape(sequence) + "'; only \\\" and \\\\ are allowed",
            backslashOffset);
    }

    public static FactoryError LexRawNewlineInString(int offset)
    {
        return FormulaErrorFactory.Create(
            FactoryPhase.Lexing,
            "E_LEX_RAW_NEWLINE_IN_STRING",
            "raw newline is not allowed inside a quoted string",
            offset);
    }

    public static FactoryError LexInvalidUnicode(int offset)
    {
        return FormulaErrorFactory.Create(
            FactoryPhase.Lexing,
            "E_LEX_INVALID_UNICODE",
            "invalid Unicode scalar value",
            offset);
    }

    public static FactoryError SyntaxEmptyInput()
    {
        return FormulaErrorFactory.Create(
            FactoryPhase.Parsing,
            "E_SYN_EMPTY_INPUT",
            "expected a transport expression",
            0);
    }

    public static FactoryError SyntaxUnexpectedToken(string expected, FormulaToken token)
    {
        return FormulaErrorFactory.Create(
            FactoryPhase.Parsing,
            "E_SYN_UNEXPECTED_TOKEN",
            "expected " + expected + ", got " + DisplayToken(token),
            token.Span.Start);
    }

    public static FactoryError SyntaxTrailingComma(string context, int commaOffset)
    {
        return FormulaErrorFactory.Create(
            FactoryPhase.Parsing,
            "E_SYN_TRAILING_COMMA",
            "trailing comma is not allowed in " + context,
            commaOffset);
    }

    public static FactoryError SyntaxPositionalAfterNamed(int offset)
    {
        return FormulaErrorFactory.Create(
            FactoryPhase.Parsing,
            "E_SYN_POSITIONAL_AFTER_NAMED",
            "positional argument is not allowed after a named argument",
            offset);
    }

    public static FactoryError SyntaxChainedCast(int offset)
    {
        return FormulaErrorFactory.Create(
            FactoryPhase.Parsing,
            "E_SYN_CHAINED_CAST",
            "chained casts are not allowed",
            offset);
    }

    public static FactoryError SyntaxNestedList(int offset)
    {
        return FormulaErrorFactory.Create(
            FactoryPhase.Parsing,
            "E_SYN_NESTED_LIST",
            "nested transport lists are not allowed",
            offset);
    }

    public static FactoryError SyntaxTrailingInput(int offset)
    {
        return FormulaErrorFactory.Create(
            FactoryPhase.Parsing,
            "E_SYN_TRAILING_INPUT",
            "unexpected trailing input",
            offset);
    }

    public static string DisplayToken(FormulaToken token)
    {
        switch (token.Kind)
        {
            case FormulaTokenKind.EndOfInput:
                return TokenDisplay.EndOfInput;
            case FormulaTokenKind.Word:
                return TokenDisplay.Word(token.Text);
            case FormulaTokenKind.QuotedString:
                return TokenDisplay.QuotedString(token.Text);
            case FormulaTokenKind.LParen:
                return TokenDisplay.Punctuation('(');
            case FormulaTokenKind.RParen:
                return TokenDisplay.Punctuation(')');
            case FormulaTokenKind.LBracket:
                return TokenDisplay.Punctuation('[');
            case FormulaTokenKind.RBracket:
                return TokenDisplay.Punctuation(']');
            case FormulaTokenKind.LBrace:
                return TokenDisplay.Punctuation('{');
            case FormulaTokenKind.RBrace:
                return TokenDisplay.Punctuation('}');
            case FormulaTokenKind.Comma:
                return TokenDisplay.Punctuation(',');
            case FormulaTokenKind.Equals:
                return TokenDisplay.Punctuation('=');
            default:
                return TokenDisplay.Word(token.Text);
        }
    }
}
