using System;
using System.Threading;
using System.Threading.Tasks;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Utils;
using Pontifex.Utils.CheckPointGate;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Test-only conformance control for a single IRawReliableEndpoint.
    /// Implements the RawReliableAck contract variant. All checkpoint gates are
    /// inactive until armed by a conformance adapter.
    /// </summary>
    public sealed class RawReliableEndpointConformanceControl : IRawReliableAckEndpointConformanceControl
    {
        private readonly CheckPoint _beforeEndpointDisconnectStateTransitionGate = new();
        private readonly CheckPoint _beforeHandlerDisconnectedGate = new();
        private readonly CheckPoint _beforeHandlerStoppedGate = new();
        private readonly CountingCheckPoint _beforeSendCommitGate = new();
        private readonly CountingCheckPoint _afterSendCommitGate = new();
        private readonly CountingCheckPoint _afterReceivedGate = new();

        private Action<UnionDataList>? _injectInbound;

        public string Name => "ConformanceControl(RawReliableEndpoint)";

        public ICheckPointCtl BeforeEndpointDisconnectStateTransitionGate => _beforeEndpointDisconnectStateTransitionGate;

        public ICheckPointCtl BeforeHandlerDisconnectedGate => _beforeHandlerDisconnectedGate;

        public ICheckPointCtl BeforeHandlerStoppedGate => _beforeHandlerStoppedGate;

        public ICheckPointCtl BeforeSendCommitGate => _beforeSendCommitGate;

        public ICheckPointCtl AfterSendCommitGate => _afterSendCommitGate;

        public ICheckPointCtl AfterReceivedGate => _afterReceivedGate;

        public int BeforeSendCommitHitCount => _beforeSendCommitGate.TotalHits;

        public int AfterSendCommitHitCount => _afterSendCommitGate.TotalHits;

        public int AfterReceivedHitCount => _afterReceivedGate.TotalHits;

        internal void SetInjector(Action<UnionDataList> injector) => _injectInbound = injector;

        public void InjectInboundData(UnionDataList data) => _injectInbound?.Invoke(data);
    }

    /// <summary>
    /// A checkpoint gate that additionally counts every hit invocation, even
    /// while unarmed, for the monotonic conformance hit counters.
    /// </summary>
    internal sealed class CountingCheckPoint : ICheckPointCtl
    {
        private readonly CheckPoint _inner = new();
        private int _totalHits;

        public int TotalHits => Volatile.Read(ref _totalHits);

        public bool IsArmed => _inner.IsArmed;

        public int HitCount => _inner.HitCount;

        public void Hit()
        {
            Interlocked.Increment(ref _totalHits);
            _inner.Hit();
        }

        public ValueTask HitAsync()
        {
            Interlocked.Increment(ref _totalHits);
            return _inner.HitAsync();
        }

        public Task<CheckPointWaitResult> Arm(int requiredHits = 1) => _inner.Arm(requiredHits);

        public void Reset() => _inner.Reset();

        public void Dispose() => _inner.Dispose();
    }
}
