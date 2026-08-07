using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Pontifex.Utils.CheckPointGate;

namespace Pontifex.Tests.Raw.Reliable.Ack;

/// <summary>
/// Carrier-independent conformance suite for the RawReliableAck transport
/// contract. Each implementation supplies a conformance adapter that creates
/// linked server-client topologies.
/// </summary>
public abstract class RawReliableAckConformanceTests
{
    protected static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(2);

    protected abstract IRawReliableAckConformanceAdapter CreateAdapter();

    // ── Helpers ──────────────────────────────────────────────────────────

    protected static TControl GetControl<TControl>(ITransport transport)
        where TControl : class, IControl
    {
        var controls = new List<IControl>();
        transport.GetControls(controls, control => control is TControl);
        return controls.OfType<TControl>().Single();
    }

    protected static TControl GetEndpointControl<TControl>(IRawReliableEndpoint endpoint)
        where TControl : class, IControl
    {
        var controls = new List<IControl>();
        endpoint.GetControls(controls, control => control is TControl);
        return controls.OfType<TControl>().Single();
    }

    protected static void Start(IRawReliableAckServer server, IRawReliableAckClient client)
    {
        Assert.That(server.Start(_ => { }), Is.True);
        StartClient(client);
    }

    protected static void StartClient(IRawReliableAckClient client)
    {
        Assert.That(client.Start(_ => { }), Is.True);
    }

    protected static IRawReliableEndpoint WaitForConnectedEndpoint(RawReliableAckTestClientHandler handler)
    {
        var deadline = DateTime.UtcNow.AddSeconds(DeliveryTimeout.TotalSeconds);
        while (handler.Endpoint == null)
        {
            if (DateTime.UtcNow >= deadline)
                Assert.Fail("Client OnConnected was not invoked within the delivery timeout.");
            Thread.Sleep(10);
        }
        return handler.Endpoint;
    }

    protected static IRawReliableEndpoint WaitForConnectedEndpoint(RawReliableAckTestServerHandler handler)
    {
        var deadline = DateTime.UtcNow.AddSeconds(DeliveryTimeout.TotalSeconds);
        while (handler.Endpoint == null)
        {
            if (DateTime.UtcNow >= deadline)
                Assert.Fail("Server OnConnected was not invoked within the delivery timeout.");
            Thread.Sleep(10);
        }
        return handler.Endpoint;
    }

