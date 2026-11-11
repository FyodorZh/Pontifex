using System.Collections.Generic;
using System.Text;

namespace Pontifex.Factory.Parsing.Internal;

internal static class FormulaLexer
{
    public static bool TryLex(
        string formula,
        out IReadOnlyList<FormulaToken> tokens,
        out FactoryError? error)
    {
        if (formula is null)
        {
            tokens = System.Array.Empty<FormulaToken>();
            error = ParsingErrorFactory.SyntaxEmptyInput();
            return false;
        }

        var result = new List<FormulaToken>();
        var position = 0;

        while (position < formula.Length)
        {
            var c = formula[position];

            if (IsWhitespace(c))
            {
                position++;
                continue;
            }

            if (TryPunctuation(c, out var punctuationKind))
            {
                result.Add(new FormulaToken(
                    punctuationKind,
                    c.ToString(),
                    c.ToString(),
                    new SourceSpan(position, 1)));
                position++;
                continue;
            }

            if (c == '"')
            {
                if (!TryLexQuotedString(formula, ref position, out var quotedToken, out error))
                {
                    tokens = System.Array.Empty<FormulaToken>();
                    return false;
                }

                result.Add(quotedToken);
                continue;
            }

            if (!TryLexWord(formula, ref position, out var wordToken, out error))
            {
                tokens = System.Array.Empty<FormulaToken>();
                return false;
            }

            result.Add(wordToken);
        }

        result.Add(new FormulaToken(
            FormulaTokenKind.EndOfInput,
            string.Empty,
            string.Empty,
            new SourceSpan(formula.Length, 0)));

        tokens = result;
        error = null;
        return true;
    }

    private static bool TryLexWord(
        string formula,
        ref int position,
        out FormulaToken token,
        out FactoryError? error)
    {
        var start = position;

        while (position < formula.Length)
        {
            var c = formula[position];

            if (IsWhitespace(c) || IsDelimiter(c) || c == '"')
            {
                break;
            }

            if (char.IsHighSurrogate(c))
            {
                if (position + 1 < formula.Length && char.IsLowSurrogate(formula[position + 1]))
                {
                    position += 2;
                    continue;
                }

                token = null!;
                error = ParsingErrorFactory.LexInvalidUnicode(position);
                return false;
            }

            if (char.IsLowSurrogate(c))
            {
                token = null!;
                error = ParsingErrorFactory.LexInvalidUnicode(position);
                return false;
            }

            position++;
        }

        var text = formula.Substring(start, position - start);
        token = new FormulaToken(
            FormulaTokenKind.Word,
            text,
            text,
            new SourceSpan(start, position - start));
        error = null;
        return true;
    }

    private static bool TryLexQuotedString(
        string formula,
        ref int position,
        out FormulaToken token,
        out FactoryError? error)
    {
        var start = position;
        position++;

        var decoded = new StringBuilder();

        while (true)
        {
            if (position >= formula.Length)
            {
                token = null!;
                error = ParsingErrorFactory.LexUnterminatedString(start);
                return false;
            }

            var c = formula[position];

            if (char.IsHighSurrogate(c))
            {
                if (position + 1 < formula.Length && char.IsLowSurrogate(formula[position + 1]))
                {
                    decoded.Append(c);
                    decoded.Append(formula[position + 1]);
                    position += 2;
                    continue;
                }

                token = null!;
                error = ParsingErrorFactory.LexInvalidUnicode(position);
                return false;
            }

            if (char.IsLowSurrogate(c))
            {
                token = null!;
                error = ParsingErrorFactory.LexInvalidUnicode(position);
                return false;
            }

            if (c == '\r' || c == '\n')
            {
                token = null!;
                error = ParsingErrorFactory.LexRawNewlineInString(position);
                return false;
            }

            if (c == '"')
            {
                position++;
                break;
            }

            if (c == '\\')
            {
                var backslash = position;
                if (position + 1 >= formula.Length)
                {
                    token = null!;
                    error = ParsingErrorFactory.LexUnterminatedString(start);
                    return false;
                }

                var next = formula[position + 1];
                if (next == '"' || next == '\\')
                {
                    decoded.Append(next);
                    position += 2;
                    continue;
                }

                string sequence;
                if (char.IsHighSurrogate(next)
                    && position + 2 < formula.Length
                    && char.IsLowSurrogate(formula[position + 2]))
                {
                    sequence = "\\" + next + formula[position + 2];
                }
                else
                {
                    sequence = "\\" + next;
                }

                token = null!;
                error = ParsingErrorFactory.LexInvalidEscape(backslash, sequence);
                return false;
            }

            decoded.Append(c);
            position++;
        }

        var raw = formula.Substring(start, position - start);
        token = new FormulaToken(
            FormulaTokenKind.QuotedString,
            decoded.ToString(),
            raw,
            new SourceSpan(start, position - start));
        error = null;
        return true;
    }

    private static bool TryPunctuation(char c, out FormulaTokenKind kind)
    {
        switch (c)
        {
            case '(':
                kind = FormulaTokenKind.LParen;
                return true;
            case ')':
                kind = FormulaTokenKind.RParen;
                return true;
            case '[':
                kind = FormulaTokenKind.LBracket;
                return true;
            case ']':
                kind = FormulaTokenKind.RBracket;
                return true;
            case '{':
                kind = FormulaTokenKind.LBrace;
                return true;
            case '}':
                kind = FormulaTokenKind.RBrace;
                return true;
            case ',':
                kind = FormulaTokenKind.Comma;
                return true;
            case '=':
                kind = FormulaTokenKind.Equals;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static bool IsWhitespace(char c)
    {
        return c == ' ' || c == '\t' || c == '\r' || c == '\n';
    }

    private static bool IsDelimiter(char c)
    {
        return c == '(' || c == ')' || c == '[' || c == ']' || c == '{' || c == '}'
               || c == ',' || c == '=' || c == '"';
    }
}
