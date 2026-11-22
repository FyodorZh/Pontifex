using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Tests.Endpoints;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Black-box contract tests for <see cref="RawReliableAdmissionServerTransport"/>, exercised through the
/// extended public + protected surface exposed by <see cref="TestRawReliableAdmissionServerTransport"/>.
/// Covers the handler-factory initialization lifecycle, the factory hand-off with its stop-once gate,
/// and the admission-failure notifications. The inherited session admission and ticker behavior is
/// covered by <see cref="RawReliableServerTransportTests"/> and is not repeated here.
/// </summary>
[Category("Transports")]
public sealed class RawReliableAdmissionServerTransportTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    private static readonly IEndPoint Remote = new StringEndPoint("remote");

    private static TestRawReliableAdmissionServerTransport Create(
        int maxConcurrentHandshakes = 2,
        int maxActiveSessions = 2,
        int tickPeriodMs = 100)
    {
        return new TestRawReliableAdmissionServerTransport(
            maxConcurrentHandshakes: maxConcurrentHandshakes,
            maxActiveSessions: maxActiveSessions,
            tickPeriodMs: tickPeriodMs);
    }

    private static UserIntention Reason(string source = "test")
    {
        return new UserIntention(source);
    }

    private static void InitializeAndStart(
        TestRawReliableAdmissionServerTransport transport,
        RecordingRawReliableAdmissionServerHandlerFactory factory)
    {
        Assert.That(transport.Init(factory), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);
    }

    #region Interface and type

    [Test]
    public void AdmissionServerTransport_ExposesReliableServerContract()
    {
        var transport = Create();

        Assert.That(transport, Is.InstanceOf<IRawReliableServer>());
        Assert.That(transport, Is.InstanceOf<IRawReliableServerTransport<IRawReliableServerHandler>>());
        Assert.That(transport, Is.InstanceOf<IRawReliableTransport>());
        Assert.That(transport, Is.InstanceOf<IRawTransport>());
        Assert.That(transport, Is.InstanceOf<ITransport>());
    }

    [Test]
    public void Type_IsRawReliable()
    {
        var transport = Create();

        Assert.That(transport.Type, Is.EqualTo(TransportType.RawReliable));
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
        Assert.That(transport.Init(new RecordingRawReliableAdmissionServerHandlerFactory()), Is.True);
    }

    [Test]
    public void Init_FirstCallSucceeds_SecondFails()
    {
        var transport = Create();
        using var firstHandler = new RecordingRawReliableHandshakeServerHandler(Memory);
        using var secondHandler = new RecordingRawReliableHandshakeServerHandler(Memory);
        var first = new RecordingRawReliableAdmissionServerHandlerFactory { Handler = firstHandler };
        var second = new RecordingRawReliableAdmissionServerHandlerFactory { Handler = secondHandler };

        Assert.That(transport.Init(first), Is.True);
        Assert.That(transport.Init(second), Is.False);
        Assert.That(transport.Start(_ => { }), Is.True);

        IRawReliableServerHandler? result = transport.ExposedTryCreateHandler(Remote);

        Assert.That(result, Is.InstanceOf<StopOnceServerHandler>());
        Assert.That(((StopOnceServerHandler)result!).Inner, Is.SameAs(firstHandler));
        Assert.That(first.TryCreateCalls, Is.EqualTo(1));
        Assert.That(second.TryCreateCalls, Is.Zero);
    }

    [Test]
    public void Init_AfterStartAttempt_ReturnsFalse()
    {
        var transport = Create();
        Assert.That(transport.Init(new RecordingRawReliableAdmissionServerHandlerFactory()), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.Init(new RecordingRawReliableAdmissionServerHandlerFactory()), Is.False);

        Assert.That(transport.ExposedHasStartBeenAttempted, Is.True);
        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
    }

    #endregion

    #region TryCreateHandler

    [Test]
    public void TryCreateHandler_WhenNotStarted_ReturnsNull()
    {
        var transport = Create();
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory();
        Assert.That(transport.Init(factory), Is.True);

        Assert.That(transport.ExposedTryCreateHandler(Remote), Is.Null);

        Assert.That(factory.TryCreateCalls, Is.Zero);
    }

    [Test]
    public void TryCreateHandler_ReturnsStopOnceWrapper()
    {
        var transport = Create();
        using var handler = new RecordingRawReliableHandshakeServerHandler(Memory);
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory { Handler = handler };
        InitializeAndStart(transport, factory);

        IRawReliableServerHandler? result = transport.ExposedTryCreateHandler(Remote);

        Assert.That(result, Is.TypeOf<StopOnceServerHandler>());
        Assert.That(((StopOnceServerHandler)result!).Inner, Is.SameAs(handler));
    }

    [Test]
    public void TryCreateHandler_WhenFactoryThrows_ReturnsNull()
    {
        var transport = Create();
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory
        {
            ThrowOnTryCreate = true,
            TryCreateException = new InvalidOperationException("factory failure")
        };
        InitializeAndStart(transport, factory);

        IRawReliableServerHandler? result = null;

        Assert.DoesNotThrow(() => result = transport.ExposedTryCreateHandler(Remote));

        Assert.That(result, Is.Null);
        Assert.That(factory.TryCreateCalls, Is.EqualTo(1));
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsStarted, Is.True);
    }

    [Test]
    public void TryCreateHandler_AfterNullResult_ReinvokesTheFactoryOnTheNextAdmission()
    {
        var transport = Create();
        using var handler = new RecordingRawReliableHandshakeServerHandler(Memory);
        int calls = 0;
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory(
            _ => ++calls == 1 ? null : handler);
        InitializeAndStart(transport, factory);

        Assert.That(transport.ExposedTryCreateHandler(Remote), Is.Null);
        Assert.That(factory.TryCreateCalls, Is.EqualTo(1));

        IRawReliableServerHandler? second = transport.ExposedTryCreateHandler(Remote);

        Assert.That(second, Is.TypeOf<StopOnceServerHandler>());
        Assert.That(((StopOnceServerHandler)second!).Inner, Is.SameAs(handler));
        Assert.That(factory.TryCreateCalls, Is.EqualTo(2),
            "a null factory result must not be cached");
    }

    [Test]
    public void TryCreateHandler_AfterThrow_ReinvokesTheFactoryOnTheNextAdmission()
    {
        var transport = Create();
        using var handler = new RecordingRawReliableHandshakeServerHandler(Memory);
        bool first = true;
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory(
            _ =>
            {
                if (first)
                {
                    first = false;
                    throw new InvalidOperationException("factory failure");
                }

                return handler;
            });
        InitializeAndStart(transport, factory);

        Assert.That(transport.ExposedTryCreateHandler(Remote), Is.Null);
        Assert.That(factory.TryCreateCalls, Is.EqualTo(1));

        IRawReliableServerHandler? second = transport.ExposedTryCreateHandler(Remote);

        Assert.That(second, Is.TypeOf<StopOnceServerHandler>());
        Assert.That(((StopOnceServerHandler)second!).Inner, Is.SameAs(handler));
        Assert.That(factory.TryCreateCalls, Is.EqualTo(2),
            "a throwing factory must remain eligible for a later attempt");
    }

    [Test]
    public void TryCreateHandler_InvokesFactoryWithRemote()
    {
        var transport = Create();
        using var handler = new RecordingRawReliableHandshakeServerHandler(Memory);
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory { Handler = handler };
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
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory(remote =>
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

    [Test]
    [Category("Transports.Concurrency")]
    public void TryCreateHandler_ConcurrentDistinctSources_SerializeFactoryCalls()
    {
        var transport = Create(maxConcurrentHandshakes: 4, maxActiveSessions: 4);
        using var ready = new CountdownEvent(2);
        using var startGate = new ManualResetEventSlim(false);
        using var factoryEntered = new ManualResetEventSlim(false);
        using var releaseFactory = new ManualResetEventSlim(false);
        int active = 0;
        int maxActive = 0;
        Exception? threadFailure = null;
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory(_ =>
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

        var secondRemote = new StringEndPoint("remote-2");

        Thread Contender(IEndPoint remote) => new(() =>
        {
            ready.Signal();
            startGate.Wait();

            try
            {
                transport.ExposedTryCreateHandler(remote);
            }
            catch (Exception ex)
            {
                threadFailure = ex;
            }
        });

        var first = Contender(Remote);
        var second = Contender(secondRemote);
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
        Assert.That(maxActive, Is.EqualTo(1),
            "the factory lock is global, not per source route");
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
        using var recording = new RecordingRawReliableHandshakeServerHandler(Memory);
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory { Handler = recording };
        InitializeAndStart(transport, factory);
        Assert.That(transport.ExposedTryBeginAdmission(), Is.True);
        IRawReliableServerHandler? handler = transport.ExposedTryCreateHandler(Remote);
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
        using var recording = new RecordingRawReliableHandshakeServerHandler(Memory);
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory { Handler = recording };
        InitializeAndStart(transport, factory);
        Assert.That(transport.ExposedTryBeginAdmission(), Is.True);
        IRawReliableServerHandler handler = transport.ExposedTryCreateHandler(Remote)!;
        Assert.That(handler, Is.Not.Null);
        var session = new TestRawReliableServerSession(handler);
        session.Queue.RejectNextSchedule = true;

        Assert.That(
            transport.ExposedCompleteAdmission(
                session, handler, new TextFail(transport.Name, "Failed to start or register a session")),
            Is.False);

        Assert.That(recording.StoppedCount, Is.EqualTo(1));

        session.DrainAll();

        Assert.That(recording.StoppedCount, Is.EqualTo(1));
        Assert.That(transport.ExposedActiveSessions, Is.Zero);

        // A second terminal notification through the same gate is ignored, which is the protocol
        // delta this layer owns.
        handler.OnStopped(Reason("second"));

        Assert.That(recording.StoppedCount, Is.EqualTo(1));
    }

    [Test]
    public void CompleteAdmission_WhenTransportStopping_DoesNotPreemptStartedSession()
    {
        var transport = Create();
        using var recording = new RecordingRawReliableHandshakeServerHandler(Memory);
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory { Handler = recording };
        InitializeAndStart(transport, factory);
        Assert.That(transport.ExposedTryBeginAdmission(), Is.True);
        IRawReliableServerHandler handler = transport.ExposedTryCreateHandler(Remote)!;
        Assert.That(handler, Is.Not.Null);
        var session = new TestRawReliableServerSession(handler);

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

        // A second terminal notification through the same gate is ignored, which is the protocol
        // delta this layer owns.
        handler.OnStopped(Reason("second"));

        Assert.That(recording.StoppedCount, Is.EqualTo(1));
    }

    #endregion
}
