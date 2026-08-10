using System;
using System.Collections.Generic;
using System.Threading;
using Pontifex.Utils;

namespace Pontifex.Raw
{
    /// <summary>
    /// Base endpoint implementation shared by all Raw transports. Holds the
    /// routing metadata, validity/lifecycle flags, and the owner reference.
    /// Family-specific endpoints derive from this type and add their contract
    /// operations (send, stop/disconnect) and conformance control.
    /// </summary>
    public abstract class RawEndpoint
    {
        private readonly IRawTransport _owner;
        private readonly IEndPoint? _remote;
        private volatile bool _isValid;
        private bool _onStartedCompleted;
        private int _stopInitiated;
        private bool _teardownDone;

        /// <summary>
        /// The handler bound to this endpoint by the owning transport.
        /// Family endpoints expose a typed accessor over this value.
        /// </summary>
        internal IRawHandler RawHandler { get; }

        protected RawEndpoint(IRawTransport owner, IRawHandler handler, IEndPoint? remote)
        {
            _owner = owner;
            RawHandler = handler;
            _remote = remote;
        }

        internal IRawTransport OwnerTransport => _owner;

        public IEndPoint? RemoteEndPoint => _remote;

        public int MessageMaxByteSize => _owner.MessageMaxByteSize;

        /// <summary>
        /// Internal usability flag driven by the owning transport. Not exposed
        /// through the contract interfaces directly; family endpoints surface
        /// their own public state (IsValid / IsConnected).
        /// </summary>
        internal bool IsValidInternal => _isValid;

        internal bool OnStartedCompleted => _onStartedCompleted;

        internal bool TeardownDone => _teardownDone;

        internal void MarkValid() => _isValid = true;

        internal void MarkInvalid() => _isValid = false;

        internal void MarkOnStartedCompleted() => _onStartedCompleted = true;

        internal void MarkTeardownDone() => _teardownDone = true;

        internal bool TryBeginStop() => Interlocked.CompareExchange(ref _stopInitiated, 1, 0) == 0;

        /// <summary>
        /// Holds the endpoint conformance checkpoint that fires when the endpoint
        /// is about to transition out of its usable (connected / valid) state.
        /// </summary>
        internal abstract void HitEndpointStopStateTransitionGate();

        /// <summary>
        /// Holds the endpoint conformance checkpoint fired immediately before an
        /// inbound message is delivered to the handler's OnReceived.
        /// </summary>
        internal abstract void HitAfterReceivedGate();

        /// <summary>
        /// Holds the endpoint conformance checkpoint fired immediately before the
        /// endpoint invokes its handler's stopped (or disconnected) callback.
        /// </summary>
        internal abstract void HitBeforeHandlerStoppedGate();

        /// <summary>
        /// Holds the endpoint conformance checkpoint fired immediately before the
        /// endpoint invokes its handler's disconnected callback. Unreliable
        /// endpoints have no disconnected lifecycle and implement this as a no-op.
        /// </summary>
        internal abstract void HitBeforeHandlerDisconnectedGate();

        public abstract void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null);
    }
}
