using System;
using System.Collections.Generic;
using System.Threading;
using Pontifex.Raw.Unreliable.Ack;
using Pontifex.Raw.Unreliable.NoAck;
using Pontifex.Utils;

namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Base endpoint implementation shared by all RawUnreliable transports.
    /// The owning transport constructs this type and wires its send and stop delegates.
    /// </summary>
    public class RawUnreliableEndpoint : RawEndpoint, IRawUnreliableEndpoint
    {
        private readonly RawUnreliableEndpointConformanceControl _conformance = new();

        /// <summary>
        /// Set by the owning transport once the endpoint can commit sends to a
        /// carrier. Null until wired; a null delegate rejects with Error.
        /// </summary>
        internal Func<RawUnreliableEndpoint, UnionDataList, SendResult>? SendDelegate;

        /// <summary>
        /// Set by the owning transport to drive the endpoint stop transition.
        /// Null until wired; a null delegate causes Stop to return false.
        /// </summary>
        internal Func<RawUnreliableEndpoint, StopReason?, bool>? StopDelegate;

        internal RawUnreliableEndpoint(RawUnreliableTransport owner, IRawUnreliableHandler handler, IEndPoint? remote)
            : base(owner, handler, remote)
        {
        }

        public bool IsValid => IsValidInternal;

        public IRawUnreliableEndpointConformanceControl Conformance => _conformance;

        internal IRawUnreliableHandler Handler => (IRawUnreliableHandler)RawHandler;

        internal override void HitEndpointStopStateTransitionGate()
            => _conformance.BeforeEndpointStopStateTransitionGate.Hit();

        internal override void HitAfterReceivedGate()
            => _conformance.AfterReceivedGate.Hit();

        internal override void HitBeforeHandlerStoppedGate()
            => _conformance.BeforeHandlerStoppedGate.Hit();

        internal override void HitBeforeHandlerDisconnectedGate()
        {
        }

        public SendResult UnreliableSend(UnionDataList message)
        {
            if (!IsValidInternal)
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

        public override void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
            if (predicate?.Invoke(_conformance) ?? true)
            {
                dst.Add(_conformance);
            }
        }

        public override string ToString() => $"raw-unreliable-endpoint[{RemoteEndPoint}]";
    }
}
