namespace Pontifex.NoAck.Raw.Unreliable.Tests;

[TestFixtureSource(typeof(ConformanceAdapterSource), nameof(ConformanceAdapterSource.GetAdapters))]
public sealed class ControlContractTests : ConformanceTestBase
{
    public ControlContractTests(
        INoAckRawUnreliableConformanceTestAdapter adapter) : base(adapter) { }

    [Test]
    public void CheckpointGatesAreStableAndDistinct()
    {
        var client = Scope.CreateClient();
        var control = GetControl(client);

        var g0a = control.BeforeSendCommitGate;
        var g0b = control.BeforeSendCommitGate;
        Assert.That(g0b, Is.SameAs(g0a));

        var g1a = control.BeforeStopStateTransitionGate;
        var g1b = control.BeforeStopStateTransitionGate;
        Assert.That(g1b, Is.SameAs(g1a));

        var g2a = control.BeforeStoppedCallbackGate;
        var g2b = control.BeforeStoppedCallbackGate;
        Assert.That(g2b, Is.SameAs(g2a));

        Assert.Multiple(() =>
        {
            Assert.That(g1a, Is.Not.SameAs(g0a));
            Assert.That(g2a, Is.Not.SameAs(g0a));
            Assert.That(g2a, Is.Not.SameAs(g1a));
        });
    }

    [Test]
    public void FailNextStartValidatesItsOneShotPreStartState()
    {
        var client = Scope.CreateClient();
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
        var client2 = scope2.CreateClient();
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
        var client = Scope.CreateClient();
        var control = GetControl(client);

        Assert.Throws<InvalidOperationException>(
            () => control.InjectUnrecoverableFailure());

        using var scope2 = Adapter.CreateScope();
        var client2 = scope2.CreateClient();
        var control2 = GetControl(client2);
        var recorder2 = new StopRecorder();

        Assert.That(client2.Start(recorder2.Callback), Is.True);
        control2.InjectUnrecoverableFailure();
        recorder2.WaitAsync().GetAwaiter().GetResult();

        Assert.Throws<InvalidOperationException>(
            () => control2.InjectUnrecoverableFailure());

        using var scope3 = Adapter.CreateScope();
        var client3 = scope3.CreateClient();
        var control3 = GetControl(client3);
        var recorder3 = new StopRecorder();

        Assert.That(client3.Start(recorder3.Callback), Is.True);

        var gate = control3.BeforeStoppedCallbackGate;

        using (var lease = new CheckpointLease(gate))
        {
            var stopTask = Task.Run(() => client3.Stop());
            lease.Reached.GetAwaiter().GetResult();

            Task.Run(() =>
            {
                Assert.Throws<InvalidOperationException>(() => control3.InjectUnrecoverableFailure()); 
            });
        }

        recorder3.WaitAsync().GetAwaiter().GetResult();
    }
}
