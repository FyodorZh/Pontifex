using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable
{
    /// <inheritdoc cref="IRawUnreliableEndpointImp"/>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The endpoint owns a capped pre-start inbox and raises
    /// <see cref="IRawUnreliableHandler.OnStarted"/> from its own flush action, after the base start
    /// completion. The handler stop notification is forwarded only when that start notification returned
    /// successfully; a handler that never received it (a throwing start or a stop raised while starting)
    /// receives no stop notification.</description></item>
    /// <item><description><b>Other notes</b>: The public one-shot <see cref="IRawUnreliableEndpoint.Stop"/>
    /// entry is implemented explicitly so the base <see cref="RawEndpoint.Stop(StopReason, bool)"/> overload
    /// stays reachable. Internal failure paths use the base stop and <see cref="RawEndpoint.FailHard"/>
    /// directly.</description></item>
    /// <item><description><b>Thread safety</b>: State transitions and handler dispatch are serialized by the
    /// inherited action queue. The inbox has its own lock; <see cref="Deliver"/> and the flush are ordered so a
    /// message can never overtake one that was already buffered.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawUnreliableEndpoint : RawEndpoint, IRawUnreliableEndpointImp
    {
        /// <summary>
        /// The state of the pre-start inbox.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The state is keyed on the inbox itself rather than on
        /// <see cref="IRawEndpointImp.IsRunning"/>. <see cref="IsRunning"/> becomes true when the base start
        /// completion runs, which is before this endpoint's flush action. A message delivered in that window
        /// would be scheduled directly while the buffered messages would only be scheduled later by the flush,
        /// so the new message would overtake them. The explicit state closes that window: the flush raises the
        /// start notification, transitions to <see cref="Open"/> and drains the buffered messages while holding
        /// the inbox lock.</description></item>
        /// </list>
        /// </remarks>
        private enum InboxState
        {
            /// <summary>
            /// The inbox is released: the endpoint is invalid and every delivered message is released
            /// immediately.
            /// </summary>
            Closed = 0,

            /// <summary>
            /// The flush action has not run: delivered messages are buffered, subject to the caps.
            /// </summary>
            Buffering,

            /// <summary>
            /// The flush action has drained the buffered messages: delivered messages are scheduled directly.
            /// </summary>
            Open
        }

        private readonly object _inboxLock = new object();
        private readonly Queue<UnionDataList> _inbox = new Queue<UnionDataList>();
        private readonly int _inboxCapacityMessages;
        private readonly long _inboxCapacityBytes;
        private readonly HandlerGate _handlerGate;
        private readonly RawUnreliableEndpointConformanceControl _conformance = new RawUnreliableEndpointConformanceControl();

        private InboxState _inboxState = InboxState.Buffering;
        private long _inboxBytes;
        private volatile bool _invalidated;
        private int _stopLatched;

        /// <summary>
        /// Gets the unreliable handler of this endpoint.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The base raw endpoint keeps its own private handler reference;
        /// this one is needed to raise the unreliable start notification, which the base class does not
        /// know about. Both references target the same handler instance.</description></item>
        /// </list>
        /// </remarks>
        protected IRawUnreliableHandler Handler { get; }

        /// <summary>
        /// Gets the conformance control of this endpoint, which exposes the relocated endpoint gates.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Concrete endpoints hit <c>BeforeSendCommitGate</c> and
        /// <c>AfterSendCommitGate</c> around their own carrier call. The control is also published through
        /// <see cref="GetControls"/>.</description></item>
        /// </list>
        /// </remarks>
        protected RawUnreliableEndpointConformanceControl Conformance => _conformance;

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The endpoint is valid exactly while it is running. The flag
        /// turns over as soon as a stop is requested, so a stopped or stopping endpoint is never valid. A
        /// never-started endpoint is not valid either; it becomes valid when the start completes.</description></item>
        /// </list>
        /// </remarks>
        public bool IsValid => IsRunning;

        /// <summary>
        /// Initializes a new unreliable endpoint owned by the given transport.
        /// </summary>
        /// <param name="transportName">The name of the transport that owns this endpoint.</param>
        /// <param name="remoteEndPoint">The remote endpoint address, or <see langword="null"/> when it is
        /// unknown.</param>
        /// <param name="handler">The handler that receives inbound messages and the unreliable lifecycle
        /// notifications.</param>
        /// <param name="messageMaxByteSize">The inclusive maximum message size, in bytes, admitted by this
        /// endpoint; must be at least 1.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted by this endpoint but not
        /// yet transmitted; must be at least 1.</param>
        /// <param name="inboxCapacityMessages">The maximum number of messages buffered before the start
        /// completes; must not be negative.</param>
        /// <param name="inboxCapacityBytes">The maximum total payload size, in bytes, buffered before the start
        /// completes; must not be negative.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="log">The logging system used by this endpoint.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: The base size and budget limits must each be
        /// at least 1; the inbox limits must not be negative. A message whose payload size does not fit the
        /// remaining inbox byte room is dropped silently, so a byte limit smaller than the smallest message
        /// rejects every buffered message.</description></item>
        /// <item><description><b>Other notes</b>: The handler is retained, not disposed, by the endpoint. The
        /// endpoint subscribes to its own <see cref="IRawEndpointImp.Stopped"/> event here so that the inbox is
        /// released on every terminal path, including a stop of a never-started endpoint and a throwing start
        /// notification.</description></item>
        /// </list>
        /// </remarks>
        protected RawUnreliableEndpoint(
            string transportName,
            IEndPoint remoteEndPoint,
            IRawUnreliableHandler handler,
            int messageMaxByteSize,
            long bufferCapacityBytes,
            int inboxCapacityMessages,
            long inboxCapacityBytes,
            IMemoryRental memory,
            ILogger log)
            : this(new HandlerGate(handler), handler, transportName, remoteEndPoint, messageMaxByteSize,
                bufferCapacityBytes, inboxCapacityMessages, inboxCapacityBytes, memory, log)
        {
        }

        /// <summary>
        /// Initializes a new unreliable endpoint with an already constructed handler gate.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The gate must be constructed by the caller of the base
        /// constructor, because the base constructor needs it before any derived field or constructor body can
        /// run. The protected constructor chains through this overload.</description></item>
        /// </list>
        /// </remarks>
        private RawUnreliableEndpoint(
            HandlerGate handlerGate,
            IRawUnreliableHandler handler,
            string transportName,
            IEndPoint remoteEndPoint,
            int messageMaxByteSize,
            long bufferCapacityBytes,
            int inboxCapacityMessages,
            long inboxCapacityBytes,
            IMemoryRental memory,
            ILogger log)
            : base(transportName, remoteEndPoint, handlerGate, messageMaxByteSize, bufferCapacityBytes, memory, log)
        {
            if (inboxCapacityMessages < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(inboxCapacityMessages));
            }

            if (inboxCapacityBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(inboxCapacityBytes));
            }

            _handlerGate = handlerGate;
            Handler = handler;
            _inboxCapacityMessages = inboxCapacityMessages;
            _inboxCapacityBytes = inboxCapacityBytes;

            Stopped += OnEndpointStopped;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The base implementation completes the raw start and the flush
        /// action scheduled here raises the unreliable start notification and drains the inbox. Overrides that
        /// prepare a transport-specific carrier must do that work first and then call the base implementation
        /// exactly once; the base implementation must not be called more than once per
        /// start.</description></item>
        /// </list>
        /// </remarks>
        protected override void BeginStart()
        {
            FinishStart(NoOp);
            _actionQueue.Schedule(FlushInbox);
        }

        /// <summary>
        /// Raises the unreliable start notification and drains the inbox, in one serialized action.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Runs on the serial action queue while holding the inbox
        /// lock, so no delivery can interleave with the drain and overtake a buffered message.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: A no-op when the inbox is not buffering. A
        /// stop latched or promoted before this action runs leaves the inbox to the
        /// <see cref="IRawEndpointImp.Stopped"/> subscription. When the start notification throws, the exception
        /// escapes so the queue error path logs it and fails the endpoint hard; the stop notification is
        /// suppressed and the inbox is released by the stop subscription.</description></item>
        /// <item><description><b>Other notes</b>: The start notification is raised before the transition to open
        /// and before the drain; a handler that delivers a message from inside the notification has it buffered
        /// and delivered after the already buffered messages. Every drained message, including a server trigger,
        /// hits <c>AfterReceivedGate</c> once immediately before it begins delivery, exactly like a message
        /// delivered through the open inbox.</description></item>
        /// </list>
        /// </remarks>
        private void FlushInbox()
        {
            lock (_inboxLock)
            {
                if (_inboxState != InboxState.Buffering || _invalidated)
                {
                    return;
                }

                Handler.OnStarted(this);
                _handlerGate.MarkStartCompleted();
                _inboxState = InboxState.Open;

                while (_inbox.Count > 0)
                {
                    UnionDataList message = _inbox.Dequeue();
                    _inboxBytes -= message.GetDataSize();
                    Conformance.AfterReceivedGate.Hit();
                    base.ProcessInboundMessage(message);
                }

                _inboxBytes = 0;
            }
        }

        /// <summary>
        /// Releases the inbox and closes it when the endpoint reaches the terminal stopped stage.
        /// </summary>
        /// <param name="reason">The reason the endpoint stopped. It is not used by the release itself.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked from the <see cref="IRawEndpointImp.Stopped"/>
        /// event, on the thread that performed the terminal transition.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: Transitions any inbox state to closed under
        /// the inbox lock and releases every buffered message exactly once, in FIFO order. A second invocation
        /// releases nothing.</description></item>
        /// <item><description><b>Other notes</b>: This subscription covers every terminal path: stop of a
        /// never-started endpoint, stop promoted while starting, start notification throwing, a hard failure,
        /// and a transport-driven stop.</description></item>
        /// </list>
        /// </remarks>
        private void OnEndpointStopped(StopReason reason)
        {
            List<UnionDataList>? released = null;

            lock (_inboxLock)
            {
                _invalidated = true;

                if (_inboxState == InboxState.Closed)
                {
                    return;
                }

                _inboxState = InboxState.Closed;

                if (_inbox.Count > 0)
                {
                    released = new List<UnionDataList>(_inbox.Count);
                    while (_inbox.Count > 0)
                    {
                        released.Add(_inbox.Dequeue());
                    }
                }

                _inboxBytes = 0;
            }

            if (released != null)
            {
                foreach (UnionDataList message in released)
                {
                    message.Release();
                }
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread. The state check and the
        /// enqueue happen under the inbox lock; an open delivery is scheduled outside it, after the flush has
        /// finished.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The message must not be
        /// <see langword="null"/>. Ownership transfers to the endpoint; when the inbox is closed or the caps
        /// reject the message it is released exactly once.</description></item>
        /// <item><description><b>Other notes</b>: Overflow is dropped silently. A delivered message never
        /// overtakes a buffered one: while the flush has not run it is appended to the inbox, and the flush
        /// drains the inbox before the state becomes open.</description></item>
        /// </list>
        /// </remarks>
        public void Deliver(UnionDataList message)
        {
            if (message == null!)
            {
                return;
            }

            lock (_inboxLock)
            {
                switch (_inboxState)
                {
                    case InboxState.Closed:
                        message.Release();
                        return;
                    case InboxState.Buffering:
                        EnqueueNoLock(message);
                        return;
                    case InboxState.Open:
                        break;
                    default:
                        message.Release();
                        return;
                }
            }

            Conformance.AfterReceivedGate.Hit();
            base.ProcessInboundMessage(message);
        }

        /// <summary>
        /// Buffers one message ahead of the flush action.
        /// </summary>
        /// <param name="message">The message to buffer. Ownership transfers to the inbox on acceptance; on
        /// rejection the message is released.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: The caller must hold the inbox lock.</description></item>
        /// <item><description><b>Other notes</b>: Both caps are applied before the message enters the queue, so
        /// the caps bound the buffered messages, not the queue slot count. A rejected message is released
        /// silently.</description></item>
        /// </list>
        /// </remarks>
        private void EnqueueNoLock(UnionDataList message)
        {
            int size = message.GetDataSize();

            if (_inbox.Count >= _inboxCapacityMessages || _inboxBytes + size > _inboxCapacityBytes)
            {
                message.Release();
                return;
            }

            _inbox.Enqueue(message);
            _inboxBytes += size;
        }

        /// <summary>
        /// Seeds the inbox with a message that must be delivered before every other buffered message.
        /// </summary>
        /// <param name="message">The message to place at the front of the inbox. Ownership transfers to the
        /// endpoint on every path.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread; the insertion happens under
        /// the inbox lock.</description></item>
        /// <item><description><b>Other notes</b>: Intended for a server trigger passed through the constructor,
        /// before the endpoint can be published or started. The message is not subject to the caps, but it
        /// occupies an inbox slot and its payload size counts toward the inbox byte usage. When the inbox is
        /// already closed the message is released.</description></item>
        /// </list>
        /// </remarks>
        protected void PrependToInbox(UnionDataList message)
        {
            if (message == null!)
            {
                return;
            }

            lock (_inboxLock)
            {
                if (_inboxState == InboxState.Closed)
                {
                    message.Release();
                    return;
                }

                _inboxBytes += message.GetDataSize();

                if (_inbox.Count == 0)
                {
                    _inbox.Enqueue(message);
                    return;
                }

                UnionDataList[] pending = _inbox.ToArray();
                _inbox.Clear();
                _inbox.Enqueue(message);
                foreach (UnionDataList item in pending)
                {
                    _inbox.Enqueue(item);
                }
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread, including concurrently with
        /// other stop requests.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: Returns <see langword="false"/> when the
        /// endpoint is already invalidated or when another caller already took the one-shot latch. Otherwise
        /// takes the latch, forwards to the base stop with a non-null reason, and returns
        /// <see langword="true"/>.</description></item>
        /// <item><description><b>Other notes</b>: There is no <see cref="IRawEndpointImp.IsRunning"/> guard, so
        /// stopping a never-started or still-starting endpoint works and releases the inbox. The latch and
        /// <see cref="IsValid"/> turn over immediately, but the call may execute the transition on the caller's
        /// thread because the serial queue drains the backlog inline, and it can therefore block while an armed
        /// conformance gate holds the transition. A missing reason is replaced with
        /// <see cref="Unknown"/>, because the base stop latches the reason as given and would otherwise deliver
        /// a null reason to the handler and to <see cref="IRawEndpointImp.Stopped"/>
        /// subscribers.</description></item>
        /// </list>
        /// </remarks>
        bool IRawUnreliableEndpoint.Stop(StopReason? reason)
        {
            if (_invalidated)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _stopLatched, 1, 0) != 0)
            {
                return false;
            }

            StopReason resolvedReason = reason ?? new Unknown(TransportName);
            base.Stop(resolvedReason, false);
            return true;
        }

        /// <summary>
        /// Performs the unreliable part of the stop transition: invalidates the endpoint, hits the endpoint
        /// gates, and completes the stop.
        /// </summary>
        /// <param name="reason">The reason the endpoint is being stopped.</param>
        /// <param name="tryBeGentle">A hint forwarded by the base class; the unreliable endpoint does not
        /// interpret it.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue; never concurrently with
        /// another transition.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: Completes the stop synchronously by calling
        /// <see cref="RawEndpoint.FinishStop"/> once. The handler stop notification is raised by the base class
        /// and may be suppressed by the handler gate.</description></item>
        /// <item><description><b>Side effects</b>: Invalidates the endpoint before the stop transition gate is
        /// hit, so conformance code that observes the gate already sees an invalid endpoint.</description></item>
        /// </list>
        /// </remarks>
        protected sealed override void BeginStop(StopReason reason, bool tryBeGentle)
        {
            _invalidated = true;
            Conformance.BeforeEndpointStopStateTransitionGate.Hit();
            Conformance.BeforeHandlerStoppedGate.Hit();
            FinishStop();
        }

        /// <summary>
        /// Handles a fatal failure by invalidating the endpoint and then invoking the base implementation.
        /// </summary>
        /// <param name="reason">The error description that caused the failure.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked on the thread that executed the failing
        /// action.</description></item>
        /// <item><description><b>Other notes</b>: The base implementation delivers the handler stop notification
        /// when the endpoint had started and always raises <see cref="IRawEndpointImp.Stopped"/>, which releases
        /// the inbox. The handler gate suppresses the notification when the start notification did not complete
        /// successfully.</description></item>
        /// </list>
        /// </remarks>
        protected override void FailHard(StopReason reason)
        {
            _invalidated = true;
            base.FailHard(reason);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call concurrently; the observed state is a
        /// best-effort snapshot.</description></item>
        /// <item><description><b>Other notes</b>: The base rejection is <see cref="SendResult.NotConnected"/>
        /// before the start completes and after a stop is requested. The unreliable contract never reports a
        /// missing connection, so that rejection is mapped to <see cref="SendResult.Error"/>; every other
        /// rejection passes through unchanged.</description></item>
        /// </list>
        /// </remarks>
        protected override bool CanSendNow(out SendResult errorCode)
        {
            if (!base.CanSendNow(out errorCode))
            {
                if (errorCode == SendResult.NotConnected)
                {
                    errorCode = SendResult.Error;
                }

                return false;
            }

            errorCode = SendResult.Ok;
            return true;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread; delivery is serialized with
        /// the stop notification by the base endpoint.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The message must not be
        /// <see langword="null"/>. It is released exactly once when the endpoint is not running or the message
        /// cannot be scheduled.</description></item>
        /// <item><description><b>Other notes</b>: The relocated <c>AfterReceivedGate</c> is hit once per
        /// impending handler delivery. Every delivery path hits it exactly once: this override for a direct
        /// inbound message, <see cref="Deliver"/> for a message delivered through an open inbox, and the flush
        /// action for each buffered message, immediately before that message begins delivery. No message is
        /// counted twice.</description></item>
        /// </list>
        /// </remarks>
        protected override void ProcessInboundMessage(UnionDataList message)
        {
            Conformance.AfterReceivedGate.Hit();
            base.ProcessInboundMessage(message);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Adds the endpoint conformance control when the predicate
        /// accepts it. The base implementation contributes no controls.</description></item>
        /// </list>
        /// </remarks>
        public override void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
            base.GetControls(dst, predicate);

            if (predicate?.Invoke(_conformance) ?? true)
            {
                dst.Add(_conformance);
            }
        }

        private static void NoOp()
        {
        }

        /// <summary>
        /// The three-state handler gate passed to the base raw endpoint.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: The gate is read from the thread that performs the terminal
        /// transition and written from the flush action; the flag is updated and read atomically.</description></item>
        /// <item><description><b>Other notes</b>: <c>OnReceived</c> is forwarded unconditionally, because the
        /// base only delivers when the endpoint is started, which already implies a successful start
        /// notification. <c>OnStopped</c> is forwarded only after the unreliable start notification returned
        /// successfully, which preserves the handler contract for a throwing start and for a stop raised while
        /// starting. <see cref="RawEndpoint.RaiseStopped"/> is unaffected by the gate, so route removal and
        /// transport teardown still observe every terminal path.</description></item>
        /// </list>
        /// </remarks>
        private sealed class HandlerGate : IRawHandler
        {
            private readonly IRawUnreliableHandler _handler;
            private int _startCompleted;

            /// <summary>
            /// Initializes a new gate that forwards to the given handler.
            /// </summary>
            /// <param name="handler">The handler to forward to; must not be null.</param>
            /// <exception cref="ArgumentNullException"><paramref name="handler"/> is
            /// <see langword="null"/>.</exception>
            public HandlerGate(IRawUnreliableHandler handler)
            {
                _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            }

            /// <summary>
            /// Opens the gate so that the handler stop notification will be forwarded.
            /// </summary>
            /// <remarks>
            /// <list type="bullet">
            /// <item><description><b>Thread safety</b>: Safe to call from any thread.</description></item>
            /// <item><description><b>Other notes</b>: Called exactly once, after the handler's start notification
            /// returned successfully. Later calls are no-ops.</description></item>
            /// </list>
            /// </remarks>
            public void MarkStartCompleted()
            {
                Interlocked.Exchange(ref _startCompleted, 1);
            }

            /// <inheritdoc/>
            public void OnReceived(UnionDataList receivedBuffer)
            {
                _handler.OnReceived(receivedBuffer);
            }

            /// <inheritdoc/>
            public void OnStopped(StopReason reason)
            {
                if (Volatile.Read(ref _startCompleted) == 0)
                {
                    return;
                }

                _handler.OnStopped(reason);
            }
        }
    }
}
