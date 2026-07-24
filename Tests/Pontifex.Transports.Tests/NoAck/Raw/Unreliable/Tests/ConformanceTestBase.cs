using Pontifex.NoAck.Raw;

namespace Pontifex.NoAck.Raw.Unreliable.Tests;

public abstract class ConformanceTestBase
{
    protected INoAckRawUnreliableConformanceTestAdapter Adapter { get; }

    protected INoAckRawUnreliableConformanceScope Scope { get; private set; }

    protected ConformanceTestBase(INoAckRawUnreliableConformanceTestAdapter adapter)
    {
        Adapter = adapter;
    }

    [SetUp]
    public void InitScope()
    {
        Scope = Adapter.CreateScope();
    }

    [TearDown]
    public void DisposeScope()
    {
        try { Scope?.Dispose(); } catch { }
    }

    protected static INoAckRawUnreliableConformanceControl GetControl(ITransport transport)
    {
        var controls = new List<IControl>();
        transport.GetControls(controls, c => c is INoAckRawUnreliableConformanceControl);
        Assert.That(controls, Has.Count.EqualTo(1),
            "Expected exactly one INoAckRawUnreliableConformanceControl; adapter contract failure");
        return (INoAckRawUnreliableConformanceControl)controls[0];
    }
}
