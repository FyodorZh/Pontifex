namespace Pontifex.NoAck.Raw.Unreliable.Tests;

[TestFixtureSource(typeof(ConformanceAdapterSource), nameof(ConformanceAdapterSource.GetAdapters))]
public sealed class SendValidationTests : ConformanceTestBase
{
    public SendValidationTests(
        INoAckRawUnreliableConformanceTestAdapter adapter) : base(adapter) { }

    [Test]
    public async Task ClientExactLimitAndOversizedMessagesAreClassifiedCorrectly()
    {
        var client = Scope.CreateClient(instrumented: true);
        var recorder = new StopRecorder();

        Assert.That(client.Start(recorder.Callback), Is.True);

        var exactMessage = Scope.CreateExactLimitMessage(client);
        var exactResult = client.TrySend(exactMessage);

        Assert.That(exactResult, Is.Not.EqualTo(SendResult.MessageTooBig));
        Assert.Multiple(() =>
        {
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.True);
        });

        var overMessage = Scope.CreateOneByteOverLimitMessage(client);
        var overResult = client.TrySend(overMessage);

        Assert.Multiple(() =>
        {
            Assert.That(overResult, Is.EqualTo(SendResult.MessageTooBig));
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.True);
        });

        client.Stop();
        await recorder.WaitAsync();
    }

    [Test]
    public async Task ClientSendBeforeStartAndAfterStopReturnsNotConnected()
    {
        var client = Scope.CreateClient(instrumented: true);

        var beforeStartMessage = Scope.CreateSmallValidMessage(client);
        Assert.That(client.TrySend(beforeStartMessage), Is.EqualTo(SendResult.NotConnected));

        var recorder = new StopRecorder();
        Assert.That(client.Start(recorder.Callback), Is.True);
        client.Stop();
        await recorder.WaitAsync();

        var afterStopMessage = Scope.CreateSmallValidMessage(client);
        Assert.That(client.TrySend(afterStopMessage), Is.EqualTo(SendResult.NotConnected));
    }

    [Test]
    public async Task ServerSendBeforeStartAndAfterStopReturnsNotConnected()
    {
        var server = Scope.CreateServer(instrumented: true);
        var foreignDest = Scope.CreateForeignServerDestination();

        var beforeStartMessage = Scope.CreateSmallValidMessage(server);
        Assert.That(server.TrySend(foreignDest, beforeStartMessage),
            Is.EqualTo(SendResult.NotConnected));

        var recorder = new StopRecorder();
        Assert.That(server.Start(recorder.Callback), Is.True);
        server.Stop();
        await recorder.WaitAsync();

        var afterStopMessage = Scope.CreateSmallValidMessage(server);
        Assert.That(server.TrySend(foreignDest, afterStopMessage),
            Is.EqualTo(SendResult.NotConnected));
    }

    [Test]
    public async Task ClientNullMessageIsRejectedNonFatally()
    {
        var client = Scope.CreateClient(instrumented: true);
        var recorder = new StopRecorder();

        Assert.That(client.Start(recorder.Callback), Is.True);

        var result = client.TrySend(null!);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(SendResult.InvalidMessage));
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.True);
            Assert.That(recorder.Count, Is.Zero);
        });

        client.Stop();
        await recorder.WaitAsync();
    }

    [Test]
    public async Task ServerNullMessageIsRejectedNonFatally()
    {
        var server = Scope.CreateServer(instrumented: true);
        var recorder = new StopRecorder();
        var foreignDest = Scope.CreateForeignServerDestination();

        Assert.That(server.Start(recorder.Callback), Is.True);

        var result = server.TrySend(foreignDest, null!);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(SendResult.InvalidMessage));
            Assert.That(server.IsValid, Is.True);
            Assert.That(server.IsStarted, Is.True);
            Assert.That(recorder.Count, Is.Zero);
        });

        server.Stop();
        await recorder.WaitAsync();
    }

    [Test]
    public async Task ServerForeignDestinationIsRejectedNonFatally()
    {
        var server = Scope.CreateServer(instrumented: true);
        var recorder = new StopRecorder();
        var foreignDest = Scope.CreateForeignServerDestination();

        Assert.That(server.Start(recorder.Callback), Is.True);

        var message = Scope.CreateSmallValidMessage(server);
        var result = server.TrySend(foreignDest, message);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(SendResult.InvalidAddress));
            Assert.That(server.IsValid, Is.True);
            Assert.That(server.IsStarted, Is.True);
            Assert.That(recorder.Count, Is.Zero);
        });

        server.Stop();
        await recorder.WaitAsync();
    }

    [Test]
    public void EveryAdditionalNonOkResultIsNonFatal()
    {
        var cases = Scope.CreateAdditionalNonOkCases().ToList();

        try
        {
            foreach (var testCase in cases)
            {
                var initialValid = testCase.Transport.IsValid;
                var initialStarted = testCase.Transport.IsStarted;

                var result = testCase.Invoke();

                Assert.Multiple(() =>
                {
                    Assert.That(result, Is.EqualTo(testCase.ExpectedResult),
                        $"Case '{testCase.Name}' returned unexpected result");
                    Assert.That(result, Is.Not.EqualTo(SendResult.Ok));
                    Assert.That(testCase.Transport.IsValid, Is.EqualTo(initialValid),
                        $"Case '{testCase.Name}' changed IsValid");
                    Assert.That(testCase.Transport.IsStarted, Is.EqualTo(initialStarted),
                        $"Case '{testCase.Name}' changed IsStarted");
                });
            }
        }
        finally
        {
            foreach (var testCase in cases)
            {
                try
                {
                    if (testCase.Transport.IsValid && testCase.Transport.IsStarted)
                        testCase.Transport.Stop();
                }
                catch { }

                try { testCase.Dispose(); } catch { }
            }
        }
    }
}
