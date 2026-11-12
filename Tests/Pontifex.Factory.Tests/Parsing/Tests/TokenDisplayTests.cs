using Pontifex.TransportFormula.Internal;

namespace Pontifex.Factory.Tests.Parsing;

[TestFixture]
[Category("Factory.Parsing")]
public sealed class TokenDisplayTests
{
    [Test]
    public void Word_WithBackslash_EscapesBackslash()
    {
        Assert.That(TokenDisplay.Word("a\\b"), Is.EqualTo("'a\\\\b'"));
    }

    [Test]
    public void QuotedString_WithTab_RendersTabEscape()
    {
        Assert.That(TokenDisplay.QuotedString("a\tb"), Is.EqualTo("'a\\tb'"));
    }

    [Test]
    public void QuotedString_WithSurrogatePair_RendersTwoUnicodeEscapes()
    {
        Assert.That(TokenDisplay.QuotedString("\uD83D\uDE00"), Is.EqualTo("'\\uD83D\\uDE00'"));
    }

    [Test]
    public void Word_WithQuoteAndApostrophe_EscapesBoth()
    {
        Assert.That(TokenDisplay.Word("a'b\"c"), Is.EqualTo("'a\\'b\\\"c'"));
    }

    [Test]
    public void Word_WithControlCode_RendersUnicodeEscape()
    {
        Assert.That(TokenDisplay.Word("\u0001"), Is.EqualTo("'\\u0001'"));
    }

    [Test]
    public void Word_WithCarriageReturnAndLineFeed_RendersNamedEscapes()
    {
        Assert.That(TokenDisplay.Word("\r\n"), Is.EqualTo("'\\r\\n'"));
    }

    [Test]
    public void Word_WithEscapedTextExactlyFortyCharacters_DoesNotTruncate()
    {
        var text = new string('a', 40);

        Assert.That(TokenDisplay.Word(text), Is.EqualTo("'" + text + "'"));
    }

    [Test]
    public void QuotedString_WithFiftyCharacters_TruncatesToFortyAndEllipsis()
    {
        var text = new string('a', 50);

        Assert.That(
            TokenDisplay.QuotedString(text),
            Is.EqualTo("'" + new string('a', 40) + "...'"));
    }

    [Test]
    public void Word_WithFiftyCharacters_TruncatesToFortyAndEllipsis()
    {
        var text = new string('a', 50);
        var displayed = TokenDisplay.Word(text);

        Assert.That(displayed, Is.EqualTo("'" + new string('a', 40) + "...'"));
    }

    [Test]
    public void Word_WithEscapeThatDoesNotFitAfterThirtyNineCharacters_DropsWholeEscape()
    {
        var text = new string('a', 39) + "\\";
        var displayed = TokenDisplay.Word(text);

        Assert.That(displayed, Is.EqualTo("'" + new string('a', 39) + "...'"));
    }

    [Test]
    public void Word_WithEscapeEndingExactlyAtFortyCharacters_KeepsEscape()
    {
        var text = new string('a', 38) + "\\b";
        var displayed = TokenDisplay.Word(text);

        Assert.That(displayed, Is.EqualTo("'" + new string('a', 38) + "\\\\" + "...'"));
    }

    [Test]
    public void Word_WithSurrogatePairAtTruncationBoundary_DropsWholePair()
    {
        var text = new string('a', 34) + "\uD83D\uDE00";
        var displayed = TokenDisplay.Word(text);

        Assert.That(displayed, Is.EqualTo("'" + new string('a', 34) + "...'"));
    }

    [Test]
    public void Punctuation_FormatsCharacterInSingleQuotes()
    {
        Assert.That(TokenDisplay.Punctuation('('), Is.EqualTo("'('"));
    }

    [Test]
    public void Escape_WithUnpairedHighSurrogate_ReturnsUnicodeEscape()
    {
        Assert.That(TokenDisplay.Escape("\uD800"), Is.EqualTo("\\uD800"));
    }

    [Test]
    public void EndOfInput_IsTheDocumentedMarker()
    {
        Assert.That(TokenDisplay.EndOfInput, Is.EqualTo("<end of input>"));
    }
}
