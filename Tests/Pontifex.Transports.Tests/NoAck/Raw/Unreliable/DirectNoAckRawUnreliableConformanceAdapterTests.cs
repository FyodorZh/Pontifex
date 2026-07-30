using Actuarius.Memory;
using Pontifex.NoAck.Raw.Unreliable;

namespace Pontifex.Tests.NoAck.Raw.Unreliable;

public sealed class DirectNoAckRawUnreliableConformanceAdapterTests
{
    [Test]
    public void CreateFixture_CreatesUnstartedLinkedEndpoints()
    {
        using var fixture = new DirectNoAckRawUnreliableConformanceAdapter().CreateFixture(
            new NoAckRawUnreliableConformanceFixtureOptions { MemoryRental = MemoryRental.Shared });

        var firstClient = fixture.CreateClient();
        var secondClient = fixture.CreateClient();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsStarted, Is.False);
            Assert.That(firstClient.IsStarted, Is.False);
            Assert.That(secondClient.IsStarted, Is.False);
            Assert.That(fixture.Server.Memory, Is.SameAs(MemoryRental.Shared));
            Assert.That(firstClient.Memory, Is.SameAs(MemoryRental.Shared));
            Assert.That(secondClient.Memory, Is.SameAs(MemoryRental.Shared));
        });
    }

    [Test]
    public void CreateClient_AfterServerStartsOrStops_ReturnsUnstartedClient()
    {
        using var fixture = new DirectNoAckRawUnreliableConformanceAdapter().CreateFixture();

        Assert.That(fixture.Server.Start(_ => { }), Is.True);
        var runningServerClient = fixture.CreateClient();
        Assert.That(fixture.Server.Stop(), Is.True);

        var stoppedServerClient = fixture.CreateClient();

        Assert.Multiple(() =>
        {
            Assert.That(runningServerClient.IsStarted, Is.False);
            Assert.That(stoppedServerClient.IsStarted, Is.False);
        });
    }

    [Test]
    public void Dispose_ResetsArmedConformanceGates()
    {
        var fixture = new DirectNoAckRawUnreliableConformanceAdapter().CreateFixture();
        var serverControl = GetControl<INoAckRawUnreliableConformanceControl>(fixture.Server);
        serverControl.BeforeStopStateTransitionGate.Arm();
        serverControl.BeforeStoppedCallbackGate.Arm();
        serverControl.BeforeSendCommitGate.Arm();
        serverControl.AfterSendCommitGate.Arm();
        serverControl.AfterReceivedGate.Arm();

        fixture.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(serverControl.BeforeStopStateTransitionGate.IsArmed, Is.False);
            Assert.That(serverControl.BeforeStoppedCallbackGate.IsArmed, Is.False);
            Assert.That(serverControl.BeforeSendCommitGate.IsArmed, Is.False);
            Assert.That(serverControl.AfterSendCommitGate.IsArmed, Is.False);
            Assert.That(serverControl.AfterReceivedGate.IsArmed, Is.False);
        });
    }

    private static TControl GetControl<TControl>(ITransport transport)
        where TControl : class, IControl
    {
        var controls = new List<IControl>();
        transport.GetControls(controls, control => control is TControl);
        return controls.OfType<TControl>().Single();
    }
}
