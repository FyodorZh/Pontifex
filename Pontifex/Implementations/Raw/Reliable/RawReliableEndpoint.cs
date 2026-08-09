using System;
using System.Collections.Generic;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Base endpoint implementation shared by all RawReliable transports. Owns
    /// the connected/disconnected state and the send/disconnect operations of
    /// one logical connection. The owning transport constructs this type and
    /// wires its send and disconnect delegates.
    /// </summary>
    public class RawReliableEndpoint : RawEndpoint, IRawReliableEndpoint
    {
        private readonly RawReliableEndpointConformanceControl _conformance = new();
        private volatile bool _isConnected;

        /// <summary>
        /// Set by the owning transport once the endpoint can commit sends to a
        /// carrier. Null until wired; a null delegate rejects with Error.
        /// </summary>
        internal Func<RawReliableEndpoint, UnionDataList, SendResult>? SendDelegate;

        /// <summary>
        /// Set by the owning transport to drive the endpoint disconnect
        /// transition. Null until wired; a null delegate causes Disconnect to
        /// return false.
        /// </summary>
        internal Func<RawReliableEndpoint, StopReason, bool>? DisconnectDelegate;

        internal RawReliableEndpoint(RawReliableTransport owner, IRawReliableHandler handler, IEndPoint? remote)
            : base(owner, handler, remote)
        {
            _conformance.SetInjector(data => owner.InjectInboundToEndpoint(this, data));
        }

        public bool IsConnected => _isConnected;

        public IRawReliableAckEndpointConformanceControl Conformance => _conformance;

        internal IRawReliableHandler Handler => (IRawReliableHandler)RawHandler;

        internal void MarkConnected() => _isConnected = true;

        internal void MarkDisconnected() => _isConnected = false;

        internal override void HitEndpointStopStateTransitionGate()
            => _conformance.BeforeEndpointDisconnectStateTransitionGate.Hit();

        internal override void HitAfterReceivedGate()
            => _conformance.AfterReceivedGate.Hit();

        internal override void HitBeforeHandlerStoppedGate()
            => _conformance.BeforeHandlerStoppedGate.Hit();

        internal override void HitBeforeHandlerDisconnectedGate()
            => _conformance.BeforeHandlerDisconnectedGate.Hit();

        public SendResult Send(UnionDataList bufferToSend)
        {
            if (!_isConnected)
            {
                bufferToSend?.Release();
                return SendResult.NotConnected;
            }

            if (bufferToSend == null!)
            {
                return SendResult.InvalidMessage;
            }

            if (bufferToSend.GetDataSize() > MessageMaxByteSize)
            {
                bufferToSend.Release();
                return SendResult.MessageTooBig;
            }

            var sendDelegate = SendDelegate;
            if (sendDelegate == null)
            {
                bufferToSend.Release();
                return SendResult.Error;
            }

            return sendDelegate(this, bufferToSend);
        }

        public bool Disconnect(StopReason reason)
        {
            var disconnectDelegate = DisconnectDelegate;
            if (disconnectDelegate == null)
            {
                return false;
            }

            return disconnectDelegate(this, reason);
        }

        public override void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
            if (predicate?.Invoke(_conformance) ?? true)
            {
                dst.Add(_conformance);
            }
        }

        public override string ToString() => $"raw-reliable-endpoint[{RemoteEndPoint}]";
    }
}