    protected static void WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(DeliveryTimeout.TotalSeconds);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                Assert.Fail("Condition was not satisfied within the delivery timeout.");
            Thread.Sleep(10);
        }
    }

    protected static T WaitForHandler<T>(ConcurrentQueue<T> queue, int minimumCount)
    {
        var deadline = DateTime.UtcNow.AddSeconds(DeliveryTimeout.TotalSeconds);
        while (queue.Count < minimumCount)
        {
            if (DateTime.UtcNow >= deadline)
                Assert.Fail("Expected handler was not created within the delivery timeout.");
            Thread.Sleep(10);
        }
        return queue.ToArray()[minimumCount - 1];
    }

    protected static void SetMaximum(ref int target, int candidate)
    {
        while (true)
        {
            var current = Volatile.Read(ref target);
            if (current >= candidate || Interlocked.CompareExchange(ref target, candidate, current) == current)
                return;
        }
    }

    // ── Message factories ────────────────────────────────────────────────

    protected static UnionDataList CreateEmptyMessage(IRawTransport transport)
    {
        return transport.Memory.CollectablePool.Acquire<UnionDataList>();
    }

    protected static UnionDataList CreateMessage(IRawTransport transport, int value)
    {
        var message = CreateEmptyMessage(transport);
        message.PutLast(value);
        return message;
    }

    protected static UnionDataList CreateComplexMessage(IRawTransport transport)
    {
        var message = CreateEmptyMessage(transport);
        message.PutLast(42);
        message.PutLast(true);
        message.PutFirst("RawReliableAck");
        return message;
    }

    protected static UnionDataList CreateOversizedMessage(IRawTransport transport)
    {
        var message = CreateEmptyMessage(transport);
        var bytes = transport.Memory.ByteArraysPool.Acquire(transport.MessageMaxByteSize);
        message.PutLast(new UnionData(bytes));
        return message;
    }

    protected static UnionDataList CreateExactLimitMessage(IRawTransport transport, int limit)
    {
        var empty = CreateEmptyMessage(transport);
        if (empty.GetDataSize() == limit)
            return empty;
        empty.Release();

        for (var overhead = 1; overhead <= 8; overhead++)
        {
            var byteCount = limit - overhead;
            if (byteCount <= 0)
                continue;

            var message = CreateEmptyMessage(transport);
            var bytes = transport.Memory.ByteArraysPool.Acquire(byteCount);
            message.PutLast(new UnionData(bytes));
            if (message.GetDataSize() == limit)
                return message;
            message.Release();
        }

        throw new AssertionException($"Unable to construct a UnionDataList of exactly {limit} bytes.");
    }

    // ── Probes ───────────────────────────────────────────────────────────

    protected sealed class ConcurrencyProbe
    {
        private int _active;
        private int _concurrent;
        private int _completed;

        public ManualResetEventSlim Release { get; } = new();
        public TaskCompletionSource FirstEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ConcurrentCallbacks => Volatile.Read(ref _concurrent);

        public void Enter()
        {
            if (Interlocked.Increment(ref _active) > 1)
                Interlocked.Exchange(ref _concurrent, 1);
            FirstEntered.TrySetResult();
        }

        public void Exit()
        {
            Interlocked.Decrement(ref _active);
            if (Interlocked.Increment(ref _completed) == 2)
                AllCompleted.TrySetResult();
        }
    }

    protected sealed class ReturnedFlag
    {
        private int _value;
        public int Value
        {
            get => Volatile.Read(ref _value);
            set => Volatile.Write(ref _value, value);
        }
    }

    // ── Recording handlers ───────────────────────────────────────────────

    protected sealed class RecordingClientHandler : RawReliableAckTestClientHandler
    {
        private readonly List<int> _received = new();
        private readonly object _lock = new();
        private int _fillAckDataCalled;
        private int _onConnectedCalled;
        private int _onDisconnectedCalled;
        private int _onStoppedCalled;
        private bool _ackResponseReceived;

        public bool FillAckDataCalled => Volatile.Read(ref _fillAckDataCalled) != 0;
        public bool OnConnectedCalled => Volatile.Read(ref _onConnectedCalled) != 0;
        public bool OnDisconnectedCalled => Volatile.Read(ref _onDisconnectedCalled) != 0;
        public bool OnStoppedCalled => Volatile.Read(ref _onStoppedCalled) != 0;
        public bool AckResponseReceived => _ackResponseReceived;
        public int ReceivedCount { get { lock (_lock) return _received.Count; } }
        public IReadOnlyList<int> ReceivedValues { get { lock (_lock) return _received.ToArray(); } }

        public RecordingClientHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        public override void FillAckData(UnionDataList ackData)
        {
            Interlocked.Increment(ref _fillAckDataCalled);
        }

        protected override void OnConnectedCore(UnionDataList ackResponse)
        {
            Interlocked.Increment(ref _onConnectedCalled);
            _ackResponseReceived = true;
            ackResponse.Release();
        }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value)) { lock (_lock) _received.Add(value); }
            }
            finally { message.Release(); }
        }

        public override void OnDisconnected(StopReason reason)
        {
            Interlocked.Increment(ref _onDisconnectedCalled);
            base.OnDisconnected(reason);
        }

        public override void OnStopped(StopReason reason)
        {
            Interlocked.Increment(ref _onStoppedCalled);
            base.OnStopped(reason);
        }
    }

    protected sealed class RecordingServerHandler : RawReliableAckTestServerHandler
    {
        private readonly List<int> _received = new();
        private readonly object _lock = new();
        private int _onConnectedCalled;
        private int _onDisconnectedCalled;

        public bool OnConnectedCalled => Volatile.Read(ref _onConnectedCalled) != 0;
        public bool OnDisconnectedCalled => Volatile.Read(ref _onDisconnectedCalled) != 0;
        public int ReceivedCount { get { lock (_lock) return _received.Count; } }
        public IReadOnlyList<int> ReceivedValues { get { lock (_lock) return _received.ToArray(); } }

        public RecordingServerHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        protected override void OnConnectedCore()
        {
            Interlocked.Increment(ref _onConnectedCalled);
        }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value)) { lock (_lock) _received.Add(value); }
            }
            finally { message.Release(); }
        }

        public override void OnDisconnected(StopReason reason)
        {
            Interlocked.Increment(ref _onDisconnectedCalled);
            base.OnDisconnected(reason);
        }
    }

    // ── Specialised recording / observer handlers ────────────────────────

    protected sealed class SizeRecordingServerHandler : RawReliableAckTestServerHandler
    {
        private readonly object _lock = new();

        public int? LastSize { get; private set; }
        public TaskCompletionSource Received { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SizeRecordingServerHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        public override void OnReceived(UnionDataList message)
        {
            try { lock (_lock) { LastSize = message.GetDataSize(); } }
            finally { message.Release(); }
            Received.TrySetResult();
        }
    }

    protected sealed class OnConnectedOrderClientHandler : RawReliableAckTestClientHandler
    {
        private readonly List<int> _received = new();
        private readonly object _lock = new();

        public bool OnConnectedCompleted { get; private set; }
        public bool SawConnectedCompleted { get; private set; }
        public int ReceivedCount { get { lock (_lock) return _received.Count; } }
        public IReadOnlyList<int> ReceivedValues { get { lock (_lock) return _received.ToArray(); } }

        public OnConnectedOrderClientHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        protected override void OnConnectedCore(UnionDataList ackResponse)
        {
            OnConnectedCompleted = true;
            ackResponse.Release();
        }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                SawConnectedCompleted = OnConnectedCompleted;
                if (message.TryPopFirst(out int value)) { lock (_lock) _received.Add(value); }
            }
            finally { message.Release(); }
        }
    }

    protected sealed class OnConnectedFlagServerHandler : RawReliableAckTestServerHandler
    {
        private readonly List<int> _received = new();
        private readonly object _lock = new();
        private int _onConnectedRan;

        public bool OnConnectedRan => Volatile.Read(ref _onConnectedRan) != 0;
        public int ReceivedCount { get { lock (_lock) return _received.Count; } }
        public IReadOnlyList<int> ReceivedValues { get { lock (_lock) return _received.ToArray(); } }

        public OnConnectedFlagServerHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        protected override void OnConnectedCore()
        {
            Volatile.Write(ref _onConnectedRan, 1);
        }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value)) { lock (_lock) _received.Add(value); }
            }
            finally { message.Release(); }
        }
    }

    protected sealed class OnDisconnectedCountingClientHandler : RawReliableAckTestClientHandler
    {
        private int _onDisconnectedCount;

        public int OnDisconnectedCount => Volatile.Read(ref _onDisconnectedCount);

        public OnDisconnectedCountingClientHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        public override void OnReceived(UnionDataList message)
        {
            message.Release();
        }

        public override void OnDisconnected(StopReason reason)
        {
            Interlocked.Increment(ref _onDisconnectedCount);
            base.OnDisconnected(reason);
        }
    }

    protected sealed class OnStoppedCountingClientHandler : RawReliableAckTestClientHandler
    {
        private int _onStoppedCount;

        public int OnStoppedCount => Volatile.Read(ref _onStoppedCount);

        public OnStoppedCountingClientHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        public override void OnReceived(UnionDataList message)
        {
            message.Release();
        }

        public override void OnStopped(StopReason reason)
        {
            Interlocked.Increment(ref _onStoppedCount);
            base.OnStopped(reason);
        }
    }

    protected sealed class SessionRecordingServerHandler : RawReliableAckTestServerHandler
    {
        private readonly List<int> _received = new();
        private readonly object _lock = new();
        private int _onDisconnectedCount;

        public int ReceivedCount { get { lock (_lock) return _received.Count; } }
        public int OnDisconnectedCount => Volatile.Read(ref _onDisconnectedCount);

        public SessionRecordingServerHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value)) { lock (_lock) _received.Add(value); }
            }
            finally { message.Release(); }
        }

        public override void OnDisconnected(StopReason reason)
        {
            Interlocked.Increment(ref _onDisconnectedCount);
            base.OnDisconnected(reason);
        }
    }

    protected sealed class AckResponseRecordingClientHandler : RawReliableAckTestClientHandler
    {
        private int _ackResponseValue;

        public int AckResponseValue => Volatile.Read(ref _ackResponseValue);

        public AckResponseRecordingClientHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        protected override void OnConnectedCore(UnionDataList ackResponse)
        {
            try
            {
                if (ackResponse.TryPopFirst(out int value))
                    Volatile.Write(ref _ackResponseValue, value);
            }
            finally { ackResponse.Release(); }
        }

        public override void OnReceived(UnionDataList message)
        {
            message.Release();
        }
    }

    protected sealed class SourceCheckingServerHandler : RawReliableAckTestServerHandler
    {
        private readonly List<int> _received = new();
        private readonly object _lock = new();

        public bool OnConnectedRan { get; private set; }
        public bool? OnConnectedRemoteIsNonNull { get; private set; }
        public int ReceivedCount { get { lock (_lock) return _received.Count; } }
        public IReadOnlyList<int> ReceivedValues { get { lock (_lock) return _received.ToArray(); } }

        public SourceCheckingServerHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        protected override void OnConnectedCore()
        {
            OnConnectedRan = true;
            OnConnectedRemoteIsNonNull = Endpoint!.RemoteEndPoint != null;
        }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value)) { lock (_lock) _received.Add(value); }
            }
            finally { message.Release(); }
        }
    }

    // ── Oversized-ACK handlers ───────────────────────────────────────────

    protected sealed class OversizedFillAckDataClientHandler : RawReliableAckTestClientHandler
    {
        private readonly IRawTransport _transport;

        public OversizedFillAckDataClientHandler(
            IRawTransport transport,
            IRawReliableAckEndpointTracker? tracker = null)
            : base(tracker)
        {
            _transport = transport;
        }

        public override void FillAckData(UnionDataList ackData)
        {
            var bytes = _transport.Memory.ByteArraysPool.Acquire(
                _transport.MessageMaxByteSize);
            ackData.PutLast(new UnionData(bytes));
        }

        public override void OnReceived(UnionDataList message)
        {
            message.Release();
        }
    }

    protected sealed class OversizedFillAckResponseServerHandler : RawReliableAckTestServerHandler
    {
        private readonly IRawTransport _transport;

        public OversizedFillAckResponseServerHandler(
            IRawTransport transport,
            IRawReliableAckEndpointTracker? tracker = null)
            : base(tracker)
        {
            _transport = transport;
        }

        public override void FillAckResponse(UnionDataList ackData)
        {
            var bytes = _transport.Memory.ByteArraysPool.Acquire(
                _transport.MessageMaxByteSize);
            ackData.PutLast(new UnionData(bytes));
        }

        public override void OnReceived(UnionDataList message)
        {
            message.Release();
        }
    }

    // ── Throwing handlers ────────────────────────────────────────────────

    protected sealed class ThrowingFillAckDataClientHandler : RawReliableAckTestClientHandler
    {
        public ThrowingFillAckDataClientHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        public override void FillAckData(UnionDataList ackData)
        {
            throw new InvalidOperationException("Expected conformance test exception in FillAckData.");
        }

        public override void OnReceived(UnionDataList message)
        {
            message.Release();
        }
    }

    protected sealed class ThrowingOnConnectedClientHandler : RawReliableAckTestClientHandler
    {
        public ThrowingOnConnectedClientHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        protected override void OnConnectedCore(UnionDataList ackResponse)
        {
            try
            {
                throw new InvalidOperationException("Expected conformance test exception in OnConnected.");
            }
            finally
            {
                ackResponse.Release();
            }
        }

        public override void OnReceived(UnionDataList message)
        {
            message.Release();
        }
    }

    protected sealed class ThrowingOnReceivedClientHandler : RawReliableAckTestClientHandler
    {
        private int _deliveredCount;

        public int DeliveredCount => Volatile.Read(ref _deliveredCount);

        public ThrowingOnReceivedClientHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value) && value == 1)
                    throw new InvalidOperationException("Expected conformance test exception.");
                Interlocked.Increment(ref _deliveredCount);
            }
            finally { message.Release(); }
        }
    }

    protected sealed class ThrowingFillAckResponseServerHandler : RawReliableAckTestServerHandler
    {
        public ThrowingFillAckResponseServerHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        public override void FillAckResponse(UnionDataList ackData)
        {
            throw new InvalidOperationException("Expected conformance test exception in FillAckResponse.");
        }

        public override void OnReceived(UnionDataList message)
        {
            message.Release();
        }
    }

    protected sealed class ThrowingOnConnectedServerHandler : RawReliableAckTestServerHandler
    {
        public ThrowingOnConnectedServerHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        protected override void OnConnectedCore()
        {
            throw new InvalidOperationException("Expected conformance test exception in OnConnected.");
        }

        public override void OnReceived(UnionDataList message)
        {
            message.Release();
        }
    }

    protected sealed class ThrowingOnReceivedServerHandler : RawReliableAckTestServerHandler
    {
        private int _deliveredCount;

        public int DeliveredCount => Volatile.Read(ref _deliveredCount);

        public ThrowingOnReceivedServerHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value) && value == 1)
                    throw new InvalidOperationException("Expected conformance test exception.");
                Interlocked.Increment(ref _deliveredCount);
            }
            finally { message.Release(); }
        }
    }

    // ── Blocking / serialization handlers ────────────────────────────────

    protected sealed class BlockingOnReceivedClientHandler : RawReliableAckTestClientHandler
    {
        private int _activeCallbacks;
        private int _concurrentCallbacks;
        private int _completedCallbacks;

        public ManualResetEventSlim Release { get; } = new();
        public TaskCompletionSource FirstEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ConcurrentCallbacks => Volatile.Read(ref _concurrentCallbacks);
        public int CompletedCallbacks => Volatile.Read(ref _completedCallbacks);

        public BlockingOnReceivedClientHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                if (Interlocked.Increment(ref _activeCallbacks) > 1)
                    Interlocked.Exchange(ref _concurrentCallbacks, 1);
                FirstEntered.TrySetResult();
                Release.Wait(DeliveryTimeout);
            }
            finally
            {
                Interlocked.Decrement(ref _activeCallbacks);
                message.Release();
                if (Interlocked.Increment(ref _completedCallbacks) == 2)
                    AllCompleted.TrySetResult();
            }
        }
    }

    protected sealed class BlockingOnReceivedServerHandler : RawReliableAckTestServerHandler
    {
        private readonly ConcurrencyProbe _probe;

        public BlockingOnReceivedServerHandler(
            IRawReliableAckEndpointTracker? tracker, ConcurrencyProbe probe) : base(tracker)
        {
            _probe = probe;
        }

        public override void OnReceived(UnionDataList message)
        {
            _probe.Enter();
            try { _probe.Release.Wait(DeliveryTimeout); }
            finally
            {
                message.Release();
                _probe.Exit();
            }
        }
    }

    protected sealed class BlockingPreReceiveServerHandler : RawReliableAckTestServerHandler
    {
        public volatile bool CallbackBegan;
        public ICheckPointCtl? Gate { get; private set; }
        public Task<CheckPointWaitResult>? Reached { get; private set; }

        public BlockingPreReceiveServerHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        protected override void OnConnectedCore()
        {
            var control = GetEndpointControl<IRawReliableAckEndpointConformanceControl>(Endpoint!);
            Gate = control.AfterReceivedGate;
            Reached = Gate.Arm();
        }

        public override void OnReceived(UnionDataList message)
        {
            CallbackBegan = true;
            message.Release();
        }
    }

    // ── Action handlers ──────────────────────────────────────────────────

    protected sealed class EchoServerHandler : RawReliableAckTestServerHandler
    {
        private readonly IRawReliableAckConformanceFixture _fixture;
        private readonly UnionDataList _expected;
        private readonly UnionDataList _response;
        private readonly TaskCompletionSource<bool> _serverReceived;

        public EchoServerHandler(
            IRawReliableAckConformanceFixture fixture,
            UnionDataList expected,
            UnionDataList response,
            TaskCompletionSource<bool> serverReceived,
            IRawReliableAckEndpointTracker? tracker = null)
            : base(tracker)
        {
            _fixture = fixture;
            _expected = expected;
            _response = response;
            _serverReceived = serverReceived;
        }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                _serverReceived.TrySetResult(message.EqualByContent(_expected));
                Assert.That(Endpoint!.Send(CreateMessage(_fixture.Server, 42)), Is.EqualTo(SendResult.Ok));
            }
            finally
            {
                message.Release();
                _expected.Release();
            }
        }
    }

    protected sealed class AssertContentClientHandler : RawReliableAckTestClientHandler
    {
        private readonly UnionDataList _expectedResponse;
        private readonly TaskCompletionSource<bool> _clientReceived;

        public AssertContentClientHandler(
            IRawReliableAckEndpointTracker? tracker,
            UnionDataList expectedResponse,
            TaskCompletionSource<bool> clientReceived)
            : base(tracker)
        {
            _expectedResponse = expectedResponse;
            _clientReceived = clientReceived;
        }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                _clientReceived.TrySetResult(message.EqualByContent(_expectedResponse));
            }
            finally
            {
                message.Release();
                _expectedResponse.Release();
            }
        }
    }

    protected sealed class ReplyAllServerHandler : RawReliableAckTestServerHandler
    {
        private const int ReplyCount = 8;
        private readonly IRawReliableAckConformanceFixture _fixture;

        public ReplyAllServerHandler(
            IRawReliableAckConformanceFixture fixture,
            IRawReliableAckEndpointTracker? tracker = null)
            : base(tracker)
        {
            _fixture = fixture;
        }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                for (var i = 0; i < ReplyCount; i++)
                    Assert.That(Endpoint!.Send(CreateMessage(_fixture.Server, i)), Is.EqualTo(SendResult.Ok));
            }
            finally { message.Release(); }
        }
    }

    protected sealed class ServerSendsValuesHandler : RawReliableAckTestServerHandler
    {
        private readonly IRawReliableAckConformanceFixture _fixture;
        private readonly int[] _values;

        public ServerSendsValuesHandler(
            IRawReliableAckConformanceFixture fixture,
            IRawReliableAckEndpointTracker? tracker,
            params int[] values)
            : base(tracker)
        {
            _fixture = fixture;
            _values = values;
        }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                foreach (var value in _values)
                    Assert.That(Endpoint!.Send(CreateMessage(_fixture.Server, value)), Is.EqualTo(SendResult.Ok));
            }
            finally { message.Release(); }
        }
    }

    protected sealed class EchoOnceClientHandler : RawReliableAckTestClientHandler
    {
        private readonly IRawReliableAckClient _client;
        private int _echoed;

        public EchoOnceClientHandler(
            IRawReliableAckClient client,
            IRawReliableAckEndpointTracker? tracker = null)
            : base(tracker)
        {
            _client = client;
        }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value) && Interlocked.Exchange(ref _echoed, 1) == 0)
                    Assert.That(Endpoint!.Send(CreateMessage(_client, value)), Is.EqualTo(SendResult.Ok));
            }
            finally { message.Release(); }
        }
    }

    protected sealed class StopFromReceiveServerHandler : RawReliableAckTestServerHandler
    {
        private readonly IRawReliableAckConformanceFixture _fixture;

        public StopFromReceiveServerHandler(
            IRawReliableAckConformanceFixture fixture,
            IRawReliableAckEndpointTracker? tracker = null)
            : base(tracker)
        {
            _fixture = fixture;
        }

        public override void OnReceived(UnionDataList message)
        {
            try { Assert.That(_fixture.Server.Stop(), Is.True); }
            finally { message.Release(); }
        }
    }

    protected sealed class DisconnectOnReceiveClientHandler : RawReliableAckTestClientHandler
    {
        private readonly List<int> _received = new();
        private readonly object _lock = new();
        private int _onDisconnectedCount;

        public int ReceivedCount { get { lock (_lock) return _received.Count; } }
        public IReadOnlyList<int> ReceivedValues { get { lock (_lock) return _received.ToArray(); } }
        public int OnDisconnectedCount => Volatile.Read(ref _onDisconnectedCount);

        public DisconnectOnReceiveClientHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value)) { lock (_lock) _received.Add(value); }
                Endpoint!.Disconnect(new UserIntention("test", "disconnect from receive"));
            }
            finally { message.Release(); }
        }

        public override void OnDisconnected(StopReason reason)
        {
            Interlocked.Increment(ref _onDisconnectedCount);
            base.OnDisconnected(reason);
        }
    }

    protected sealed class DisconnectOnReceiveServerHandler : RawReliableAckTestServerHandler
    {
        private readonly List<int> _received = new();
        private readonly object _lock = new();
        private int _onDisconnectedCount;

        public int ReceivedCount { get { lock (_lock) return _received.Count; } }
        public IReadOnlyList<int> ReceivedValues { get { lock (_lock) return _received.ToArray(); } }
        public int OnDisconnectedCount => Volatile.Read(ref _onDisconnectedCount);

        public DisconnectOnReceiveServerHandler(IRawReliableAckEndpointTracker? tracker = null) : base(tracker) { }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value)) { lock (_lock) _received.Add(value); }
                Endpoint!.Disconnect(new UserIntention("test", "disconnect from receive"));
            }
            finally { message.Release(); }
        }

        public override void OnDisconnected(StopReason reason)
        {
            Interlocked.Increment(ref _onDisconnectedCount);
            base.OnDisconnected(reason);
        }
    }

    // ── Send timing observer ─────────────────────────────────────────────

    protected sealed class SendReturnedObservingServerHandler : RawReliableAckTestServerHandler
    {
        private readonly ReturnedFlag _flag;

        public ReturnedFlag Flag => _flag;
        public TaskCompletionSource<bool> Observed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SendReturnedObservingServerHandler(
            ReturnedFlag flag,
            IRawReliableAckEndpointTracker? tracker = null)
            : base(tracker)
        {
            _flag = flag;
        }

        public override void OnReceived(UnionDataList message)
        {
            try { Observed.TrySetResult(_flag.Value == 1); }
            finally { message.Release(); }
        }
    }

    // ── Init lifecycle ─────────────────────────────────────────────────

    [Test]
    public void Init_NullArgs_ThrowsWithoutChangingLifecycle()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();

        Assert.Throws<ArgumentNullException>(() => client.Init(null!));
        Assert.Throws<ArgumentNullException>(() => fixture.InitServer(null!));
        Assert.Multiple(() =>
        {
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.False);
            Assert.That(fixture.Server.IsValid, Is.True);
            Assert.That(fixture.Server.IsStarted, Is.False);
        });

        Assert.That(client.Init(new RecordingClientHandler()), Is.True);
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return new RecordingServerHandler(); })), Is.True);
    }

    [Test]
    public void Init_IsOneTime_ReturnsFalse()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();

        Assert.That(client.Init(new RecordingClientHandler()), Is.True);
        Assert.That(client.Init(new RecordingClientHandler()), Is.False);
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return new RecordingServerHandler(); })), Is.True);
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return new RecordingServerHandler(); })), Is.False);
    }

    [Test]
    public void Init_AfterStart_ReturnsFalse()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(new RecordingClientHandler()), Is.True);
        Start(fixture.Server, client);

        Assert.Multiple(() =>
        {
            Assert.That(client.Init(new RecordingClientHandler()), Is.False);
            Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return new RecordingServerHandler(); })), Is.False);
        });
    }

    [Test]
    public void Start_BeforeInit_ReturnsFalseAndInvalidates()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();

        Assert.That(client.Start(_ => { }), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(client.IsValid, Is.False);
            Assert.That(client.Init(new RecordingClientHandler()), Is.False);
            Assert.That(client.Start(_ => { }), Is.False);
        });
    }

    // ── Transport lifecycle ─────────────────────────────────────────────

    [Test]
    public void Start_NullCallback_ThrowsWithoutChangingLifecycle()
    {
        using var fixture = CreateAdapter().CreateFixture();

        Assert.Throws<ArgumentNullException>(() => fixture.Server.Start(null!));
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsValid, Is.True);
            Assert.That(fixture.Server.IsStarted, Is.False);
        });
    }

    [Test]
    public void Start_IsOneTime_NormalStopRemainsValid()
    {
        using var fixture = CreateAdapter().CreateFixture();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return new RecordingServerHandler(); })), Is.True);

        Assert.That(fixture.Server.Start(_ => { }), Is.True);
        Assert.That(fixture.Server.Start(_ => { }), Is.False);
        Assert.That(fixture.Server.Stop(new UserIntention("test", "normal stop")), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsValid, Is.True);
            Assert.That(fixture.Server.IsStarted, Is.False);
            Assert.That(fixture.Server.Start(_ => { }), Is.False);
        });
    }

    [Test]
    public async Task Start_ConcurrentCalls_HasExactlyOneWinner()
    {
        using var fixture = CreateAdapter().CreateFixture();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return new RecordingServerHandler(); })), Is.True);

        var starts = Enumerable.Range(0, 8).Select(_ => Task.Run(() => fixture.Server.Start(_ => { })));
        var results = await Task.WhenAll(starts);

        Assert.That(results.Count(result => result), Is.EqualTo(1));
        Assert.That(fixture.Server.Stop(), Is.True);
    }

    [Test]
    public void FailNextStart_InvalidatesWithoutStoppedNotification()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var control = GetControl<IConformanceControl>(fixture.Server);
        var stopped = false;

        control.FailNextStart();

        Assert.That(fixture.Server.Start(_ => stopped = true), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsValid, Is.False);
            Assert.That(fixture.Server.IsStarted, Is.False);
            Assert.That(stopped, Is.False);
            Assert.That(fixture.Server.Stop(), Is.False);
        });
    }

    [Test]
    public async Task InjectUnrecoverableFailure_InvalidatesAndNotifies()
    {
        using var fixture = CreateAdapter().CreateFixture();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return new RecordingServerHandler(); })), Is.True);
        var control = GetControl<IConformanceControl>(fixture.Server);
        var stopped = new TaskCompletionSource<StopReason>(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.That(fixture.Server.Start(reason => stopped.TrySetResult(reason)), Is.True);
        control.InjectUnrecoverableFailure();

        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsValid, Is.False);
            Assert.That(fixture.Server.IsStarted, Is.False);
            Assert.That(fixture.Server.Stop(), Is.False);
        });
    }

    [Test]
    public void Stop_BeforeStart_IsNoOp_DoesNotConsumeStart()
    {
        using var fixture = CreateAdapter().CreateFixture();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return new RecordingServerHandler(); })), Is.True);

        Assert.That(fixture.Server.Stop(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsValid, Is.True);
            Assert.That(fixture.Server.IsStarted, Is.False);
        });
        Assert.That(fixture.Server.Start(_ => { }), Is.True);
    }

    [Test]
    public void Start_AfterStop_Fails()
    {
        using var fixture = CreateAdapter().CreateFixture();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return new RecordingServerHandler(); })), Is.True);

        Assert.That(fixture.Server.Start(_ => { }), Is.True);
        Assert.That(fixture.Server.Stop(), Is.True);
        Assert.That(fixture.Server.Start(_ => { }), Is.False);
    }

    // ── Client lifecycle ────────────────────────────────────────────────

    [Test]
    public void Client_SuccessfulConnection_CallbackSequence()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.Multiple(() =>
        {
            Assert.That(endpoint.IsConnected, Is.True);
            Assert.That(endpoint.RemoteEndPoint, Is.Not.Null);
        });
        Assert.That(endpoint.Send(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        WaitForConnectedEndpoint(serverHandler);
        Assert.That(serverHandler.OnConnectedCalled, Is.True);

        Assert.That(fixture.Server.Stop(new UserIntention("test", "stop")), Is.True);
        WaitUntil(() => clientHandler.OnDisconnectedCalled);
        WaitUntil(() => clientHandler.OnStoppedCalled);
        Assert.Multiple(() =>
        {
            Assert.That(clientHandler.FillAckDataCalled, Is.True);
            Assert.That(clientHandler.OnConnectedCalled, Is.True);
        });
    }

    [Test]
    public void Client_EndpointInOnConnected_IsUsable()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.Multiple(() =>
        {
            Assert.That(endpoint.IsConnected, Is.True);
            Assert.That(endpoint.RemoteEndPoint, Is.Not.Null);
        });
        Assert.That(endpoint.Send(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
    }

    [Test]
    public void Client_OnConnectedThrows_TerminatesWithExceptionFail()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var handler = new ThrowingOnConnectedClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(handler), Is.True);
        Start(fixture.Server, client);

        WaitUntil(() => handler.StoppedReason != null);
        Assert.Multiple(() =>
        {
            Assert.That(handler.StoppedReason, Is.InstanceOf<ExceptionFail>());
            Assert.That(handler.IsConnected, Is.False);
        });
    }

    [Test]
    public void Client_FillAckDataException_OnStoppedWithoutOnConnected()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var handler = new ThrowingFillAckDataClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(handler), Is.True);
        Start(fixture.Server, client);

        WaitUntil(() => handler.StoppedReason != null);
        Assert.That(handler.StoppedReason, Is.InstanceOf<ExceptionFail>());
    }

    [Test]
    public void Client_OversizedAckData_OnStoppedWithoutOnConnected()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var handler = new OversizedFillAckDataClientHandler(client);
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(handler), Is.True);
        Start(fixture.Server, client);

        WaitUntil(() => handler.StoppedReason != null);
        Assert.Multiple(() =>
        {
            Assert.That(handler.IsConnected, Is.False);
            Assert.That(handler.DisconnectReason, Is.Null);
        });
    }

    [Test]
    public async Task Client_StartWithoutServer_NoOnConnectedUntilServerAvailable()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        Assert.That(client.Init(clientHandler), Is.True);
        StartClient(client);

        await Task.Delay(200);
        Assert.That(clientHandler.OnConnectedCalled, Is.False);

        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return new RecordingServerHandler(); })), Is.True);
        Assert.That(fixture.Server.Start(_ => { }), Is.True);
        WaitForConnectedEndpoint(clientHandler);
    }

    // ── Server session ──────────────────────────────────────────────────

    [Test]
    public void Server_SuccessfulSession_OnConnectedThenOnDisconnected()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingServerHandler();
        var clientHandler = new RecordingClientHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        WaitForConnectedEndpoint(serverHandler);
        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.That(endpoint.Send(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        Assert.That(fixture.Server.Stop(), Is.True);
        WaitUntil(() => serverHandler.OnDisconnectedCalled);
    }

    [Test]
    public void Server_HandlerFreshness_EachSessionGetsDistinctHandler()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var handlers = new ConcurrentQueue<RecordingServerHandler>();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData =>
        {
            var handler = new RecordingServerHandler();
            handlers.Enqueue(handler);
            ackData.Release();
            return handler;
        })), Is.True);

        var firstClient = fixture.CreateClient();
        var firstClientHandler = new RecordingClientHandler();
        Assert.That(firstClient.Init(firstClientHandler), Is.True);
        var secondClient = fixture.CreateClient();
        var secondClientHandler = new RecordingClientHandler();
        Assert.That(secondClient.Init(secondClientHandler), Is.True);
        Start(fixture.Server, firstClient);
        Assert.That(secondClient.Start(_ => { }), Is.True);

        WaitForConnectedEndpoint(firstClientHandler);
        WaitForConnectedEndpoint(secondClientHandler);
        WaitForHandler(handlers, 2);
        var created = handlers.ToArray();
        Assert.That(created[0], Is.Not.SameAs(created[1]));
    }

    [Test]
    public async Task Server_SessionCleanup_OldHandlerGetsNoMoreCallbacks()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var firstHandler = new SessionRecordingServerHandler();
        var calls = 0;
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData =>
        {
            ackData.Release();
            return Interlocked.Increment(ref calls) == 1
                ? firstHandler
                : new SessionRecordingServerHandler();
        })), Is.True);

        var firstClient = fixture.CreateClient();
        var firstClientHandler = new RecordingClientHandler();
        Assert.That(firstClient.Init(firstClientHandler), Is.True);
        Start(fixture.Server, firstClient);

        var firstEndpoint = WaitForConnectedEndpoint(firstClientHandler);
        Assert.That(firstEndpoint.Send(CreateMessage(firstClient, 1)), Is.EqualTo(SendResult.Ok));
        WaitUntil(() => firstHandler.ReceivedCount == 1);
        Assert.That(firstEndpoint.Disconnect(new UserIntention("test", "d")), Is.True);
        WaitUntil(() => firstHandler.OnDisconnectedCount == 1);

        var secondClient = fixture.CreateClient();
        var secondClientHandler = new RecordingClientHandler();
        Assert.That(secondClient.Init(secondClientHandler), Is.True);
        Assert.That(secondClient.Start(_ => { }), Is.True);
        var secondEndpoint = WaitForConnectedEndpoint(secondClientHandler);
        Assert.That(secondEndpoint.Send(CreateMessage(secondClient, 2)), Is.EqualTo(SendResult.Ok));

        await Task.Delay(200);
        Assert.That(firstHandler.OnDisconnectedCount, Is.EqualTo(1));
    }

    // ── Ack handshake ───────────────────────────────────────────────────

    [Test]
    public void TryAck_ReceivesClientAckData()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new AckDataWritingClientHandler(99);
        var receivedValue = 0;
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData =>
        {
            if (ackData.TryPopFirst(out int value))
                receivedValue = value;
            ackData.Release();
            return new RecordingServerHandler();
        })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        WaitForConnectedEndpoint(clientHandler);
        Assert.That(receivedValue, Is.EqualTo(99));
    }

    [Test]
    public void TryAck_Rejection_NoSessionCreated()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return null; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        WaitUntil(() => clientHandler.StoppedReason != null);
        Assert.Multiple(() =>
        {
            Assert.That(clientHandler.OnConnectedCalled, Is.False);
            Assert.That(clientHandler.OnDisconnectedCalled, Is.False);
        });
    }

    [Test]
    public void TryAck_Exception_FailsConnectionWithoutStoppingTransport()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(_ => throw new InvalidOperationException("test"))), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        WaitUntil(() => clientHandler.StoppedReason != null);
        Assert.Multiple(() =>
        {
            Assert.That(clientHandler.OnConnectedCalled, Is.False);
            Assert.That(fixture.Server.IsValid, Is.True);
            Assert.That(fixture.Server.IsStarted, Is.True);
        });
    }

    [Test]
    public void FillAckResponse_Exception_FailsEstablishment()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var throwingHandler = new ThrowingFillAckResponseServerHandler();
        var clientHandler = new RecordingClientHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return throwingHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        WaitUntil(() => clientHandler.StoppedReason != null);
        Assert.Multiple(() =>
        {
            Assert.That(clientHandler.OnConnectedCalled, Is.False);
            Assert.That(clientHandler.OnDisconnectedCalled, Is.False);
            Assert.That(throwingHandler.IsConnected, Is.False);
        });
    }

    [Test]
    public void FillAckResponse_Oversized_FailsEstablishment()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var throwingHandler = new OversizedFillAckResponseServerHandler(fixture.Server);
        var clientHandler = new RecordingClientHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return throwingHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        WaitUntil(() => clientHandler.StoppedReason != null);
        Assert.Multiple(() =>
        {
            Assert.That(clientHandler.OnConnectedCalled, Is.False);
            Assert.That(clientHandler.OnDisconnectedCalled, Is.False);
        });
    }

    [Test]
    public async Task Server_OnConnected_OnlyAfterAckResponseCommited()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var control = GetControl<IRawReliableAckTransportConformanceControl>(fixture.Server);
        var gateHit = control.BeforeAckResponseCommitGate.Arm();
        var serverHandler = new RecordingServerHandler();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        await gateHit.WaitAsync(DeliveryTimeout);
        Assert.That(serverHandler.OnConnectedCalled, Is.False);
        control.BeforeAckResponseCommitGate.Reset();
        WaitUntil(() => serverHandler.OnConnectedCalled);
    }

    [Test]
    public void AckResponse_ReachesClient_OnConnected()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new AckResponseWritingServerHandler(42);
        var clientHandler = new AckResponseRecordingClientHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        WaitForConnectedEndpoint(clientHandler);
        Assert.That(clientHandler.AckResponseValue, Is.EqualTo(42));
    }

    [Test]
    public void Client_OnConnected_AckResponseIsDelivered()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        WaitForConnectedEndpoint(clientHandler);
        Assert.That(clientHandler.AckResponseReceived, Is.True);
    }

    // ── Send ────────────────────────────────────────────────────────────

    [Test]
    public void Send_Ok_AcceptsMessage()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.That(endpoint.Send(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
    }

    [Test]
    public void Send_NotConnected_AfterDisconnect()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.That(endpoint.Disconnect(new UserIntention("test", "d")), Is.True);
        WaitUntil(() => clientHandler.OnDisconnectedCalled);
        Assert.That(endpoint.Send(CreateMessage(client, 1)), Is.EqualTo(SendResult.NotConnected));
    }

    [Test]
    public void Send_InvalidMessage_Null()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.Multiple(() =>
        {
            Assert.That(endpoint.Send(null!), Is.EqualTo(SendResult.InvalidMessage));
            Assert.That(client.IsValid, Is.True);
        });
    }

    [Test]
    public void Send_MessageTooBig()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.Multiple(() =>
        {
            Assert.That(endpoint.Send(CreateOversizedMessage(client)), Is.EqualTo(SendResult.MessageTooBig));
            Assert.That(client.IsValid, Is.True);
        });
    }

    [Test]
    public void Send_Error_AfterTransportStop()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.That(fixture.Server.Stop(), Is.True);
        Assert.That(endpoint.Send(CreateMessage(client, 1)), Is.EqualTo(SendResult.Error));
    }

    [Test]
    public async Task Send_ThreadSafe_Concurrent()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        const int messageCount = 32;
        var sends = Enumerable.Range(0, messageCount)
            .Select(_ => Task.Run(() => endpoint.Send(CreateMessage(client, 1))));
        var results = await Task.WhenAll(sends);

        Assert.That(results, Is.All.EqualTo(SendResult.Ok));
        WaitUntil(() => serverHandler.ReceivedCount == messageCount);
    }

    // ── Delivery ────────────────────────────────────────────────────────

    [Test]
    public void Delivery_FifoOrder_ClientToServer()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        const int messageCount = 8;
        for (var i = 0; i < messageCount; i++)
            Assert.That(endpoint.Send(CreateMessage(client, i)), Is.EqualTo(SendResult.Ok));

        WaitUntil(() => serverHandler.ReceivedCount == messageCount);
        Assert.That(serverHandler.ReceivedValues, Is.EqualTo(Enumerable.Range(0, messageCount)));
    }

    [Test]
    public void Delivery_FifoOrder_ServerToClient()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new ServerSendsValuesHandler(fixture, null, 0, 1, 2, 3, 4, 5, 6, 7);
        var clientHandler = new RecordingClientHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.That(endpoint.Send(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        WaitUntil(() => clientHandler.ReceivedCount == 8);
        Assert.That(clientHandler.ReceivedValues, Is.EqualTo(Enumerable.Range(0, 8)));
    }

    [Test]
    public async Task Delivery_EmptyMessages()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new SizeRecordingServerHandler();
        var clientHandler = new RecordingClientHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.That(endpoint.Send(CreateEmptyMessage(client)), Is.EqualTo(SendResult.Ok));
        await serverHandler.Received.Task.WaitAsync(DeliveryTimeout);
    }

    [Test]
    public async Task Delivery_ComplexContent_BothDirections()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = CreateComplexMessage(client);
        var response = CreateMessage(fixture.Server, 42);
        var expectedResponse = response.Clone(fixture.Server.Memory.CollectablePool);
        var serverHandler = new EchoServerHandler(fixture, expected, response, serverReceived);
        var clientHandler = new AssertContentClientHandler(null, expectedResponse, clientReceived);
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.That(endpoint.Send(expected.Clone(client.Memory.CollectablePool)), Is.EqualTo(SendResult.Ok));
        Assert.That(await serverReceived.Task.WaitAsync(DeliveryTimeout), Is.True);
        Assert.That(await clientReceived.Task.WaitAsync(DeliveryTimeout), Is.True);
    }

    [Test]
    public async Task Delivery_ExactLimitMessages()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new SizeRecordingServerHandler();
        var clientHandler = new RecordingClientHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.That(endpoint.Send(CreateExactLimitMessage(client, client.MessageMaxByteSize)), Is.EqualTo(SendResult.Ok));
        await serverHandler.Received.Task.WaitAsync(DeliveryTimeout);
        Assert.That(serverHandler.LastSize, Is.EqualTo(client.MessageMaxByteSize));
    }

    [Test]
    public void Delivery_ConsistentMessageMaxByteSize()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var clientEndpoint = WaitForConnectedEndpoint(clientHandler);
        var serverEndpoint = WaitForConnectedEndpoint(serverHandler);
        Assert.That(clientEndpoint.MessageMaxByteSize, Is.EqualTo(serverEndpoint.MessageMaxByteSize));
    }

    [Test]
    public async Task Delivery_HandshakeNotDeliveredThroughOnReceived()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        WaitForConnectedEndpoint(clientHandler);
        await Task.Delay(200);
        Assert.That(serverHandler.ReceivedCount, Is.Zero);
    }

    [Test]
    public void Delivery_ValidInjectedData()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        WaitForConnectedEndpoint(serverHandler);
        var epControl = GetEndpointControl<IRawReliableAckEndpointConformanceControl>(serverHandler.Endpoint!);
        epControl.InjectInboundData(CreateMessage(fixture.Server, 42));

        WaitUntil(() => serverHandler.ReceivedCount == 1);
        Assert.That(serverHandler.ReceivedValues, Is.EqualTo(new[] { 42 }));
    }

    [Test]
    public void Delivery_MalformedInbound_Disconnects()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        var epControl = GetEndpointControl<IRawReliableAckEndpointConformanceControl>(endpoint);
        epControl.InjectInboundData(CreateOversizedMessage(client));

        WaitUntil(() => clientHandler.OnDisconnectedCalled);
    }

    // ── Disconnect ──────────────────────────────────────────────────────

    [Test]
    public void Disconnect_ReturnsTrueOnceThenFalse()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.That(endpoint.Disconnect(new UserIntention("test", "d")), Is.True);
        Assert.That(endpoint.Disconnect(new UserIntention("test", "d")), Is.False);
    }

    [Test]
    public void Disconnect_ReasonPropagated_ExactSameInstance()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        var reason = new UserIntention("test", "prop");
        Assert.That(endpoint.Disconnect(reason), Is.True);
        WaitUntil(() => clientHandler.OnDisconnectedCalled);
        Assert.That(clientHandler.DisconnectReason, Is.SameAs(reason));
        WaitUntil(() => clientHandler.OnStoppedCalled);
        Assert.That(clientHandler.StoppedReason, Is.SameAs(reason));
    }

    [Test]
    public void Disconnect_NullReason_Unknown()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.That(endpoint.Disconnect(null!), Is.True);
        WaitUntil(() => clientHandler.DisconnectReason != null);
        Assert.That(clientHandler.DisconnectReason, Is.InstanceOf<Unknown>());
    }

    [Test]
    public void ClientEndpoint_Disconnect_StopsTransport()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.That(endpoint.Disconnect(new UserIntention("test", "d")), Is.True);
        WaitUntil(() => !client.IsStarted);
        Assert.That(client.IsValid, Is.True);
    }

    [Test]
    public async Task ServerEndpoint_Disconnect_KeepsServerRunning()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new DisconnectOnReceiveServerHandler();
        var clientHandler = new RecordingClientHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.That(endpoint.Send(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        WaitUntil(() => serverHandler.OnDisconnectedCount == 1);

        await Task.Delay(200);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsValid, Is.True);
            Assert.That(fixture.Server.IsStarted, Is.True);
        });
    }

    // ── Callbacks ───────────────────────────────────────────────────────

    [Test]
    public async Task Client_OnReceived_NonReentrant()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new BlockingOnReceivedClientHandler();
        var serverHandler = new ServerSendsValuesHandler(fixture, null, 1, 2);
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.That(endpoint.Send(CreateMessage(client, 0)), Is.EqualTo(SendResult.Ok));

        await clientHandler.FirstEntered.Task.WaitAsync(DeliveryTimeout);
        Assert.That(clientHandler.ConcurrentCallbacks, Is.Zero);
        clientHandler.Release.Set();
        await clientHandler.AllCompleted.Task.WaitAsync(DeliveryTimeout);
        Assert.That(clientHandler.CompletedCallbacks, Is.EqualTo(2));
    }

    [Test]
    public async Task Server_OnReceived_NonReentrant()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new BlockingOnReceivedClientHandler();
        var serverHandler = new ServerSendsValuesHandler(fixture, null, 1, 2);
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.That(endpoint.Send(CreateMessage(client, 0)), Is.EqualTo(SendResult.Ok));

        await clientHandler.FirstEntered.Task.WaitAsync(DeliveryTimeout);
        Assert.That(clientHandler.ConcurrentCallbacks, Is.Zero);
        clientHandler.Release.Set();
        await clientHandler.AllCompleted.Task.WaitAsync(DeliveryTimeout);
    }

    [Test]
    public async Task TryAck_Serialization_NoOverlap()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var control = GetControl<IRawReliableAckTransportConformanceControl>(fixture.Server);
        var tryAckCalls = 0;
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData =>
        {
            Interlocked.Increment(ref tryAckCalls);
            ackData.Release();
            return new RecordingServerHandler();
        })), Is.True);

        var gateHit = control.BeforeAcknowledgerGate.Arm(2);
        var firstClient = fixture.CreateClient();
        var firstClientHandler = new RecordingClientHandler();
        Assert.That(firstClient.Init(firstClientHandler), Is.True);
        var secondClient = fixture.CreateClient();
        var secondClientHandler = new RecordingClientHandler();
        Assert.That(secondClient.Init(secondClientHandler), Is.True);
        Start(fixture.Server, firstClient);
        Assert.That(secondClient.Start(_ => { }), Is.True);

        await gateHit.WaitAsync(DeliveryTimeout);
        await Task.Delay(200);
        Assert.That(Volatile.Read(ref tryAckCalls), Is.EqualTo(1));
        control.BeforeAcknowledgerGate.Reset();
        WaitUntil(() => Volatile.Read(ref tryAckCalls) == 2);
    }

    [Test]
    public void Handler_OnReceivedThrows_TerminatesConnection()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new ThrowingOnReceivedServerHandler();
        var clientHandler = new RecordingClientHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.That(endpoint.Send(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        Assert.That(endpoint.Send(CreateMessage(client, 2)), Is.EqualTo(SendResult.Ok));
        WaitUntil(() => serverHandler.DisconnectReason != null);
        Assert.That(serverHandler.DeliveredCount, Is.Zero);
    }

    [Test]
    public async Task Server_Stop_FromReceiveHandler()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new StopFromReceiveServerHandler(fixture);
        var clientHandler = new RecordingClientHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);

        var stopped = new TaskCompletionSource<StopReason>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.That(fixture.Server.Start(reason => stopped.TrySetResult(reason)), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.That(endpoint.Send(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));

        await stopped.Task.WaitAsync(DeliveryTimeout);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsStarted, Is.False);
            Assert.That(fixture.Server.IsValid, Is.True);
        });
    }

    // ── Transport shutdown ──────────────────────────────────────────────

    [Test]
    public void Client_Stop_Connected_ExactReason()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        WaitForConnectedEndpoint(clientHandler);
        var reason = new UserIntention("test", "clientstop");
        Assert.That(client.Stop(reason), Is.True);
        WaitUntil(() => clientHandler.OnDisconnectedCalled && clientHandler.OnStoppedCalled);
        Assert.Multiple(() =>
        {
            Assert.That(clientHandler.DisconnectReason, Is.SameAs(reason));
            Assert.That(clientHandler.StoppedReason, Is.SameAs(reason));
        });
    }

    [Test]
    public async Task Client_Stop_PreConnect_OnlyOnStopped()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        Assert.That(client.Init(clientHandler), Is.True);
        StartClient(client);

        await Task.Delay(200);
        Assert.That(client.Stop(new UserIntention("test", "prestop")), Is.True);
        WaitUntil(() => clientHandler.OnStoppedCalled);
        Assert.Multiple(() =>
        {
            Assert.That(clientHandler.OnConnectedCalled, Is.False);
            Assert.That(clientHandler.OnDisconnectedCalled, Is.False);
        });
    }

    [Test]
    public void Server_Stop_DisconnectsAllSessions()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var handlers = new ConcurrentQueue<SessionRecordingServerHandler>();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData =>
        {
            var handler = new SessionRecordingServerHandler();
            handlers.Enqueue(handler);
            ackData.Release();
            return handler;
        })), Is.True);

        var firstClient = fixture.CreateClient();
        var firstClientHandler = new RecordingClientHandler();
        Assert.That(firstClient.Init(firstClientHandler), Is.True);
        var secondClient = fixture.CreateClient();
        var secondClientHandler = new RecordingClientHandler();
        Assert.That(secondClient.Init(secondClientHandler), Is.True);
        Start(fixture.Server, firstClient);
        Assert.That(secondClient.Start(_ => { }), Is.True);

        var firstEndpoint = WaitForConnectedEndpoint(firstClientHandler);
        var secondEndpoint = WaitForConnectedEndpoint(secondClientHandler);
        Assert.That(firstEndpoint.Send(CreateMessage(firstClient, 1)), Is.EqualTo(SendResult.Ok));
        Assert.That(secondEndpoint.Send(CreateMessage(secondClient, 2)), Is.EqualTo(SendResult.Ok));

        WaitForHandler(handlers, 2);
        var created = handlers.ToArray();
        Assert.That(fixture.Server.Stop(new UserIntention("test", "serverstop")), Is.True);
        WaitUntil(() => created[0].OnDisconnectedCount == 1 && created[1].OnDisconnectedCount == 1);
    }

    // ── Conformance controls ────────────────────────────────────────────

    [Test]
    public async Task BeforeAcknowledgerGate_BlocksTryAck()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var control = GetControl<IRawReliableAckTransportConformanceControl>(fixture.Server);
        var called = 0;
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData =>
        {
            Interlocked.Increment(ref called);
            ackData.Release();
            return new RecordingServerHandler();
        })), Is.True);

        var gateHit = control.BeforeAcknowledgerGate.Arm();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        await gateHit.WaitAsync(DeliveryTimeout);
        Assert.That(Volatile.Read(ref called), Is.Zero);
        control.BeforeAcknowledgerGate.Reset();
        WaitUntil(() => Volatile.Read(ref called) == 1);
    }

    [Test]
    public async Task BeforeHandlerConnectedGate_BlocksClient()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var control = GetControl<IRawReliableAckTransportConformanceControl>(client);
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);

        var gateHit = control.BeforeHandlerConnectedGate.Arm();
        Start(fixture.Server, client);

        await gateHit.WaitAsync(DeliveryTimeout);
        Assert.That(clientHandler.OnConnectedCalled, Is.False);
        control.BeforeHandlerConnectedGate.Reset();
        WaitUntil(() => clientHandler.OnConnectedCalled);
    }

    [Test]
    public async Task BeforeHandlerConnectedGate_BlocksServer()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var control = GetControl<IRawReliableAckTransportConformanceControl>(fixture.Server);
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);

        var gateHit = control.BeforeHandlerConnectedGate.Arm();
        Start(fixture.Server, client);

        await gateHit.WaitAsync(DeliveryTimeout);
        Assert.That(serverHandler.OnConnectedCalled, Is.False);
        control.BeforeHandlerConnectedGate.Reset();
        WaitUntil(() => serverHandler.OnConnectedCalled);
    }

    [Test]
    public async Task BeforeEndpointDisconnectStateTransitionGate_Blocks()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        var epControl = GetEndpointControl<IRawReliableAckEndpointConformanceControl>(endpoint);
        var gateHit = epControl.BeforeEndpointDisconnectStateTransitionGate.Arm();
        var disconnectTask = Task.Run(() => endpoint.Disconnect(new UserIntention("test", "g")));

        await gateHit.WaitAsync(DeliveryTimeout);
        Assert.That(endpoint.IsConnected, Is.True);
        epControl.BeforeEndpointDisconnectStateTransitionGate.Reset();
        Assert.That(await disconnectTask.WaitAsync(DeliveryTimeout), Is.True);
        Assert.That(endpoint.IsConnected, Is.False);
    }

    [Test]
    public async Task BeforeHandlerDisconnectedGate_Blocks()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        var epControl = GetEndpointControl<IRawReliableAckEndpointConformanceControl>(endpoint);
        var gateHit = epControl.BeforeHandlerDisconnectedGate.Arm();
        var disconnectTask = Task.Run(() => endpoint.Disconnect(new UserIntention("test", "g")));

        await gateHit.WaitAsync(DeliveryTimeout);
        Assert.That(clientHandler.OnDisconnectedCalled, Is.False);
        epControl.BeforeHandlerDisconnectedGate.Reset();
        Assert.That(await disconnectTask.WaitAsync(DeliveryTimeout), Is.True);
        WaitUntil(() => clientHandler.OnDisconnectedCalled);
    }

    [Test]
    public async Task BeforeHandlerStoppedGate_BlocksClient()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        var epControl = GetEndpointControl<IRawReliableAckEndpointConformanceControl>(endpoint);
        var gateHit = epControl.BeforeHandlerStoppedGate.Arm();
        var stopTask = Task.Run(() => client.Stop(new UserIntention("test", "g")));

        await gateHit.WaitAsync(DeliveryTimeout);
        Assert.That(clientHandler.OnStoppedCalled, Is.False);
        epControl.BeforeHandlerStoppedGate.Reset();
        Assert.That(await stopTask.WaitAsync(DeliveryTimeout), Is.True);
        WaitUntil(() => clientHandler.OnStoppedCalled);
    }

    [Test]
    public async Task BeforeSendCommitGate_BlocksCommit()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        var epControl = GetEndpointControl<IRawReliableAckEndpointConformanceControl>(endpoint);
        var gateHit = epControl.BeforeSendCommitGate.Arm();
        Assert.That(endpoint.Send(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        await gateHit.WaitAsync(DeliveryTimeout);
        Assert.That(serverHandler.ReceivedCount, Is.Zero);

        epControl.BeforeSendCommitGate.Reset();
        WaitUntil(() => serverHandler.ReceivedCount == 1);
    }

    [Test]
    public async Task AfterSendCommitGate_BlocksAfterCommit()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        var epControl = GetEndpointControl<IRawReliableAckEndpointConformanceControl>(endpoint);
        var gateHit = epControl.AfterSendCommitGate.Arm();
        Assert.That(endpoint.Send(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        await gateHit.WaitAsync(DeliveryTimeout);
        WaitUntil(() => epControl.AfterSendCommitHitCount == 1);
        await Task.Delay(200);
        Assert.That(serverHandler.ReceivedCount, Is.Zero);

        epControl.AfterSendCommitGate.Reset();
        WaitUntil(() => serverHandler.ReceivedCount == 1);
    }

    [Test]
    public async Task AfterReceivedGate_BlocksOnReceived()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new BlockingPreReceiveServerHandler();
        var clientHandler = new RecordingClientHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        Assert.That(endpoint.Send(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));

        WaitUntil(() => serverHandler.Reached != null);
        await serverHandler.Reached!.WaitAsync(DeliveryTimeout);
        Assert.That(serverHandler.CallbackBegan, Is.False);
        serverHandler.Gate!.Reset();
        WaitUntil(() => serverHandler.CallbackBegan);
    }

    [Test]
    public void BeforeSendCommitHitCount_Monotonic()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForConnectedEndpoint(clientHandler);
        var epControl = GetEndpointControl<IRawReliableAckEndpointConformanceControl>(endpoint);
        Assert.That(endpoint.Send(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        Assert.That(endpoint.Send(CreateMessage(client, 2)), Is.EqualTo(SendResult.Ok));
        Assert.That(endpoint.Send(CreateMessage(client, 3)), Is.EqualTo(SendResult.Ok));

        WaitUntil(() => epControl.BeforeSendCommitHitCount == 3);
    }

    // ── Concurrent ──────────────────────────────────────────────────────

    [Test]
    public async Task StatusReads_SafeDuringStop()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingClientHandler();
        var serverHandler = new RecordingServerHandler();
        Assert.That(fixture.InitServer(fixture.CreateSimpleAcknowledger(ackData => { ackData.Release(); return serverHandler; })), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);
        WaitForConnectedEndpoint(clientHandler);
        WaitForConnectedEndpoint(serverHandler);

        using var cancellation = new CancellationTokenSource();
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                var serverValid = fixture.Server.IsValid;
                var serverStarted = fixture.Server.IsStarted;
                var clientValid = client.IsValid;
                var clientStarted = client.IsStarted;
            }
        }));

        Assert.That(await Task.Run(() => fixture.Server.Stop()).WaitAsync(DeliveryTimeout), Is.True);
        cancellation.Cancel();
        await Task.WhenAll(readers).WaitAsync(DeliveryTimeout);
    }

    // ── Ack handshake helpers ───────────────────────────────────────────

    protected sealed class AckDataWritingClientHandler : RawReliableAckTestClientHandler
    {
        private readonly int _value;

        public AckDataWritingClientHandler(int value, IRawReliableAckEndpointTracker? tracker = null)
            : base(tracker)
        {
            _value = value;
        }

        public override void FillAckData(UnionDataList ackData)
        {
            ackData.PutLast(_value);
        }

        public override void OnReceived(UnionDataList message)
        {
            message.Release();
        }
    }

    protected sealed class AckResponseWritingServerHandler : RawReliableAckTestServerHandler
    {
        private readonly int _value;

        public AckResponseWritingServerHandler(int value, IRawReliableAckEndpointTracker? tracker = null)
            : base(tracker)
        {
            _value = value;
        }

        public override void FillAckResponse(UnionDataList ackData)
        {
            ackData.PutLast(_value);
        }

        public override void OnReceived(UnionDataList message)
        {
            message.Release();
        }
    }
}
