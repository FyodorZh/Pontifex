using System;
using System.Collections.Generic;
using System.Linq;
using Pontifex.Factory;
using Pontifex.Factory.Parsing;
using Pontifex.Factory.Parsing.Internal;

namespace Pontifex.Factory.Tests.Parsing;

[TestFixture]
[Category("Factory.Parsing")]
public sealed class LexerTests
{
    [Test]
    public void TryLex_WithAllWhitespaceKinds_SkipsThemAndReportsEndOfInput()
    {
        var tokens = Lex(" \t\r\n");

        Assert.That(tokens, Has.Count.EqualTo(1));
        Assert.That(tokens[0].Kind, Is.EqualTo(FormulaTokenKind.EndOfInput));
        Assert.That(tokens[0].Span.Start, Is.EqualTo(4));
        Assert.That(tokens[0].Span.Length, Is.EqualTo(0));
    }

    [Test]
    public void TryLex_WithSurroundingWhitespace_ProducesTokensAtUtf16Offsets()
    {
        var tokens = Lex(" \t\r\nalpha \t\r\nbeta ");

        Assert.That(tokens.Select(t => t.Kind), Is.EqualTo(new[]
        {
            FormulaTokenKind.Word,
            FormulaTokenKind.Word,
            FormulaTokenKind.EndOfInput
        }));
        Assert.That(tokens[0].Text, Is.EqualTo("alpha"));
        Assert.That(tokens[0].Span, Is.EqualTo(new SourceSpan(4, 5)));
        Assert.That(tokens[1].Text, Is.EqualTo("beta"));
        Assert.That(tokens[1].Span, Is.EqualTo(new SourceSpan(13, 4)));
        Assert.That(tokens[2].Span, Is.EqualTo(new SourceSpan(18, 0)));
    }

    [TestCase("alpha")]
    [TestCase("alpha-beta")]
    [TestCase("alpha_beta")]
    [TestCase("127.0.0.1:9000")]
    [TestCase("a\\b")]
    [TestCase("a|b")]
    [TestCase("a+b")]
    [TestCase("as")]
    [TestCase("123")]
    public void TryLex_ForBareScalarExamples_ProducesOneWordToken(string source)
    {
        var tokens = Lex(source);

        Assert.That(tokens, Has.Count.EqualTo(2));
        Assert.That(tokens[0].Kind, Is.EqualTo(FormulaTokenKind.Word));
        Assert.That(tokens[0].Text, Is.EqualTo(source));
        Assert.That(tokens[0].RawText, Is.EqualTo(source));
        Assert.That(tokens[0].Span, Is.EqualTo(new SourceSpan(0, source.Length)));
        Assert.That(tokens[1].Kind, Is.EqualTo(FormulaTokenKind.EndOfInput));
    }

    [Test]
    public void TryLex_ForPunctuation_ProducesExpectedKindsAtConsecutiveOffsets()
    {
        var tokens = Lex("()[]{},=");

        Assert.That(tokens.Select(t => t.Kind), Is.EqualTo(new[]
        {
            FormulaTokenKind.LParen,
            FormulaTokenKind.RParen,
            FormulaTokenKind.LBracket,
            FormulaTokenKind.RBracket,
            FormulaTokenKind.LBrace,
            FormulaTokenKind.RBrace,
            FormulaTokenKind.Comma,
            FormulaTokenKind.Equals,
            FormulaTokenKind.EndOfInput
        }));

        for (var i = 0; i < tokens.Count - 1; i++)
        {
            Assert.That(tokens[i].Span, Is.EqualTo(new SourceSpan(i, 1)));
        }

        Assert.That(tokens[8].Span, Is.EqualTo(new SourceSpan(8, 0)));
    }

    [Test]
    public void TryLex_ForWordTerminatedByQuote_ProducesWordThenQuotedString()
    {
        var tokens = Lex("alpha\"x\"");

        Assert.That(tokens.Select(t => t.Kind), Is.EqualTo(new[]
        {
            FormulaTokenKind.Word,
            FormulaTokenKind.QuotedString,
            FormulaTokenKind.EndOfInput
        }));
        Assert.That(tokens[0].Span, Is.EqualTo(new SourceSpan(0, 5)));
        Assert.That(tokens[1].Text, Is.EqualTo("x"));
        Assert.That(tokens[1].RawText, Is.EqualTo("\"x\""));
        Assert.That(tokens[1].Span, Is.EqualTo(new SourceSpan(5, 3)));
    }

