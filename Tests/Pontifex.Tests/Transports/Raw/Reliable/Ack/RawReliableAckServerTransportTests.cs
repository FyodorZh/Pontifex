using System;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.StopReasons;
using Pontifex.Tests.Endpoints;
using Pontifex.Utils;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Black-box contract tests for <see cref="RawReliableAckServerTransport"/>, exercised through the
/// extended public + protected surface exposed by <see cref="TestRawReliableAckServerTransport"/>.
/// Covers the acknowledger initialization lifecycle, the acknowledgement hand-off, and the transport
/// contract. The inherited reliable-server session and ticker behavior is covered by
/// <see cref="RawReliableServerTransportTests"/> and is not repeated here.
/// </summary>
[Category("Transports")]
public sealed class RawReliableAckServerTransportTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    private static readonly IEndPoint Remote = new StringEndPoint("remote");

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    private static TestRawReliableAckServerTransport Create(
        int maxConcurrentHandshakes = 2,
        int maxActiveSessions = 2,
        int tickPeriodMs = 100)
    {
        return new TestRawReliableAckServerTransport(
            maxConcurrentHandshakes: maxConcurrentHandshakes,
            maxActiveSessions: maxActiveSessions,
            tickPeriodMs: tickPeriodMs);
    }

    private static UserIntention Reason(string source = "test")
    {
        return new UserIntention(source);
    }

    private static UnionDataList CreateData(int value)
    {
        var data = Memory.CollectablePool.Acquire<UnionDataList>();
        data.PutLast(new UnionData(value));
        return data;
    }

    private static RecordingRawReliableAckServerAcknowledger CreateAcknowledger(
        Func<IEndPoint, UnionDataList, IRawReliableAckServerHandler?>? behavior = null)
    {
        return new RecordingRawReliableAckServerAcknowledger(
            behavior ?? (static (_, data) =>
            {
                data.Release();
                return null;
            }));
    }

    private static void InitializeAndStart(
        TestRawReliableAckServerTransport transport,
        RecordingRawReliableAckServerAcknowledger acknowledger)
    {
        Assert.That(transport.Init(acknowledger), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);
    }

    #region Interface and type

    [Test]
    public void AckServerTransport_ExposesReliableAckServerContract()
    {
        var transport = Create();

        Assert.That(transport, Is.InstanceOf<IRawReliableAckServer>());
        Assert.That(transport, Is.InstanceOf<IRawReliableServerTransport<IRawReliableAckServerHandler>>());
        Assert.That(transport, Is.InstanceOf<IRawReliableTransport>());
        Assert.That(transport, Is.InstanceOf<IRawTransport>());
        Assert.That(transport, Is.InstanceOf<ITransport>());
        Assert.That(transport.Type, Is.EqualTo(TransportType.RawReliableAck));
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
        Assert.That(transport.Init(CreateAcknowledger()), Is.True);
    }

    [Test]
    public void Init_FirstCallSucceedsSecondFailsAndKeepsFirstAcknowledger()
    {
        var transport = Create();
        using var firstHandler = new RecordingRawReliableAckServerHandler(Memory);
        using var secondHandler = new RecordingRawReliableAckServerHandler(Memory);
        var first = CreateAcknowledger((_, data) =>
        {
            data.Release();
            return firstHandler;
        });
        var second = CreateAcknowledger((_, data) =>
        {
            data.Release();
            return secondHandler;
        });

        Assert.That(transport.Init(first), Is.True);
        Assert.That(transport.Init(second), Is.False);
        Assert.That(transport.Start(_ => { }), Is.True);

        UnionDataList data = CreateData(1);
        using var dataDisposer = data.AsDisposable();

        Assert.That(transport.ExposedTryAcknowledge(Remote, data), Is.SameAs(firstHandler));
        Assert.That(first.TryAckCalls, Is.EqualTo(1));
        Assert.That(second.TryAckCalls, Is.Zero);
    }

    [Test]
    public void Init_AfterStartAttempt_ReturnsFalse()
    {
        var transport = Create();
        Assert.That(transport.Init(CreateAcknowledger()), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.Init(CreateAcknowledger()), Is.False);

        Assert.That(transport.ExposedHasStartBeenAttempted, Is.True);
        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
    }

    [Test]
    public void Init_AfterFailedStartBeforeInit_ReturnsFalse()
    {
        var transport = Create();

        Assert.That(transport.Start(_ => { }), Is.False);
        Assert.That(transport.IsValid, Is.False);

        Assert.That(transport.Init(CreateAcknowledger()), Is.False);
        Assert.That(transport.StartCarrierCalls, Is.Zero);
    }

    [Test]
    public void Init_AfterStopBeforeStart_ReturnsTrue()
    {
        var transport = Create();

        Assert.That(transport.Stop(Reason()), Is.True);
        Assert.That(transport.StopCarrierCalls, Is.Zero);

        Assert.That(transport.Init(CreateAcknowledger()), Is.True);
        Assert.That(transport.IsValid, Is.True);
    }

    [Test]
    public void Init_AfterNormalStop_ReturnsFalse()
    {
        var transport = Create();
        Assert.That(transport.Init(CreateAcknowledger()), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);
        Assert.That(transport.Stop(Reason()), Is.True);

        Assert.That(transport.Init(CreateAcknowledger()), Is.False);

        Assert.That(transport.ExposedHasStartBeenAttempted, Is.True);
        Assert.That(transport.IsValid, Is.True);
    }

    [Test]
    public void Init_AfterFail_ReturnsFalse()
    {
        var transport = Create();
        transport.Fail(new TextFail(transport.Name, "failure"));

        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.Init(CreateAcknowledger()), Is.False);
    }

    [Test]
    public void Init_WhenAlreadyMarkedInitialized_DoesNotInstallAcknowledger()
    {
        var transport = Create();
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        var acknowledger = CreateAcknowledger((_, data) =>
        {
            data.Release();
            return handler;
        });

        Assert.That(transport.ExposedTryMarkInitialized(), Is.True);
        Assert.That(transport.Init(acknowledger), Is.False);
        Assert.That(transport.Start(_ => { }), Is.True);

        UnionDataList data = CreateData(1);
        using var dataDisposer = data.AsDisposable();

        Assert.That(transport.ExposedTryAcknowledge(Remote, data), Is.Null);
        Assert.That(acknowledger.TryAckCalls, Is.Zero);
    }

    [Test]
    public void Init_ConcurrentCalls_ExactlyOneSucceeds()
    {
        var transport = Create();
        var acknowledgers = new RecordingRawReliableAckServerAcknowledger[20];
        for (int i = 0; i < acknowledgers.Length; i++)
        {
            acknowledgers[i] = CreateAcknowledger();
        }

        int successes = 0;
        Parallel.For(0, acknowledgers.Length, i =>
        {
            if (transport.Init(acknowledgers[i]))
            {
                Interlocked.Increment(ref successes);
            }
        });

        Assert.That(successes, Is.EqualTo(1));
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);

        UnionDataList data = CreateData(1);
        using var dataDisposer = data.AsDisposable();
        Assert.That(transport.ExposedTryAcknowledge(Remote, data), Is.Null);

        int totalCalls = 0;
        for (int i = 0; i < acknowledgers.Length; i++)
        {
            totalCalls += acknowledgers[i].TryAckCalls;
        }

        Assert.That(totalCalls, Is.EqualTo(1));
    }

    #endregion

    #region TryAcknowledge

    [Test]
    public void TryAcknowledge_WithoutInit_ReturnsNullAndLeavesDataOwnedByCaller()
    {
        var transport = Create();
        UnionDataList data = CreateData(1);
        using var dataDisposer = data.AsDisposable();

        Assert.That(transport.ExposedTryAcknowledge(Remote, data), Is.Null);

        Assert.That(data.IsAlive, Is.True);
        Assert.That(data.RefCount.RefCount, Is.EqualTo(1));
    }

    [Test]
    public void TryAcknowledge_WhenStarted_PassesSameDataWithExtraReferenceAndReturnsHandler()
    {
        var transport = Create();
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        int received = 0;
        var acknowledger = CreateAcknowledger((remote, data) =>
        {
            Assert.That(remote, Is.SameAs(Remote));
            Assert.That(data.TryPopFirst(out int value), Is.True);
            received = value;
            data.Release();
            return handler;
        });
        InitializeAndStart(transport, acknowledger);
        UnionDataList data = CreateData(42);
        using var dataDisposer = data.AsDisposable();

        IRawReliableAckServerHandler? result = transport.ExposedTryAcknowledge(Remote, data);

        Assert.That(result, Is.SameAs(handler));
        Assert.That(received, Is.EqualTo(42));
        Assert.That(acknowledger.TryAckCalls, Is.EqualTo(1));
        Assert.That(acknowledger.LastRemote, Is.SameAs(Remote));
        Assert.That(acknowledger.LastAckData, Is.SameAs(data));
        Assert.That(acknowledger.RefCountBefore, Is.EqualTo(2));
        Assert.That(acknowledger.ReleasedByBehavior, Is.True);
        Assert.That(acknowledger.RefCountAfterRelease, Is.EqualTo(1));
        Assert.That(data.IsAlive, Is.True);
    }

    [Test]
    public void TryAcknowledge_WhenAcknowledgerRejects_ReturnsNullAndKeepsCallerReference()
    {
        var transport = Create();
        var acknowledger = CreateAcknowledger((_, data) =>
        {
            data.Release();
            return null;
        });
        InitializeAndStart(transport, acknowledger);
        UnionDataList data = CreateData(1);
        using var dataDisposer = data.AsDisposable();

        Assert.That(transport.ExposedTryAcknowledge(Remote, data), Is.Null);

        Assert.That(acknowledger.TryAckCalls, Is.EqualTo(1));
        Assert.That(acknowledger.RefCountBefore, Is.EqualTo(2));
        Assert.That(acknowledger.ReleasedByBehavior, Is.True);
        Assert.That(acknowledger.RefCountAfterRelease, Is.EqualTo(1));
        Assert.That(data.IsAlive, Is.True);
    }

    [Test]
    public void TryAcknowledge_WhenAcknowledgerThrows_ReturnsNullAndDoesNotStopTransport()
    {
        var transport = Create();
        var acknowledger = CreateAcknowledger((_, data) =>
        {
            data.Release();
            throw new InvalidOperationException("ack failure");
        });
        InitializeAndStart(transport, acknowledger);
        UnionDataList data = CreateData(1);
        using var dataDisposer = data.AsDisposable();
        IRawReliableAckServerHandler? result = null;

        Assert.DoesNotThrow(() => result = transport.ExposedTryAcknowledge(Remote, data));

        Assert.That(result, Is.Null);
        Assert.That(acknowledger.TryAckCalls, Is.EqualTo(1));
        Assert.That(acknowledger.ReleasedByBehavior, Is.True);
        Assert.That(data.IsAlive, Is.True);
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsStarted, Is.True);
    }

    [Test]
    public void TryAcknowledge_BeforeStart_ReturnsNullAndDoesNotCallAcknowledger()
    {
        var transport = Create();
        var acknowledger = CreateAcknowledger();
        Assert.That(transport.Init(acknowledger), Is.True);
        UnionDataList data = CreateData(1);
        using var dataDisposer = data.AsDisposable();

        Assert.That(transport.ExposedTryAcknowledge(Remote, data), Is.Null);

        Assert.That(acknowledger.TryAckCalls, Is.Zero);
        Assert.That(data.IsAlive, Is.True);
        Assert.That(data.RefCount.RefCount, Is.EqualTo(1));
    }

    [Test]
    public void TryAcknowledge_AfterStop_ReturnsNullAndDoesNotCallAcknowledger()
    {
        var transport = Create();
        var acknowledger = CreateAcknowledger();
        InitializeAndStart(transport, acknowledger);
        Assert.That(transport.Stop(Reason()), Is.True);
        UnionDataList data = CreateData(1);
        using var dataDisposer = data.AsDisposable();

        Assert.That(transport.ExposedTryAcknowledge(Remote, data), Is.Null);

        Assert.That(acknowledger.TryAckCalls, Is.Zero);
        Assert.That(data.IsAlive, Is.True);
    }

    [Test]
    public void TryAcknowledge_AfterFail_ReturnsNullAndDoesNotCallAcknowledger()
    {
        var transport = Create();
        var acknowledger = CreateAcknowledger();
        InitializeAndStart(transport, acknowledger);
        transport.Fail(new TextFail(transport.Name, "failure"));
        UnionDataList data = CreateData(1);
        using var dataDisposer = data.AsDisposable();

        Assert.That(transport.ExposedTryAcknowledge(Remote, data), Is.Null);

        Assert.That(acknowledger.TryAckCalls, Is.Zero);
        Assert.That(data.IsAlive, Is.True);
    }

    #endregion
}
