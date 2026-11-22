using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Factory.Registration;
using Pontifex.Raw.Unreliable.Direct;
using Pontifex.VirtualDelivery;
using Pontifex.VirtualDelivery.Netem;

namespace Pontifex.Tests.Raw;

/// <summary>
/// Pins the delivery-config parser defaults and the explicit rejection of unknown values.
/// </summary>
[TestFixture]
public sealed class UnreliableDeliveryBindingTests
{
    [Test]
    public void Parse_WithoutOptionalFields_UsesDocumentedDefaults()
    {
        var config = UnreliableDeliveryBinding.ParseNetemConfig(FakeStruct.Empty());

        Assert.Multiple(() =>
        {
            Assert.That(config.LatencyNs, Is.Zero);
            Assert.That(config.JitterNs, Is.Zero);
            Assert.That(config.QueueLimit, Is.EqualTo(int.MaxValue));
            Assert.That(config.LossModel, Is.EqualTo(LossModelKind.Random));
            Assert.That(config.Correlation.DelayRho, Is.Zero);
            Assert.That(config.Slot.IsEnabled, Is.False);
        });
    }

    [Test]
    public void Parse_WithPartialCorrelation_DefaultsMissingFields()
    {
        var value = FakeStruct.Empty()
            .SetStruct("correlation", FakeStruct.Empty().SetLong("delayrho", 7));

        var config = UnreliableDeliveryBinding.ParseNetemConfig(value);

        Assert.Multiple(() =>
        {
            Assert.That(config.Correlation.DelayRho, Is.EqualTo(7));
            Assert.That(config.Correlation.LossRho, Is.Zero);
            Assert.That(config.Correlation.DuplicateRho, Is.Zero);
            Assert.That(config.Correlation.ReorderRho, Is.Zero);
        });
    }

    [Test]
    public void Parse_WithUnknownLossModel_Throws()
    {
        var value = FakeStruct.Empty().SetString("lossmodel", "unknown");

        Assert.Throws<ArgumentException>(
            () => UnreliableDeliveryBinding.ParseNetemConfig(value));
    }

    [Test]
    public void Build_WithPerfectType_ReturnsPerfectDeliverySystem()
    {
        var system = UnreliableDeliveryBinding.Build(
            FakeStruct.Empty().SetString("type", "perfect"), MemoryRental.Shared);

        Assert.That(system, Is.TypeOf<PerfectDeliverySystem>());
    }

    [Test]
    public void Build_WithNetemType_ReturnsNetemDeliverySystem()
    {
        var system = UnreliableDeliveryBinding.Build(
            FakeStruct.Empty().SetString("type", "netem"), MemoryRental.Shared);

        try
        {
            Assert.That(system, Is.TypeOf<NetemDeliverySystem>());
        }
        finally
        {
            ((IDisposable)system).Dispose();
        }
    }

    [Test]
    public void Build_WithUnknownType_Throws()
    {
        Assert.Throws<ArgumentException>(() => UnreliableDeliveryBinding.Build(
            FakeStruct.Empty().SetString("type", "bogus"), MemoryRental.Shared));
    }

    [Test]
    public void Build_WithFourStateWithoutParameters_Throws()
    {
        Assert.Throws<ArgumentException>(() => UnreliableDeliveryBinding.Build(
            FakeStruct.Empty()
                .SetString("type", "netem")
                .SetString("lossmodel", "fourState"),
            MemoryRental.Shared));
    }

    private sealed class FakeStruct : ITransportStructValue
    {
        private readonly Dictionary<string, string> _strings = new();
        private readonly Dictionary<string, long> _numbers = new();
        private readonly Dictionary<string, FakeStruct> _structs = new();

        public static FakeStruct Empty()
        {
            return new FakeStruct();
        }

        public FakeStruct SetString(string fieldName, string value)
        {
            _strings[fieldName] = value;
            return this;
        }

        public FakeStruct SetLong(string fieldName, long value)
        {
            _numbers[fieldName] = value;
            return this;
        }

        public FakeStruct SetStruct(string fieldName, FakeStruct value)
        {
            _structs[fieldName] = value;
            return this;
        }

        public bool TryGetString(string fieldName, out string value)
        {
            if (_strings.TryGetValue(fieldName, out var found))
            {
                value = found;
                return true;
            }

            value = string.Empty;
            return false;
        }

        public bool TryGetInt64(string fieldName, out long value)
        {
            return _numbers.TryGetValue(fieldName, out value);
        }

        public bool TryGetDecimal(string fieldName, out decimal value)
        {
            value = 0;
            return false;
        }

        public bool TryGetStruct(string fieldName, out ITransportStructValue value)
        {
            if (_structs.TryGetValue(fieldName, out var found))
            {
                value = found;
                return true;
            }

            value = null!;
            return false;
        }

        public bool TryGetArray(string fieldName, out ITransportArrayValue value)
        {
            value = null!;
            return false;
        }
    }
}
