using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.NoAck;
using Pontifex.StopReasons;
using Pontifex.Tests.Endpoints;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Black-box contract tests for <see cref="RawReliableNoAckServerTransport"/>, exercised through the
/// extended public + protected surface exposed by <see cref="TestRawReliableNoAckServerTransport"/>.
/// Covers the handler-factory initialization lifecycle, the factory hand-off with its stop-once gate,
/// and the admission-failure notifications. The inherited session admission and ticker behavior is
/// covered by <see cref="RawReliableServerTransportTests"/> and is not repeated here.
/// </summary>
[Category("Transports")]
public sealed class RawReliableNoAckServerTransportTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    private static readonly IEndPoint Remote = new StringEndPoint("remote");

    private static TestRawReliableNoAckServerTransport Create(
        int maxConcurrentHandshakes = 2,
        int maxActiveSessions = 2,
        int tickPeriodMs = 100)
    {
        return new TestRawReliableNoAckServerTransport(
            maxConcurrentHandshakes: maxConcurrentHandshakes,
            maxActiveSessions: maxActiveSessions,
            tickPeriodMs: tickPeriodMs);
    }

    private static UserIntention Reason(string source = "test")
    {
        return new UserIntention(source);
    }

    private static void InitializeAndStart(
        TestRawReliableNoAckServerTransport transport,
        RecordingRawReliableNoAckServerHandlerFactory factory)
    {
        Assert.That(transport.Init(factory), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);
    }

    #region Interface and type

    [Test]
    public void NoAckServerTransport_ExposesReliableNoAckServerContract()
    {
        var transport = Create();

        Assert.That(transport, Is.InstanceOf<IRawReliableNoAckServer>());
        Assert.That(transport, Is.InstanceOf<IRawReliableServerTransport<IRawReliableNoAckServerHandler>>());
        Assert.That(transport, Is.InstanceOf<IRawReliableTransport>());
        Assert.That(transport, Is.InstanceOf<IRawTransport>());
        Assert.That(transport, Is.InstanceOf<ITransport>());
    }

    [Test]
    public void Type_IsRawReliableNoAck()
    {
        var transport = Create();

        Assert.That(transport.Type, Is.EqualTo(TransportType.RawReliableNoAck));
    }

    #endregion

    #region Init

    [Test]
    public void Init_WithNull_ThrowsWithoutChangingLifecycle()
    {
        var transport = Create();

        Assert.Throws<ArgumentNullException>(() => transport.Init(null!));

        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.ExposedHasStartBeenAttempted, Is.False);
        Assert.That(transport.Init(new RecordingRawReliableNoAckServerHandlerFactory()), Is.True);
    }

    [Test]
    public void Init_FirstCallSucceeds_SecondFails()
    {
        var transport = Create();
        using var firstHandler = new RecordingRawReliableNoAckServerHandler(Memory);
        using var secondHandler = new RecordingRawReliableNoAckServerHandler(Memory);
        var first = new RecordingRawReliableNoAckServerHandlerFactory { Handler = firstHandler };
        var second = new RecordingRawReliableNoAckServerHandlerFactory { Handler = secondHandler };

        Assert.That(transport.Init(first), Is.True);
        Assert.That(transport.Init(second), Is.False);
        Assert.That(transport.Start(_ => { }), Is.True);

        IRawReliableNoAckServerHandler? result = transport.ExposedTryCreateHandler(Remote);

        Assert.That(result, Is.InstanceOf<StopOnceNoAckServerHandler>());
        Assert.That(((StopOnceNoAckServerHandler)result!).Inner, Is.SameAs(firstHandler));
        Assert.That(first.TryCreateCalls, Is.EqualTo(1));
        Assert.That(second.TryCreateCalls, Is.Zero);
    }

    [Test]
    public void Init_AfterStartAttempt_ReturnsFalse()
    {
        var transport = Create();
        Assert.That(transport.Init(new RecordingRawReliableNoAckServerHandlerFactory()), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.Init(new RecordingRawReliableNoAckServerHandlerFactory()), Is.False);

        Assert.That(transport.ExposedHasStartBeenAttempted, Is.True);
        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
    }

    #endregion

    #region TryCreateHandler

    [Test]
    public void TryCreateHandler_WhenNotStarted_ReturnsNull()
    {
        var transport = Create();
        var factory = new RecordingRawReliableNoAckServerHandlerFactory();
        Assert.That(transport.Init(factory), Is.True);

        Assert.That(transport.ExposedTryCreateHandler(Remote), Is.Null);

        Assert.That(factory.TryCreateCalls, Is.Zero);
    }

    [Test]
    public void TryCreateHandler_ReturnsStopOnceWrapper()
    {
        var transport = Create();
        using var handler = new RecordingRawReliableNoAckServerHandler(Memory);
        var factory = new RecordingRawReliableNoAckServerHandlerFactory { Handler = handler };
        InitializeAndStart(transport, factory);

        IRawReliableNoAckServerHandler? result = transport.ExposedTryCreateHandler(Remote);

        Assert.That(result, Is.TypeOf<StopOnceNoAckServerHandler>());
        Assert.That(((StopOnceNoAckServerHandler)result!).Inner, Is.SameAs(handler));
    }

    [Test]
    public void TryCreateHandler_WhenFactoryThrows_ReturnsNull()
    {
        var transport = Create();
        var factory = new RecordingRawReliableNoAckServerHandlerFactory
        {
            ThrowOnTryCreate = true,
            TryCreateException = new InvalidOperationException("factory failure")
        };
        InitializeAndStart(transport, factory);

        IRawReliableNoAckServerHandler? result = null;

        Assert.DoesNotThrow(() => result = transport.ExposedTryCreateHandler(Remote));

        Assert.That(result, Is.Null);
        Assert.That(factory.TryCreateCalls, Is.EqualTo(1));
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsStarted, Is.True);
    }

    [Test]
    public void TryCreateHandler_InvokesFactoryWithRemote()
    {
        var transport = Create();
        using var handler = new RecordingRawReliableNoAckServerHandler(Memory);
        var factory = new RecordingRawReliableNoAckServerHandlerFactory { Handler = handler };
        InitializeAndStart(transport, factory);

        transport.ExposedTryCreateHandler(Remote);

        Assert.That(factory.TryCreateCalls, Is.EqualTo(1));
        Assert.That(factory.LastRemote, Is.SameAs(Remote));
    }

    [Test]
    [Category("Transports.Concurrency")]
    public void TryCreateHandler_CallsDoNotOverlap()
    {
        var transport = Create();
        using var ready = new CountdownEvent(2);
        using var startGate = new ManualResetEventSlim(false);
        using var factoryEntered = new ManualResetEventSlim(false);
        using var releaseFactory = new ManualResetEventSlim(false);
        int active = 0;
        int maxActive = 0;
        Exception? threadFailure = null;
        var factory = new RecordingRawReliableNoAckServerHandlerFactory(remote =>
        {
            int current = Interlocked.Increment(ref active);
            UpdateMax(ref maxActive, current);
            factoryEntered.Set();

            try
            {
                if (!releaseFactory.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("The factory gate was not released.");
                }
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }

            return null;
        });
        InitializeAndStart(transport, factory);

        Thread Contender() => new(() =>
        {
            ready.Signal();
            startGate.Wait();

            try
            {
                transport.ExposedTryCreateHandler(Remote);
            }
            catch (Exception ex)
            {
                threadFailure = ex;
            }
        });

        var first = Contender();
        var second = Contender();
        first.IsBackground = true;
        second.IsBackground = true;

        try
        {
            first.Start();
            second.Start();

            Assert.That(ready.Wait(TimeSpan.FromSeconds(10)), Is.True,
                "The contenders did not become ready.");

            startGate.Set();

            Assert.That(factoryEntered.Wait(TimeSpan.FromSeconds(10)), Is.True,
                "Neither contender entered the factory.");
        }
        finally
        {
            startGate.Set();
            releaseFactory.Set();
        }

        Assert.That(first.Join(TimeSpan.FromSeconds(10)), Is.True, "The first contender did not finish.");
        Assert.That(second.Join(TimeSpan.FromSeconds(10)), Is.True, "The second contender did not finish.");

        Assert.That(threadFailure, Is.Null);
        Assert.That(maxActive, Is.EqualTo(1));
        Assert.That(factory.TryCreateCalls, Is.EqualTo(2));
    }

    private static void UpdateMax(ref int max, int value)
    {
        int observed = Volatile.Read(ref max);
        while (value > observed)
        {
            int previous = Interlocked.CompareExchange(ref max, value, observed);
            if (previous == observed)
            {
                return;
            }

            observed = previous;
        }
    }

    #endregion

    #region Admission

    [Test]
    public void AbandonAdmission_WithHandler_NotifiesOnStoppedOnce()
    {
        var transport = Create();
        using var recording = new RecordingRawReliableNoAckServerHandler(Memory);
        var factory = new RecordingRawReliableNoAckServerHandlerFactory { Handler = recording };
        InitializeAndStart(transport, factory);
        Assert.That(transport.ExposedTryBeginAdmission(), Is.True);
        IRawReliableNoAckServerHandler? handler = transport.ExposedTryCreateHandler(Remote);
        Assert.That(handler, Is.Not.Null);

        transport.ExposedAbandonAdmission(handler, Reason("abandon"));
        handler!.OnStopped(Reason("second"));

        Assert.That(recording.StoppedCount, Is.EqualTo(1));
        Assert.That(transport.ExposedActiveSessions, Is.Zero);
    }

    [Test]
    public void CompleteAdmission_WhenSessionCannotStart_NotifiesOnStoppedOnce()
    {
        var transport = Create();
        using var recording = new RecordingRawReliableNoAckServerHandler(Memory);
        var factory = new RecordingRawReliableNoAckServerHandlerFactory { Handler = recording };
        InitializeAndStart(transport, factory);
        Assert.That(transport.ExposedTryBeginAdmission(), Is.True);
        IRawReliableNoAckServerHandler? handler = transport.ExposedTryCreateHandler(Remote);
        Assert.That(handler, Is.Not.Null);
        var session = new TestRawReliableServerSession(handler!);
        session.Queue.RejectNextSchedule = true;

        Assert.That(
            transport.ExposedCompleteAdmission(
                session, handler, new TextFail(transport.Name, "Failed to start or register a session")),
            Is.False);

        Assert.That(recording.StoppedCount, Is.EqualTo(1));

        session.DrainAll();

        Assert.That(recording.StoppedCount, Is.EqualTo(1));
        Assert.That(transport.ExposedActiveSessions, Is.Zero);
    }

    [Test]
    public void CompleteAdmission_WhenTransportStopping_DoesNotPreemptStartedSession()
    {
        var transport = Create();
        using var recording = new RecordingRawReliableNoAckServerHandler(Memory);
        var factory = new RecordingRawReliableNoAckServerHandlerFactory { Handler = recording };
        InitializeAndStart(transport, factory);
        Assert.That(transport.ExposedTryBeginAdmission(), Is.True);
        IRawReliableNoAckServerHandler? handler = transport.ExposedTryCreateHandler(Remote);
        Assert.That(handler, Is.Not.Null);
        var session = new TestRawReliableServerSession(handler!);

        Assert.That(transport.Stop(Reason("transport")), Is.True);
        Assert.That(
            transport.ExposedCompleteAdmission(
                session, handler, new TextFail(transport.Name, "Failed to start or register a session")),
            Is.False);

        // The session accepted its start, so it owns the terminal notification and the admission
        // fallback must not preempt it.
        Assert.That(recording.StoppedCount, Is.Zero);

        session.DrainAll();

        Assert.That(recording.StoppedCount, Is.EqualTo(1));
        Assert.That(transport.ExposedActiveSessions, Is.Zero);
    }

    #endregion
}
