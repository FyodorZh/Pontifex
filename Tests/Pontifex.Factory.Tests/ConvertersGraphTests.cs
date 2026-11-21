using System;
using NUnit.Framework;
using Pontifex.Converters;

namespace Pontifex.Factory.Tests;

// Loose fixture for the default converter graph. It verifies the graph is sized for the whole
// transport-type enum and targets the single reliable raw contract.
[TestFixture]
public sealed class ConvertersGraphTests
{
    [Test]
    public void Default_HasSingleConversionFromRawUnreliableToRawReliable()
    {
        Assert.That(
            ConvertersGraph.Default.HasPath(
                TransportType.RawUnreliable,
                TransportType.RawReliable,
                out int length),
            Is.True);
        Assert.That(length, Is.EqualTo(1));
    }

    [Test]
    public void Default_TryConvert_FromRawUnreliableToRawReliable_ReturnsAConverter()
    {
        Func<ITransport> conversion = ConvertersGraph.Default.TryConvert(
            TransportType.RawUnreliable,
            TransportType.RawReliable,
            () => throw new InvalidOperationException("The inner transport must not be built by TryConvert."))!;

        Assert.That(conversion, Is.Not.Null);
    }

    [Test]
    public void Default_CoversEveryEnumValueIncludingTheHighest()
    {
        Array values = Enum.GetValues(typeof(TransportType));
        TransportType highest = (TransportType)values.GetValue(values.Length - 1)!;

        Assert.That(highest, Is.EqualTo(TransportType.RRUnreliableNoAck));

        foreach (TransportType type in values)
        {
            Assert.That(
                ConvertersGraph.Default.HasPath(type, type, out int length),
                Is.True,
                $"the converter graph must be sized for {type}");
            Assert.That(length, Is.Zero);
        }
    }

    [Test]
    public void Default_HasNoPathFromTheHighestValueToRawReliable()
    {
        Assert.That(
            ConvertersGraph.Default.HasPath(
                TransportType.RRUnreliableNoAck,
                TransportType.RawReliable,
                out int length),
            Is.False);
        Assert.That(length, Is.Zero);
    }
}
