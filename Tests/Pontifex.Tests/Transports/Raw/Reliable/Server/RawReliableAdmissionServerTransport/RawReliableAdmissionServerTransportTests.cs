using System;
using System.Text;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Tests.Endpoints;
using Pontifex.Utils;
using Scriba;

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
        int tickPeriodMs = 100,
        int messageMaxByteSize = 1024,
        ILogger? logger = null)
    {
        return new TestRawReliableAdmissionServerTransport(
            maxConcurrentHandshakes: maxConcurrentHandshakes,
            maxActiveSessions: maxActiveSessions,
            tickPeriodMs: tickPeriodMs,
            messageMaxByteSize: messageMaxByteSize,
            logger: logger);
    }

    private static UserIntention Reason(string source = "test")
    {
        return new UserIntention(source);
    }

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    private static UnionDataList Request(short version, string name, int messageMaxByteSize)
    {
        UnionDataList request = TestBuffers.Acquire(Memory);
        request.PutLast(new UnionData(version));
        request.PutLast(new UnionData(new MultiRefByteArray(Encoding.UTF8.GetBytes(name))));
        request.PutLast(new UnionData(messageMaxByteSize));
        return request;
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

    #region Constructor

    [Test]
    public void Constructor_WithNullHandshakeName_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new TestRawReliableAdmissionServerTransport(handshakeName: null!));

        Assert.That(exception!.ParamName, Is.EqualTo("handshakeName"));
    }

    [Test]
    public void Constructor_WithNonPositiveHandshakeMaxByteSize_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new TestRawReliableAdmissionServerTransport(handshakeMaxByteSize: 0));

        Assert.That(exception!.ParamName, Is.EqualTo("handshakeMaxByteSize"));
    }

    [Test]
    public void Constructor_WithNonPositiveHandshakeTimeout_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new TestRawReliableAdmissionServerTransport(handshakeTimeout: TimeSpan.Zero));

        Assert.That(exception!.ParamName, Is.EqualTo("handshakeTimeout"));
    }

    [Test]
    public void Constructor_WithInfiniteHandshakeTimeout_AcceptsValue()
    {
        var transport = new TestRawReliableAdmissionServerTransport(
            handshakeTimeout: Timeout.InfiniteTimeSpan);

        Assert.That(transport.ExposedHandshakeTimeout, Is.EqualTo(Timeout.InfiniteTimeSpan));
    }

    [Test]
    public void Constructor_WithNegativeRejectLinger_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new TestRawReliableAdmissionServerTransport(
                rejectLinger: TimeSpan.FromMilliseconds(-1)));

        Assert.That(exception!.ParamName, Is.EqualTo("rejectLinger"));
    }

    [Test]
    public void Constructor_WithDefaults_UsesDocumentedHandshakeDefaults()
    {
        var transport = Create();

        Assert.That(transport.ExposedHandshakeName, Is.EqualTo("test-handshake"));
        Assert.That(transport.ExposedHandshakeMaxByteSize,
            Is.EqualTo(RawReliableHandshake.DefaultMaxByteSize));
        Assert.That(transport.ExposedHandshakeTimeout, Is.EqualTo(TimeSpan.FromSeconds(10)));
        Assert.That(transport.ExposedRejectLinger, Is.EqualTo(TimeSpan.FromSeconds(2)));
        Assert.That(transport.ExposedHandshakeTimerFactory, Is.Not.Null);
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

    #region TryHandshake

    [Test]
    public void TryHandshake_BeforeStart_ReturnsRejectedWithoutCallingFactory()
    {
        var transport = Create();
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory();
        Assert.That(transport.Init(factory), Is.True);
        UnionDataList request = RawReliableHandshake.BuildRequest("test-handshake", 1024);

        HandshakeOutcome outcome;
        try
        {
            outcome = transport.ExposedTryHandshake(Remote, request);
        }
        finally
        {
            request.Release();
        }

        Assert.That(outcome.Status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.Rejected));
        Assert.That(outcome.IsAccepted, Is.False);
        Assert.That(outcome.Handler, Is.Null);
        Assert.That(outcome.Response, Is.Null);
        Assert.That(factory.TryCreateCalls, Is.Zero);
    }

    [Test]
    public void TryHandshake_AfterStop_ReturnsRejectedWithoutCallingFactory()
    {
        var transport = Create();
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory();
        InitializeAndStart(transport, factory);
        Assert.That(transport.Stop(), Is.True);
        UnionDataList request = RawReliableHandshake.BuildRequest("test-handshake", 1024);

        HandshakeOutcome outcome;
        try
        {
            outcome = transport.ExposedTryHandshake(Remote, request);
        }
        finally
        {
            request.Release();
        }

        Assert.That(outcome.Status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.Rejected));
        Assert.That(outcome.Response, Is.Null);
        Assert.That(factory.TryCreateCalls, Is.Zero);
    }

    [Test]
    public void TryHandshake_WithEmptyRequest_ReturnsRejectedWithoutCallingFactory()
    {
        var transport = Create();
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory();
        InitializeAndStart(transport, factory);
        UnionDataList request = TestBuffers.Acquire(Memory);

        HandshakeOutcome outcome;
        try
        {
            outcome = transport.ExposedTryHandshake(Remote, request);
        }
        finally
        {
            request.Release();
        }

        Assert.That(outcome.Status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.Rejected));
        Assert.That(factory.TryCreateCalls, Is.Zero);
    }

    [Test]
    public void TryHandshake_WithWrongElementCount_ReturnsRejectedWithoutCallingFactory()
    {
        var transport = Create();
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory();
        InitializeAndStart(transport, factory);
        UnionDataList request = TestBuffers.Acquire(Memory);
        request.PutLast(new UnionData(RawReliableHandshake.Version));
        request.PutLast(new UnionData((short)1));

        HandshakeOutcome outcome;
        try
        {
            outcome = transport.ExposedTryHandshake(Remote, request);
        }
        finally
        {
            request.Release();
        }

        Assert.That(outcome.Status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.Rejected));
        Assert.That(factory.TryCreateCalls, Is.Zero);
    }

    [Test]
    public void TryHandshake_WithWrongElementTypes_ReturnsRejectedWithoutCallingFactory()
    {
        var transport = Create();
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory();
        InitializeAndStart(transport, factory);
        UnionDataList request = TestBuffers.Acquire(Memory);
        request.PutLast(new UnionData(RawReliableHandshake.Version));
        request.PutLast(new UnionData(1024));
        request.PutLast(new UnionData(1024));

        HandshakeOutcome outcome;
        try
        {
            outcome = transport.ExposedTryHandshake(Remote, request);
        }
        finally
        {
            request.Release();
        }

        Assert.That(outcome.Status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.Rejected));
        Assert.That(factory.TryCreateCalls, Is.Zero);
    }

    [Test]
    public void TryHandshake_WithVersionMismatch_ReturnsVersionMismatch()
    {
        var transport = Create();
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory();
        InitializeAndStart(transport, factory);
        UnionDataList request = Request(
            (short)(RawReliableHandshake.Version + 1), "test-handshake", 1024);

        HandshakeOutcome outcome;
        try
        {
            outcome = transport.ExposedTryHandshake(Remote, request);
        }
        finally
        {
            request.Release();
        }

        Assert.That(outcome.Status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.VersionMismatch));
        Assert.That(factory.TryCreateCalls, Is.Zero);
    }

    [Test]
    public void TryHandshake_WithNameMismatch_ReturnsNameMismatch()
    {
        var transport = Create();
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory();
        InitializeAndStart(transport, factory);
        UnionDataList request = RawReliableHandshake.BuildRequest("other-handshake", 1024);

        HandshakeOutcome outcome;
        try
        {
            outcome = transport.ExposedTryHandshake(Remote, request);
        }
        finally
        {
            request.Release();
        }

        Assert.That(outcome.Status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.NameMismatch));
        Assert.That(factory.TryCreateCalls, Is.Zero);
    }

    [Test]
    public void TryHandshake_WithRequestMaxBelowOne_ReturnsCapacityExceeded()
    {
        var transport = Create();
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory();
        InitializeAndStart(transport, factory);
        UnionDataList request = Request(RawReliableHandshake.Version, "test-handshake", 0);

        HandshakeOutcome outcome;
        try
        {
            outcome = transport.ExposedTryHandshake(Remote, request);
        }
        finally
        {
            request.Release();
        }

        Assert.That(outcome.Status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.CapacityExceeded));
        Assert.That(factory.TryCreateCalls, Is.Zero);
    }

    [Test]
    public void TryHandshake_WithRequestMaxBelowHandshakeMax_ReturnsCapacityExceeded()
    {
        var transport = Create();
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory();
        InitializeAndStart(transport, factory);
        UnionDataList request = RawReliableHandshake.BuildRequest(
            "test-handshake", RawReliableHandshake.DefaultMaxByteSize - 1);

        HandshakeOutcome outcome;
        try
        {
            outcome = transport.ExposedTryHandshake(Remote, request);
        }
        finally
        {
            request.Release();
        }

        Assert.That(outcome.Status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.CapacityExceeded));
        Assert.That(factory.TryCreateCalls, Is.Zero);
    }

    [Test]
    public void TryHandshake_WithoutFactory_ReturnsRejected()
    {
        var transport = Create();
        Assert.That(transport.ExposedTryMarkInitialized(), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);
        UnionDataList request = RawReliableHandshake.BuildRequest("test-handshake", 1024);

        HandshakeOutcome outcome;
        try
        {
            outcome = transport.ExposedTryHandshake(Remote, request);
        }
        finally
        {
            request.Release();
        }

        Assert.That(outcome.Status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.Rejected));
        Assert.That(outcome.Handler, Is.Null);
        Assert.That(outcome.Response, Is.Null);
    }

    [Test]
    public void TryHandshake_WhenFactoryReturnsNull_ReturnsRejected()
    {
        var transport = Create();
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory { Handler = null };
        InitializeAndStart(transport, factory);
        UnionDataList request = RawReliableHandshake.BuildRequest("test-handshake", 1024);

        HandshakeOutcome outcome;
        try
        {
            outcome = transport.ExposedTryHandshake(Remote, request);
        }
        finally
        {
            request.Release();
        }

        Assert.That(outcome.Status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.Rejected));
        Assert.That(outcome.Handler, Is.Null);
        Assert.That(outcome.Response, Is.Null);
        Assert.That(factory.TryCreateCalls, Is.EqualTo(1));
    }

    [Test]
    public void TryHandshake_WhenFactoryThrows_ReturnsRejectedAndLogs()
    {
        var logConsumer = new RecordingLogConsumer();
        var transport = Create(logger: new Logger([logConsumer]));
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory
        {
            ThrowOnTryCreate = true,
            TryCreateException = new InvalidOperationException("handshake factory failure")
        };
        InitializeAndStart(transport, factory);
        UnionDataList request = RawReliableHandshake.BuildRequest("test-handshake", 1024);

        HandshakeOutcome outcome;
        try
        {
            outcome = transport.ExposedTryHandshake(Remote, request);
        }
        finally
        {
            request.Release();
        }

        Assert.That(outcome.Status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.Rejected));
        Assert.That(outcome.Handler, Is.Null);
        Assert.That(outcome.Response, Is.Null);
        Assert.That(factory.TryCreateCalls, Is.EqualTo(1));
        Assert.That(logConsumer.Messages, Has.Some.Contains("handshake factory failure"));
    }

    [Test]
    public void TryHandshake_WhenRequestMaxIsBelowTransportMax_AcceptsWithRequestMax()
    {
        var transport = Create();
        using var handler = new RecordingRawReliableHandshakeServerHandler(Memory);
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory { Handler = handler };
        InitializeAndStart(transport, factory);
        UnionDataList request = RawReliableHandshake.BuildRequest("test-handshake", 256);
        HandshakeOutcome outcome;

        try
        {
            outcome = transport.ExposedTryHandshake(Remote, request);
        }
        finally
        {
            request.Release();
        }

        Assert.That(outcome.Status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.Accepted));
        Assert.That(outcome.IsAccepted, Is.True);
        Assert.That(outcome.Handler, Is.TypeOf<StopOnceServerHandler>());
        Assert.That(((StopOnceServerHandler)outcome.Handler!).Inner, Is.SameAs(handler));
        Assert.That(outcome.AgreedMaxByteSize, Is.EqualTo(256));
        Assert.That(outcome.Response, Is.Not.Null);

        try
        {
            Assert.That(
                RawReliableHandshake.TryParseResponse(
                    outcome.Response!, out short version, out string name, out int max),
                Is.True);
            Assert.That(version, Is.EqualTo(RawReliableHandshake.Version));
            Assert.That(name, Is.EqualTo("test-handshake"));
            Assert.That(max, Is.EqualTo(256));
        }
        finally
        {
            outcome.Response!.Release();
        }
    }

    [Test]
    public void TryHandshake_WhenRequestMaxExceedsTransportMax_AcceptsWithTransportMax()
    {
        var transport = Create(messageMaxByteSize: 1024);
        using var handler = new RecordingRawReliableHandshakeServerHandler(Memory);
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory { Handler = handler };
        InitializeAndStart(transport, factory);
        UnionDataList request = RawReliableHandshake.BuildRequest("test-handshake", 2048);
        HandshakeOutcome outcome;

        try
        {
            outcome = transport.ExposedTryHandshake(Remote, request);
        }
        finally
        {
            request.Release();
        }

        Assert.That(outcome.Status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.Accepted));
        Assert.That(outcome.AgreedMaxByteSize, Is.EqualTo(transport.MessageMaxByteSize));
        Assert.That(outcome.Response, Is.Not.Null);

        try
        {
            Assert.That(
                RawReliableHandshake.TryParseResponse(
                    outcome.Response!, out short version, out string name, out int max),
                Is.True);
            Assert.That(version, Is.EqualTo(RawReliableHandshake.Version));
            Assert.That(name, Is.EqualTo("test-handshake"));
            Assert.That(max, Is.EqualTo(transport.MessageMaxByteSize));
        }
        finally
        {
            outcome.Response!.Release();
        }
    }

    [Test]
    public void TryHandshake_WhenResponseBuildFails_RejectsAndStopsCreatedHandlerOnce()
    {
        var transport = new TestRawReliableAdmissionServerTransport(messageMaxByteSize: 0);
        using var handler = new RecordingRawReliableHandshakeServerHandler(Memory);
        var factory = new RecordingRawReliableAdmissionServerHandlerFactory { Handler = handler };
        InitializeAndStart(transport, factory);
        UnionDataList request = RawReliableHandshake.BuildRequest(
            "test-handshake", RawReliableHandshake.DefaultMaxByteSize);

        HandshakeOutcome outcome;
        try
        {
            outcome = transport.ExposedTryHandshake(Remote, request);
        }
        finally
        {
            request.Release();
        }

        Assert.That(outcome.Status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.Rejected));
        Assert.That(outcome.IsAccepted, Is.False);
        Assert.That(outcome.Handler, Is.Null);
        Assert.That(outcome.Response, Is.Null);
        Assert.That(factory.TryCreateCalls, Is.EqualTo(1));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<ExceptionFail>());
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
