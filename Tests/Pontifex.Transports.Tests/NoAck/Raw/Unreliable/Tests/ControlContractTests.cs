namespace Pontifex.NoAck.Raw.Unreliable.Tests;

[TestFixtureSource(typeof(ConformanceAdapterSource), nameof(ConformanceAdapterSource.GetAdapters))]
public sealed class ControlContractTests : ConformanceTestBase
{
    public ControlContractTests(
        INoAckRawUnreliableConformanceTestAdapter adapter) : base(adapter) { }

    [Test]
    public void CheckpointLookupIsStableAndValidatesItsArgument()
    {
        var client = Scope.CreateClient(instrumented: true);
        var control = GetControl(client);

        foreach (var cp in Enum.GetValues<
            NoAckRawUnreliableCarrierIndependentCoreConformanceCheckpoint>())
        {
            var first = control.GetGate(cp);
            var second = control.GetGate(cp);
            Assert.That(second, Is.SameAs(first),
                $"GetGate({cp}) returned different instances across calls");
        }

        var gate0 = control.GetGate(
            NoAckRawUnreliableCarrierIndependentCoreConformanceCheckpoint.BeforeTrySendStateDecision);
        var gate1 = control.GetGate(
            NoAckRawUnreliableCarrierIndependentCoreConformanceCheckpoint.BeforeStopStateTransition);
        var gate2 = control.GetGate(
            NoAckRawUnreliableCarrierIndependentCoreConformanceCheckpoint.BeforeStoppedCallback);

        Assert.Multiple(() =>
        {
            Assert.That(gate1, Is.Not.SameAs(gate0));
            Assert.That(gate2, Is.Not.SameAs(gate0));
            Assert.That(gate2, Is.Not.SameAs(gate1));
        });

        var undefinedValue = (NoAckRawUnreliableCarrierIndependentCoreConformanceCheckpoint)99;
        Assert.Throws<ArgumentOutOfRangeException>(
            () => control.GetGate(undefinedValue));
    }

    [Test]
    public void FailNextStartValidatesItsOneShotPreStartState()
    {
        var client = Scope.CreateClient(instrumented: true);
        var control = GetControl(client);

        control.FailNextStart();
        Assert.Throws<InvalidOperationException>(() => control.FailNextStart());
        Assert.That(client.Start(new StopRecorder().Callback), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(client.IsValid, Is.False);
            Assert.That(client.IsStarted, Is.False);
        });

        using var scope2 = Adapter.CreateScope();
        var client2 = scope2.CreateClient(instrumented: true);
        var control2 = GetControl(client2);
        var recorder = new StopRecorder();

        Assert.That(client2.Start(recorder.Callback), Is.True);
        Assert.Throws<InvalidOperationException>(() => control2.FailNextStart());
        client2.Stop();
        recorder.WaitAsync().GetAwaiter().GetResult();
    }

    [Test]
    public void InjectUnrecoverableFailureValidatesItsRunningOneShotState()
    {
        var client = Scope.CreateClient(instrumented: true);
        var control = GetControl(client);

        Assert.Throws<InvalidOperationException>(
            () => control.InjectUnrecoverableFailure());

        using var scope2 = Adapter.CreateScope();
        var client2 = scope2.CreateClient(instrumented: true);
        var control2 = GetControl(client2);
        var recorder2 = new StopRecorder();

        Assert.That(client2.Start(recorder2.Callback), Is.True);
        control2.InjectUnrecoverableFailure();
        recorder2.WaitAsync().GetAwaiter().GetResult();

        Assert.Throws<InvalidOperationException>(
            () => control2.InjectUnrecoverableFailure());

        using var scope3 = Adapter.CreateScope();
        var client3 = scope3.CreateClient(instrumented: true);
        var control3 = GetControl(client3);
        var recorder3 = new StopRecorder();

        Assert.That(client3.Start(recorder3.Callback), Is.True);

        var gate = control3.GetGate(
            NoAckRawUnreliableCarrierIndependentCoreConformanceCheckpoint.BeforeStoppedCallback);

        using (var lease = new CheckpointLease(gate))
        {
            var stopTask = Task.Run(() => client3.Stop());
            lease.Reached.GetAwaiter().GetResult();

            Assert.Throws<InvalidOperationException>(
                () => control3.InjectUnrecoverableFailure());
        }

        recorder3.WaitAsync().GetAwaiter().GetResult();
    }
}
