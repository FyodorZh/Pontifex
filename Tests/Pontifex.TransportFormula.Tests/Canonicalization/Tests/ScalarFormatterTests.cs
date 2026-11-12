using NUnit.Framework;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Canonicalization.Internal;
using Pontifex.TransportFormula.Tests.Canonicalization.Harness;

namespace Pontifex.TransportFormula.Tests.Canonicalization;

[TestFixture]
[Category("TransportFormula.Canonicalization")]
public sealed class ScalarFormatterTests
{
    [Test]
    public void Format_ForEmptyText_ReturnsQuotedEmptyString()
    {
        Assert.That(ScalarFormatter.Format(string.Empty), Is.EqualTo("\"\""));
    }

    [TestCase("plain")]
    [TestCase("127.0.0.1:9000")]
    [TestCase("a|b")]
    [TestCase("a+b")]
    [TestCase("a\\b")]
    [TestCase("café")]
    [TestCase("\u00e9\u4e2d\u6587")]
    [TestCase("\U0001F600")]
    public void Format_ForSafeBareText_ReturnsTextUnchanged(string text)
    {
        Assert.That(ScalarFormatter.Format(text), Is.EqualTo(text));
    }

    [TestCase(",")]
    [TestCase("=")]
    [TestCase("(")]
    [TestCase(")")]
    [TestCase("[")]
    [TestCase("]")]
    [TestCase("{")]
    [TestCase("}")]
    [TestCase("\"")]
    [TestCase(" ")]
    [TestCase("\t")]
    public void Format_ForWhitespaceOrDelimiter_QuotesText(string text)
    {
        var expected = text == "\""
            ? "\"\\\"\""
            : "\"" + text + "\"";

        Assert.That(ScalarFormatter.Format(text), Is.EqualTo(expected));
    }

    [TestCase("a, b")]
    [TestCase("x=y")]
    [TestCase("{name=x}")]
    public void Format_ForTextWithDelimiters_QuotesAndPreservesText(string text)
    {
        Assert.That(ScalarFormatter.Format(text), Is.EqualTo("\"" + text + "\""));
    }

    [Test]
    public void Format_ForTextWithDoubleQuote_EscapesOnlyTheQuote()
    {
        Assert.That(ScalarFormatter.Format("a\"b"), Is.EqualTo("\"a\\\"b\""));
    }

    [Test]
    public void Format_ForTextWithBackslashAndWhitespace_EscapesOnlyTheBackslash()
    {
        Assert.That(ScalarFormatter.Format("a\\b c"), Is.EqualTo("\"a\\\\b c\""));
    }

    [Test]
    public void Format_ForTextWithBothEscapes_EscapesBackslashBeforeQuote()
    {
        Assert.That(ScalarFormatter.Format("a\\\"b"), Is.EqualTo("\"a\\\\\\\"b\""));
    }

    [Test]
    public void Format_ForTextWithLeadingAndTrailingSpaces_PreservesThemInsideQuotes()
    {
        Assert.That(ScalarFormatter.Format("  x  "), Is.EqualTo("\"  x  \""));
    }

    [Test]
    public void Format_ForTextWithTab_QuotesAndPreservesTab()
    {
        Assert.That(ScalarFormatter.Format("a\tb"), Is.EqualTo("\"a\tb\""));
    }

    [Test]
    public void Format_ForCarriageReturn_ThrowsInvalidScalarWithU000D()
    {
        AssertInvalidScalar("a\rb", "U+000D");
    }

    [Test]
    public void Format_ForLineFeed_ThrowsInvalidScalarWithU000A()
    {
        AssertInvalidScalar("a\nb", "U+000A");
    }

    [Test]
    public void Format_ForBothNewlineKinds_ReportsFirstCodeUnitInTextOrder()
    {
        AssertInvalidScalar("\n\r", "U+000A");
        AssertInvalidScalar("\r\n", "U+000D");
    }

    [Test]
    public void Format_ForUnpairedHighSurrogate_ThrowsInvalidScalarWithUD800()
    {
        AssertInvalidScalar("a\uD800b", "U+D800");
    }

    [Test]
    public void Format_ForUnpairedLowSurrogate_ThrowsInvalidScalarWithUDC00()
    {
        AssertInvalidScalar("a\uDC00b", "U+DC00");
    }

    [Test]
    public void Format_ForSurrogatePair_PreservesBarePair()
    {
        var text = "a\uD83D\uDE00b";

        Assert.That(ScalarFormatter.Format(text), Is.EqualTo(text));
    }

    [Test]
    public void Format_ForCrBeforeUnpairedSurrogate_ReportsTheCr()
    {
        AssertInvalidScalar("a\r\uD800", "U+000D");
    }

    private static void AssertInvalidScalar(string text, string codeUnit)
    {
        var exception = Assert.Throws<PrinterException>(() => ScalarFormatter.Format(text));

        Assert.That(exception, Is.Not.Null);
        CanonicalizationAssertions.AssertError(
            exception!.Error,
            "E_PRN_INVALID_SCALAR",
            "scalar value contains a character that cannot be printed: " + codeUnit);
    }
}
