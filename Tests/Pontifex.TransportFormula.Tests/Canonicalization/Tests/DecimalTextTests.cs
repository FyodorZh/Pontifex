using System.Globalization;
using NUnit.Framework;
using Pontifex.TransportFormula.Internal;

namespace Pontifex.TransportFormula.Tests.Canonicalization;

[TestFixture]
[Category("TransportFormula.Canonicalization")]
public sealed class DecimalTextTests
{
    [TestCase("0", "0")]
    [TestCase("-0", "0")]
    [TestCase("1", "1")]
    [TestCase("+1", "1")]
    [TestCase("-1", "-1")]
    [TestCase("1.5", "1.5")]
    [TestCase("1.50", "1.5")]
    [TestCase(".5", "0.5")]
    [TestCase("1.", "1")]
    [TestCase("1e3", "1000")]
    [TestCase("1E+3", "1000")]
    [TestCase("2.5e-1", "0.25")]
    public void TryParse_ForExactLiteral_SucceedsWithValue(string text, string expected)
    {
        Assert.That(DecimalText.TryParse(text, out var value), Is.True, text);
        Assert.That(
            value,
            Is.EqualTo(decimal.Parse(expected, NumberStyles.Float, CultureInfo.InvariantCulture)),
            text);
    }

    [Test]
    public void TryParse_ForDecimalMinValue_Succeeds()
    {
        Assert.That(
            DecimalText.TryParse("-79228162514264337593543950335", out var value),
            Is.True);
        Assert.That(value, Is.EqualTo(decimal.MinValue));
    }

    [Test]
    public void TryParse_ForDecimalMaxValue_Succeeds()
    {
        Assert.That(
            DecimalText.TryParse("79228162514264337593543950335", out var value),
            Is.True);
        Assert.That(value, Is.EqualTo(decimal.MaxValue));
    }

    [Test]
    public void TryParse_ForLargestScaleCoefficient_Succeeds()
    {
        Assert.That(
            DecimalText.TryParse("7922816251426433759354395033.5", out var value),
            Is.True);
        Assert.That(value, Is.EqualTo(7922816251426433759354395033.5m));
    }

    [Test]
    public void TryParse_ForTrailingZeroThatReducesToMaximumCoefficient_Succeeds()
    {
        Assert.That(
            DecimalText.TryParse("792281625142643375935439503350e-1", out var value),
            Is.True);
        Assert.That(value, Is.EqualTo(decimal.MaxValue));
    }

    [Test]
    public void TryParse_ForSmallestScale_Succeeds()
    {
        Assert.That(DecimalText.TryParse("1e-28", out var value), Is.True);
        Assert.That(value, Is.EqualTo(0.0000000000000000000000000001m));
    }

    [Test]
    public void TryParse_ForTrailingZeroAtSmallestScale_Succeeds()
    {
        Assert.That(DecimalText.TryParse("1.0e-28", out var value), Is.True);
        Assert.That(value, Is.EqualTo(0.0000000000000000000000000001m));
    }

    [TestCase("0e-29")]
    [TestCase("0e29")]
    [TestCase("-0e-29")]
    [TestCase("0e1000001")]
    [TestCase("0e-1000001")]
    [TestCase("0e999999999999999999999999")]
    public void TryParse_ForZeroWithAnyExponent_SucceedsAsZero(string text)
    {
        Assert.That(DecimalText.TryParse(text, out var value), Is.True, text);
        Assert.That(value, Is.EqualTo(0m));
    }

    [TestCase("1e-29")]
    [TestCase("-1e-29")]
    [TestCase("5e-29")]
    [TestCase("6e-29")]
    [TestCase("0.00000000000000000000000000001")]
    [TestCase("0.12345678901234567890123456789")]
    [TestCase("1e29")]
    [TestCase("-1e29")]
    [TestCase("79228162514264337593543950336")]
    [TestCase("79228162514264337593543950335.1")]
    public void TryParse_ForInexactOrOutOfRangeLiteral_Fails(string text)
    {
        Assert.That(DecimalText.TryParse(text, out _), Is.False, text);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase(" 1")]
    [TestCase("1 ")]
    [TestCase("+")]
    [TestCase("-")]
    [TestCase(".")]
    [TestCase("e3")]
    [TestCase("1e")]
    [TestCase("1e+")]
    [TestCase("1e-")]
    [TestCase("1,5")]
    [TestCase("1_000")]
    [TestCase("NaN")]
    [TestCase("Infinity")]
    [TestCase("0x10")]
    [TestCase("1.2.3")]
    [TestCase("--1")]
    [TestCase("1e1e1")]
    [TestCase("1e999999999999999999999999")]
    public void TryParse_ForNonNumericOrMalformedText_Fails(string? text)
    {
        Assert.That(DecimalText.TryParse(text, out _), Is.False, text ?? "<null>");
    }

    [TestCase("1.50", "1.5")]
    [TestCase("1.5", "1.5")]
    [TestCase("1.0", "1")]
    [TestCase("1.0000000000000000000000000000", "1")]
    [TestCase("0.000", "0")]
    [TestCase("-0.0", "0")]
    [TestCase("-0", "0")]
    [TestCase("1000", "1000")]
    [TestCase("1000.00", "1000")]
    [TestCase("0.0000000000000000000000000001", "0.0000000000000000000000000001")]
    [TestCase("-0.0000000000000000000000000001", "-0.0000000000000000000000000001")]
    [TestCase("7922816251426433759354395033.5", "7922816251426433759354395033.5")]
    public void Format_ForRepresentableValue_ReturnsCanonicalPlainText(string input, string expected)
    {
        var value = decimal.Parse(input, NumberStyles.Float, CultureInfo.InvariantCulture);

        Assert.That(DecimalText.Format(value), Is.EqualTo(expected));
    }

    [Test]
    public void Format_ForDecimalMinValue_ReturnsPlainText()
    {
        Assert.That(
            DecimalText.Format(decimal.MinValue),
            Is.EqualTo("-79228162514264337593543950335"));
    }

    [Test]
    public void Format_ForDecimalMaxValue_ReturnsPlainText()
    {
        Assert.That(
            DecimalText.Format(decimal.MaxValue),
            Is.EqualTo("79228162514264337593543950335"));
    }

    [TestCase("1.50")]
    [TestCase("1e3")]
    [TestCase("1e-28")]
    [TestCase("-0.0")]
    [TestCase("7922816251426433759354395033.5")]
    public void Format_AfterExactParse_RoundTripsThroughParse(string text)
    {
        Assert.That(DecimalText.TryParse(text, out var first), Is.True, text);
        var formatted = DecimalText.Format(first);

        Assert.That(DecimalText.TryParse(formatted, out var second), Is.True, formatted);
        Assert.That(second, Is.EqualTo(first));
        Assert.That(DecimalText.Format(second), Is.EqualTo(formatted));
    }
}
