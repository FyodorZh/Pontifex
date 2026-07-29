namespace Pontifex.NoAck.Raw.Unreliable.Tests;

[TestFixtureSource(typeof(ConformanceAdapterSource), nameof(ConformanceAdapterSource.GetAdapters))]
public sealed class FailedStartTests : ConformanceTestBase
{
    public FailedStartTests(
        INoAckRawUnreliableConformanceTestAdapter adapter) : base(adapter) { }

    [Test]
    public void ForcedClientStartFailureIsTerminalAndSilent()
    {
        var client = Scope.CreateClient();
        var control = GetControl(client);
        var recorder = new StopRecorder();

        control.FailNextStart();
        var startResult = client.Start(recorder.Callback);

        Assert.Multiple(() =>
        {
            Assert.That(startResult, Is.False);
            Assert.That(client.IsValid, Is.False);
            Assert.That(client.IsStarted, Is.False);
            Assert.That(recorder.Count, Is.Zero);
            Assert.That(client.Start(new StopRecorder().Callback), Is.False);
        });
    }

    [Test]
    public void ForcedServerStartFailureIsTerminalAndSilent()
    {
        var server = Scope.CreateServer();
        var control = GetControl(server);
        var recorder = new StopRecorder();

        control.FailNextStart();
        var startResult = server.Start(recorder.Callback);

        Assert.Multiple(() =>
        {
            Assert.That(startResult, Is.False);
            Assert.That(server.IsValid, Is.False);
            Assert.That(server.IsStarted, Is.False);
            Assert.That(recorder.Count, Is.Zero);
            Assert.That(server.Start(new StopRecorder().Callback), Is.False);
        });
    }

    [Test]
    public void StartFailureArmingIsOneShot()
    {
        var client = Scope.CreateClient();
        var control = GetControl(client);

        control.FailNextStart();
        Assert.Throws<InvalidOperationException>(() => control.FailNextStart());

        var recorder = new StopRecorder();
        Assert.That(client.Start(recorder.Callback), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(client.IsValid, Is.False);
            Assert.That(client.IsStarted, Is.False);
            Assert.That(recorder.Count, Is.Zero);
        });
    }
}