    [Test]
    public void TryLex_ForEmptyQuotedString_ProducesEmptyDecodedText()
    {
        var token = Lex("\"\"").Single(t => t.Kind == FormulaTokenKind.QuotedString);

        Assert.That(token.Text, Is.Empty);
        Assert.That(token.RawText, Is.EqualTo("\"\""));
        Assert.That(token.Span, Is.EqualTo(new SourceSpan(0, 2)));
    }

    [Test]
    public void TryLex_ForQuotedStringContainingDelimiters_PreservesTheirCharacters()
    {
        var token = Lex("\"a, {}=\"").Single(t => t.Kind == FormulaTokenKind.QuotedString);

        Assert.That(token.Text, Is.EqualTo("a, {}="));
        Assert.That(token.Span, Is.EqualTo(new SourceSpan(0, 8)));
    }

    [Test]
    public void TryLex_ForQuotedStringContainingWhitespace_PreservesWhitespaceAndTab()
    {
        var token = Lex("\" a\tb \"").Single(t => t.Kind == FormulaTokenKind.QuotedString);

        Assert.That(token.Text, Is.EqualTo(" a\tb "));
    }

    [Test]
    public void TryLex_ForQuotedStringWithEscapes_DecodesQuoteAndBackslash()
    {
        var token = Lex("\"a\\\"b\\\\c\"").Single(t => t.Kind == FormulaTokenKind.QuotedString);

        Assert.That(token.Text, Is.EqualTo("a\"b\\c"));
        Assert.That(token.RawText, Is.EqualTo("\"a\\\"b\\\\c\""));
    }

    [Test]
    public void TryLex_ForQuotedStringWithSurrogatePair_PreservesBothCodeUnits()
    {
        const string emoji = "\uD83D\uDE00";
        var token = Lex("\"" + emoji + "\"").Single(t => t.Kind == FormulaTokenKind.QuotedString);

        Assert.That(token.Text, Is.EqualTo(emoji));
        Assert.That(token.Text.Length, Is.EqualTo(2));
        Assert.That(token.Span, Is.EqualTo(new SourceSpan(0, 4)));
    }

    [Test]
    public void TryLex_ForWordWithSurrogatePair_CountsBothCodeUnitsInSpan()
    {
        const string emoji = "\uD83D\uDE00";
        var tokens = Lex("alpha(" + emoji + ")");

        Assert.That(tokens[2].Kind, Is.EqualTo(FormulaTokenKind.Word));
        Assert.That(tokens[2].Text, Is.EqualTo(emoji));
        Assert.That(tokens[2].Span, Is.EqualTo(new SourceSpan(6, 2)));
    }

    [Test]
    public void TryLex_ForInputEndingInWhitespace_ReportsEndOfInputAtInputLength()
    {
        var tokens = Lex("alpha  ");
        var eof = tokens[tokens.Count - 1];

        Assert.That(eof.Kind, Is.EqualTo(FormulaTokenKind.EndOfInput));
        Assert.That(eof.Text, Is.Empty);
        Assert.That(eof.RawText, Is.Empty);
        Assert.That(eof.Span, Is.EqualTo(new SourceSpan(7, 0)));
    }

    [Test]
    public void TryLex_WithRawLineFeedInsideQuotedString_ReturnsRawNewlineError()
    {
        var error = LexError("alpha(\"x\ny\")");

        Assert.That(error.Code, Is.EqualTo("E_LEX_RAW_NEWLINE_IN_STRING"));
        Assert.That(error.Phase, Is.EqualTo(FactoryPhase.Lexing));
        Assert.That(error.Message, Is.EqualTo("raw newline is not allowed inside a quoted string"));
        Assert.That(error.Span!.Value.Start, Is.EqualTo(8));
    }

    [Test]
    public void TryLex_WithRawCarriageReturnInsideQuotedString_ReturnsRawNewlineError()
    {
        var error = LexError("alpha(\"x\ry\")");

        Assert.That(error.Code, Is.EqualTo("E_LEX_RAW_NEWLINE_IN_STRING"));
        Assert.That(error.Span!.Value.Start, Is.EqualTo(8));
    }

