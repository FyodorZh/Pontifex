using System.Collections.Generic;
using Pontifex.Raw;
using Pontifex.Raw.Unreliable;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Black-box contract tests for <see cref="RawUnreliableTransport"/>. The class is a thin marker facade
/// over <see cref="Pontifex.Raw.RawTransport"/>: this test-set covers only its delta, the unreliable raw
/// transport contract and the typed conformance-control view.
/// </summary>
[Category("Transports")]
public sealed class RawUnreliableTransportTests
{
    private static TestRawUnreliableTransport Create()
    {
        return new TestRawUnreliableTransport();
    }

    [Test]
    public void RawUnreliableTransport_ExposesUnreliableRawTransportContract()
    {
        var transport = Create();

        Assert.That(transport, Is.InstanceOf<IRawUnreliableTransport>());
        Assert.That(transport, Is.InstanceOf<IRawTransport>());
    }

    [Test]
    public void GetControls_ExposesRawUnreliableTransportConformanceControl()
    {
        var transport = Create();
        var controls = new List<IControl>();

        transport.GetControls(controls);

        Assert.That(controls, Has.Exactly(1).InstanceOf<IRawUnreliableTransportConformanceControl>());
        IRawUnreliableTransportConformanceControl conformance =
            (IRawUnreliableTransportConformanceControl)controls[0];
        Assert.That(conformance, Is.SameAs(transport.ExposedConformance));
    }

    [Test]
    public void TryMakeReliable_WhenOwnerImplementsDebugReliability_DelegatesToIt()
    {
        var transport = Create();
        transport.ReliableForDebugResult = true;

        Assert.That(transport.ExposedConformance.TryMakeReliable(), Is.True);
        Assert.That(transport.ReliableForDebugCalls, Is.EqualTo(1));

        transport.ReliableForDebugResult = false;

        Assert.That(transport.ExposedConformance.TryMakeReliable(), Is.False);
        Assert.That(transport.ReliableForDebugCalls, Is.EqualTo(2));
    }
}
