using System;
using System.Collections.Generic;
using System.Threading;
using Pontifex.Utils;

namespace Pontifex.Raw.Unreliable.NoAck
{
    /// <summary>
    /// Base endpoint implementation for the RawUnreliableNoAck transport.
    /// The owning transport (added in a later work package) constructs this
    /// type and wires its send and stop delegates.
    /// </summary>
    public class RawUnreliableNoAckEndpoint : IRawUnreliableEndpoint
    {
        private readonly RawUnreliableNoAckTransport _owner;
        private readonly IRawUnreliableHandler _handler;
        private readonly IEndPoint? _remote;
        private readonly RawUnreliableNoAckEndpointConformanceControl _conformance = new();
        private volatile bool _isValid;
        private bool _onStartedCompleted;
        private int _stopInitiated;
        private bool _teardownDone;

        /// <summary>
        /// Set by the owning transport once the endpoint can commit sends to a
        /// carrier. Null until wired; a null delegate rejects with Error.
        /// </summary>
        internal Func<RawUnreliableNoAckEndpoint, UnionDataList, SendResult>? SendDelegate;

        /// <summary>
        /// Set by the owning transport to drive the endpoint stop transition.
        /// Null until wired; a null delegate causes Stop to return false.
        /// </summary>
        internal Func<RawUnreliableNoAckEndpoint, StopReason?, bool>? StopDelegate;

        internal RawUnreliableNoAckEndpoint(RawUnreliableNoAckTransport owner, IRawUnreliableHandler handler, IEndPoint? remote)
        {
            _owner = owner;
            _handler = handler;
            _remote = remote;
        }

        public bool IsValid => _isValid;

        public IEndPoint? RemoteEndPoint => _remote;

        public int MessageMaxByteSize => ((IRawTransport)_owner).MessageMaxByteSize;

        public IRawUnreliableNoAckEndpointConformanceControl Conformance => _conformance;

        internal IRawUnreliableHandler Handler => _handler;

        internal bool OnStartedCompleted => _onStartedCompleted;

        internal bool TeardownDone => _teardownDone;

        internal void MarkValid() => _isValid = true;

        internal void MarkInvalid() => _isValid = false;

        internal void MarkOnStartedCompleted() => _onStartedCompleted = true;

        internal void MarkTeardownDone() => _teardownDone = true;

        internal bool TryBeginStop() => Interlocked.CompareExchange(ref _stopInitiated, 1, 0) == 0;

        public SendResult UnreliableSend(UnionDataList message)
        {
            if (!_isValid)
            {
                message?.Release();
                return SendResult.Error;
            }

            if (message == null!)
            {
                return SendResult.InvalidMessage;
            }

            if (message.GetDataSize() > MessageMaxByteSize)
            {
                message.Release();
                return SendResult.MessageTooBig;
            }

            var sendDelegate = SendDelegate;
            if (sendDelegate == null)
            {
                message.Release();
                return SendResult.Error;
            }

            return sendDelegate(this, message);
        }

        public bool Stop(StopReason? reason = null)
        {
            var stopDelegate = StopDelegate;
            if (stopDelegate == null)
            {
                return false;
            }

            return stopDelegate(this, reason);
        }

        public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
            if (predicate?.Invoke(_conformance) ?? true)
            {
                dst.Add(_conformance);
            }
        }

        public override string ToString() => $"raw-unreliable-endpoint[{_remote}]";
    }
}