    [Test]
    public void TryLex_WithInvalidEscape_ReturnsInvalidEscapeAtBackslash()
    {
        var error = LexError("alpha(\"x\\q\")");

        Assert.That(error.Code, Is.EqualTo("E_LEX_INVALID_ESCAPE"));
        Assert.That(error.Phase, Is.EqualTo(FactoryPhase.Lexing));
        Assert.That(
            error.Message,
            Is.EqualTo("invalid escape sequence '\\\\q'; only \\\" and \\\\ are allowed"));
        Assert.That(error.Span!.Value.Start, Is.EqualTo(8));
    }

    [Test]
    public void TryLex_WithBackslashBeforeLineFeed_ReturnsInvalidEscapeNotRawNewline()
    {
        var error = LexError("alpha(\"x\\\ny\")");

        Assert.That(error.Code, Is.EqualTo("E_LEX_INVALID_ESCAPE"));
        Assert.That(error.Span!.Value.Start, Is.EqualTo(8));
        Assert.That(error.Message, Does.StartWith("invalid escape sequence '"));
    }

    [Test]
    public void TryLex_WithBackslashBeforeUnpairedSurrogate_ReturnsInvalidEscapeNotInvalidUnicode()
    {
        var error = LexError("alpha(\"x\\" + '\uD800' + "\")");

        Assert.That(error.Code, Is.EqualTo("E_LEX_INVALID_ESCAPE"));
        Assert.That(error.Span!.Value.Start, Is.EqualTo(8));
    }

    [Test]
    public void TryLex_WithBackslashAtEndOfInput_ReturnsUnterminatedAtOpeningQuote()
    {
        var error = LexError("alpha(\"x\\");

        Assert.That(error.Code, Is.EqualTo("E_LEX_UNTERMINATED_STRING"));
        Assert.That(error.Message, Is.EqualTo("unterminated quoted string"));
        Assert.That(error.Span!.Value.Start, Is.EqualTo(6));
    }

    [TestCase("alpha(\"x", 6)]
    [TestCase("alpha(1) \"unterminated", 9)]
    public void TryLex_WithUnterminatedQuotedString_ReturnsErrorAtOpeningQuote(string source, int offset)
    {
        var error = LexError(source);

        Assert.That(error.Code, Is.EqualTo("E_LEX_UNTERMINATED_STRING"));
        Assert.That(error.Span!.Value.Start, Is.EqualTo(offset));
    }

    [Test]
    public void TryLex_WithUnpairedHighSurrogateInQuotedString_ReturnsInvalidUnicode()
    {
        var error = LexError("alpha(\"x" + '\uD800' + "y\")");

        Assert.That(error.Code, Is.EqualTo("E_LEX_INVALID_UNICODE"));
        Assert.That(error.Message, Is.EqualTo("invalid Unicode scalar value"));
        Assert.That(error.Span!.Value.Start, Is.EqualTo(8));
    }

    [Test]
    public void TryLex_WithUnpairedLowSurrogateInQuotedString_ReturnsInvalidUnicode()
    {
        var error = LexError("alpha(\"x" + '\uDC00' + "y\")");

        Assert.That(error.Code, Is.EqualTo("E_LEX_INVALID_UNICODE"));
        Assert.That(error.Span!.Value.Start, Is.EqualTo(8));
    }

    [Test]
    public void TryLex_WithUnpairedHighSurrogateInWord_ReturnsInvalidUnicode()
    {
        var error = LexError("alpha(" + '\uD800' + ")");

        Assert.That(error.Code, Is.EqualTo("E_LEX_INVALID_UNICODE"));
        Assert.That(error.Span!.Value.Start, Is.EqualTo(6));
    }

    [Test]
    public void TryLex_WithUnpairedLowSurrogateInWord_ReturnsInvalidUnicode()
    {
        var error = LexError("alpha(" + '\uDC00' + ")");

        Assert.That(error.Code, Is.EqualTo("E_LEX_INVALID_UNICODE"));
        Assert.That(error.Span!.Value.Start, Is.EqualTo(6));
    }

    private static IReadOnlyList<FormulaToken> Lex(string source)
    {
        var succeeded = FormulaLexer.TryLex(source, out var tokens, out var error);

        Assert.That(succeeded, Is.True, error?.Message);
        Assert.That(error, Is.Null);
        return tokens;
    }

    private static FactoryError LexError(string source)
    {
        var succeeded = FormulaLexer.TryLex(source, out _, out var error);

        Assert.That(succeeded, Is.False);
        Assert.That(error, Is.Not.Null);
        return error!;
    }
}
